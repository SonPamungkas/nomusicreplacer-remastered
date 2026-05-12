using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NOMusicReplacer.Patch;
using UnityEngine;
using UnityEngine.Networking;

namespace NOMusicReplacer
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class MusicReplacerBase : BaseUnityPlugin
    {
        private const string modGUID = "Truffle.NOMusicReplacer";
        private const string modName = "Nuclear Option Music Replacer";
        private const string modVersion = "0.33.2";

        private readonly Harmony harmony = new Harmony(modGUID);

        internal static Dictionary<string,bool> BundleDict = new Dictionary<string,bool>();
        internal static Dictionary<string,List<AudioClip>> AudioDict = new Dictionary<string,List<AudioClip>>();
        
        internal static Dictionary<string,string> ConversionDict = new Dictionary<string,string>();


        internal static MusicReplacerBase Instance;
        internal static ManualLogSource mls;
        internal static System.Random rng = new System.Random();
        internal static bool LoopSetting = false;
        internal static string FolderPath;
        internal static string CurrentSong;
        XmlDocument settings = new XmlDocument();


        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            

            harmony.PatchAll(typeof(MusicReplacerBase));
            harmony.PatchAll(typeof(MusicPatch));
            mls.LogInfo("Music Replacer Started");
            FolderPath = Instance.Info.Location;
            
            FolderPath = FolderPath.TrimEnd("NOMusicReplacer.dll".ToCharArray());

            string settingsPath = Path.Combine(FolderPath, "settings.xml");
            if (File.Exists(settingsPath))
            {
                try 
                {
                    settings.Load(settingsPath);
                    XmlNode node = settings.DocumentElement.SelectSingleNode("/Settings");

                    if (node != null) {
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

            // Automatically scan and preload all folders in Audio
            PreloadAudio(FolderPath);
        }

        internal static AudioClip GetReplacement(string input)
        {
            if (!AudioDict.ContainsKey(input) || AudioDict[input].Count == 0) return null;
            List<AudioClip> clip_list = AudioDict[input];
            int index = rng.Next(clip_list.Count);
            return clip_list[index];
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
            List<AudioClip> musicList = new List<AudioClip>();
            foreach (string song in songPaths)
            {
                AudioClip result = LoadSong(song);
                if (result != null)
                {
                    musicList.Add(result);
                }
            }
            if (musicList.Count == 0)
            {
                mls.LogInfo(packName + " music not found or formats unsupported.");
                BundleDict[packName] = false;
                return;
            }
            BundleDict[packName] = true;
            if (AudioDict.ContainsKey(packName))
                AudioDict[packName] = musicList;
            else
                AudioDict.Add(packName, musicList);
            mls.LogInfo(packName + " asset bundle loaded dynamically");
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
                
                // Pre-populate ConversionDict so it maps the exact AudioClip string
                string expectedAudioClipString = packName + " (UnityEngine.AudioClip)";
                if (!ConversionDict.ContainsKey(expectedAudioClipString))
                {
                    ConversionDict.Add(expectedAudioClipString, packName);
                }
            }
        }

        AudioClip LoadSong(string path)
        {
            var musicType = GetAudioType(path);
            var loader = UnityWebRequestMultimedia.GetAudioClip(path, musicType);
            loader.SendWebRequest();

            while (!loader.isDone) { }

#pragma warning disable CS0618
            if (loader.isNetworkError || loader.isHttpError) 
            {
                loader.Dispose();
                return null;
            }
#pragma warning restore CS0618

            var clip = DownloadHandlerAudioClip.GetContent(loader);
            if (clip != null && clip.loadState == AudioDataLoadState.Loaded && clip.length > 0)
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

            if (extension == ".wav")
                return AudioType.WAV;
            if (extension == ".ogg")
                return AudioType.OGGVORBIS;
            if (extension == ".mp3")
                return AudioType.MPEG;
            if (extension == ".opus")
                return AudioType.OGGVORBIS;

            mls.LogError($"Unsupported extension: {path}");
            return AudioType.UNKNOWN;
        }
    }
}
