using System;
using HarmonyLib;
using UnityEngine;



namespace NOMusicReplacer.Patch
{
    [HarmonyPatch(typeof(MusicManager))]
    internal class MusicPatch
    {
        [HarmonyPatch("PlayMusic")]
        [HarmonyPrefix]
        static void SwapTheme(ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);
            if (clean_name == "Ignition" || clean_name == "9. PALA" || clean_name == "2. BDF")
            {
                repeat = MusicReplacerBase.LoopSetting;
            }

            AudioClip new_clip = GetNewSong(song_title);

            
            if (new_clip != null)
            {
                audioClip = new_clip;
                MusicReplacerBase.mls.LogInfo("Replaced: " + song_title + " with: " + new_clip.ToString());
            }
            else
            {
                MusicReplacerBase.mls.LogError(song_title + " resulted in a failed pull from an asset bundle");
            }
        }

        [HarmonyPatch("CrossFadeMusic")]
        [HarmonyPrefix]
        static void SwapCrossTheme(ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);
            if (clean_name == "Ignition" || clean_name == "9. PALA" || clean_name == "2. BDF")
            {
                repeat = MusicReplacerBase.LoopSetting;
            }

            AudioClip new_clip = GetNewSong(song_title);


            if (new_clip != null)
            {
                audioClip = new_clip;
                MusicReplacerBase.mls.LogInfo("Replaced: " + song_title + " with: " + new_clip.ToString());
            }

        }

        static AudioClip GetNewSong(string song_title) 
        {
            string target_key = null;
            if (MusicReplacerBase.ConversionDict.ContainsKey(song_title))
            {
                target_key = MusicReplacerBase.ConversionDict[song_title];
            }
            else
            {
                target_key = MusicReplacerBase.GetCleanName(song_title);
                MusicReplacerBase.ConversionDict.Add(song_title, target_key);

                string folderPath = System.IO.Path.Combine(MusicReplacerBase.FolderPath, "Audio", target_key);
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                    MusicReplacerBase.mls.LogInfo("Captured and created new Audio folder: " + target_key);
                    MusicReplacerBase.BundleDict[target_key] = false;
                }
                else if (!MusicReplacerBase.BundleDict.ContainsKey(target_key))
                {
                    MusicReplacerBase.Instance.LoadFolderDynamically(target_key);
                }
            }

            if (!MusicReplacerBase.BundleDict.ContainsKey(target_key) || MusicReplacerBase.BundleDict[target_key] == false)
            {
                MusicReplacerBase.mls.LogInfo("Asset Bundle not found, playing original: " + song_title);
                return null;
            }

            return MusicReplacerBase.GetReplacement(target_key);
        }
    }
}
