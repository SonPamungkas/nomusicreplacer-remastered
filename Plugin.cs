using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NOMusicReplacer.Patch;
using UnityEngine;
using UnityEngine.Networking;

namespace NOMusicReplacer
{
    public class MusicVolumeScaler : MonoBehaviour
    {
        public AudioSource source;
        public string packName;
        public bool isDeepDive = false;
        public ConfigEntry<int> config;

        void Awake() 
        { 
            source = GetComponent<AudioSource>(); 
            MusicReplacerBase.ActiveScalers.Add(this);
        }

        void OnDestroy()
        {
            MusicReplacerBase.ActiveScalers.Remove(this);
        }

        void Update()
        {
            if (source == null || source.clip == null) return;
            if (packName != null) return; // Already initialized

            if (MusicReplacerBase.ClipToPack.TryGetValue(source.clip.GetInstanceID(), out var pName))
            {
                packName = pName;
            }
            else
            {
                packName = MusicReplacerBase.GetCleanName(source.clip.name);
            }

            if (MusicReplacerBase.VolumeConfigs.TryGetValue(packName, out var cfg))
            {
                config = cfg;
            }
            else if (MusicReplacerBase.DeepDiveEnabled.Value && MusicReplacerBase.CheckedDeepDiveClips.Contains(source.clip.GetInstanceID()))
            {
                isDeepDive = true;
            }
        }
    }

    [BepInPlugin(modGUID, modName, modVersion)]
    public class MusicReplacerBase : BaseUnityPlugin
    {
        private const string modGUID = "Truffle.NOMusicReplacer";
        private const string modName = "Nuclear Option Music Replacer";
        private const string modVersion = "0.33.3";

        private readonly Harmony harmony = new Harmony(modGUID);

        internal static Dictionary<string, bool> BundleDict = new Dictionary<string, bool>();
        internal static Dictionary<string, List<string>> AvailableFiles = new Dictionary<string, List<string>>();
        internal static Dictionary<string, string> ConversionDict = new Dictionary<string, string>();
        internal static Dictionary<string, ConfigEntry<int>> VolumeConfigs = new Dictionary<string, ConfigEntry<int>>();
        internal static Dictionary<int, string> ClipToPack = new Dictionary<int, string>();
        internal static List<MusicVolumeScaler> ActiveScalers = new List<MusicVolumeScaler>();

        internal static ConfigEntry<bool> DeepDiveEnabled;
        internal static ConfigEntry<int> GlobalDeepDiveVolume;
        internal static HashSet<int> CheckedDeepDiveClips = new HashSet<int>();

        internal static MusicReplacerBase Instance;
        internal static ManualLogSource mls;
        internal static System.Random rng = new System.Random();
        internal static bool LoopSetting = false;
        internal static string FolderPath;
        internal static string CurrentSong;
        internal static string SoundsPath;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            harmony.PatchAll(typeof(MusicReplacerBase));
            harmony.PatchAll(typeof(MusicPatch));
            harmony.PatchAll(typeof(VolumePatch));
            harmony.PatchAll(typeof(DeepDivePatch));
            
            mls.LogInfo("Music Replacer Started");
            FolderPath = Instance.Info.Location;
            FolderPath = FolderPath.TrimEnd("NOMusicReplacer.dll".ToCharArray());

            DeepDiveEnabled = Config.Bind("Deep Dive Settings", "Enable Deep Dive", false, "Creates a folder for EVERY sound that plays in the Sounds folder, allowing you to replace them.");
            GlobalDeepDiveVolume = Config.Bind("Deep Dive Settings", "Global Deep Dive Volume", 100, new ConfigDescription("Global volume multiplier (%) for replaced Deep Dive sounds", new AcceptableValueRange<int>(0, 200)));
            GlobalDeepDiveVolume.SettingChanged += (s, e) =>
            {
                foreach (var scaler in ActiveScalers)
                {
                    if (scaler.isDeepDive && scaler.source != null)
                    {
                        scaler.source.volume = GlobalDeepDiveVolume.Value / 100f;
                        MusicReplacerBase.mls.LogInfo($"Dynamically updated Deep Dive volume to {GlobalDeepDiveVolume.Value}%");
                    }
                }
            };

            XmlDocument settings = new XmlDocument();
            string settingsPath = Path.Combine(FolderPath, "settings.xml");
            if (File.Exists(settingsPath))
            {
                try
                {
                    settings.Load(settingsPath);
                    XmlNode node = settings.DocumentElement.SelectSingleNode("/Settings");

                    if (node != null)
                    {
                        string sLoop = node.SelectSingleNode("LoopMusic").InnerText;
                        bool.TryParse(sLoop, out bool loop_result);
                        LoopSetting = loop_result;
                    }
                    else
                    {
                        mls.LogError("settings.xml corrupted");
                    }
                }
                catch (Exception ex)
                {
                    mls.LogError("Failed to load settings.xml: " + ex.Message);
                }
            }

            SoundsPath = Path.Combine(FolderPath, "Sounds");
            if (!Directory.Exists(SoundsPath)) Directory.CreateDirectory(SoundsPath);

            PreloadAudio(FolderPath);
        }

        internal static AudioClip GetReplacement(string input)
        {
            if (!AvailableFiles.ContainsKey(input) || AvailableFiles[input].Count == 0) return null;
            List<string> clip_list = AvailableFiles[input];
            int index = rng.Next(clip_list.Count);
            AudioClip clip = Instance.LoadSong(clip_list[index]);
            if (clip != null)
            {
                ClipToPack[clip.GetInstanceID()] = input;
            }
            return clip;
        }

        public void LoadFolderDynamically(string packName)
        {
            string folderPath = Path.Combine(FolderPath, "Audio", packName);
            if (!Directory.Exists(folderPath)) return;

            string[] songPaths = Directory.GetFiles(folderPath);
            if (songPaths.Length == 0)
            {
                mls.LogInfo(packName + " music not found.");
                BundleDict[packName] = false;
                return;
            }
            List<string> musicList = new List<string>();
            foreach (string song in songPaths)
            {
                musicList.Add(song);
            }
            BundleDict[packName] = true;
            AvailableFiles[packName] = musicList;
            
            if (!VolumeConfigs.ContainsKey(packName))
            {
                var cfg = Config.Bind("Music Volumes", packName, 100, new ConfigDescription("Volume multiplier (%) for " + packName, new AcceptableValueRange<int>(0, 200)));
                VolumeConfigs[packName] = cfg;

                cfg.SettingChanged += (s, e) =>
                {
                    foreach (var scaler in ActiveScalers)
                    {
                        if (scaler.config == cfg && scaler.source != null)
                        {
                            scaler.source.volume = cfg.Value / 100f;
                            MusicReplacerBase.mls.LogInfo($"Dynamically updated volume for {packName} to {cfg.Value}%");
                        }
                    }
                };
            }

            mls.LogInfo(packName + " asset bundle indexed dynamically");
        }

        public static string GetCleanName(string clipName)
        {
            return clipName.Replace("(UnityEngine.AudioClip)", "").Trim();
        }

        void PreloadAudio(string path)
        {
            mls.LogInfo("BASEPATH: " + path);
            string AudioPath = Path.Combine(path, "Audio");

            if (!Directory.Exists(AudioPath))
            {
                Directory.CreateDirectory(AudioPath);
                mls.LogInfo("Created main Audio folder");
            }

            string[] audioFolders = Directory.GetDirectories(AudioPath);
            foreach (string folder in audioFolders)
            {
                string packName = new DirectoryInfo(folder).Name;
                LoadFolderDynamically(packName);

                string expectedAudioClipString = packName + " (UnityEngine.AudioClip)";
                if (!ConversionDict.ContainsKey(expectedAudioClipString))
                {
                    ConversionDict.Add(expectedAudioClipString, packName);
                }
            }
        }

        internal static AudioClip GetDeepDiveReplacement(string clipName)
        {
            string cleanName = GetCleanName(clipName);
            string folderPath = Path.Combine(SoundsPath, cleanName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                return null;
            }

            string[] songPaths = Directory.GetFiles(folderPath);
            if (songPaths.Length == 0) return null;

            int index = rng.Next(songPaths.Length);
            return Instance.LoadSong(songPaths[index]);
        }

        AudioClip LoadSong(string path)
        {
            var musicType = GetAudioType(path);
            var uri = new Uri(path).AbsoluteUri;
            var loader = UnityWebRequestMultimedia.GetAudioClip(uri, musicType);
            
            ((DownloadHandlerAudioClip)loader.downloadHandler).streamAudio = true;
            loader.SendWebRequest();

            while (!loader.isDone) { }

            var clip = DownloadHandlerAudioClip.GetContent(loader);
            if (clip != null && clip.length > 0)
            {
                clip.name = Path.GetFileName(path);
                return clip;
            }

            loader.Dispose();
            return null;
        }

        private static AudioType GetAudioType(string path)
        {
            var extension = Path.GetExtension(path).ToLower();
            if (extension == ".wav") return AudioType.WAV;
            if (extension == ".ogg") return AudioType.OGGVORBIS;
            if (extension == ".mp3") return AudioType.MPEG;
            if (extension == ".opus") return AudioType.OGGVORBIS;
            mls.LogError($"Unsupported extension: {path}");
            return AudioType.UNKNOWN;
        }
    }
}
