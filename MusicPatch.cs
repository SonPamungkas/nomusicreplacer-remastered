using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;



namespace NOMusicReplacer.Patch
{
    [HarmonyPatch(typeof(MusicManager))]
    internal class MusicPatch
    {
        [HarmonyPatch("PlayMusic")]
        [HarmonyPrefix]
        static bool SwapTheme(ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);

            AudioClip new_clip = GetNewSong(song_title);

            if (new_clip != null)
            {
                if (clean_name == "Ignition" && MusicReplacerBase.CurrentSong == "Ignition")
                {
                    MusicReplacerBase.mls.LogInfo("Ignition already playing (replacement), skipping restart.");
                    return false;
                }

                audioClip = new_clip;
                MusicReplacerBase.mls.LogInfo("Replaced: " + song_title + " with: " + new_clip.ToString());

                MusicReplacerBase.CurrentSong = clean_name;

                if (clean_name == "Ignition")
                {
                    // Only loop Ignition if we are in the main menu
                    if (SceneManager.GetActiveScene().name == "MainMenu")
                    {
                        repeat = MusicReplacerBase.LoopSetting;
                    }
                    else
                    {
                        repeat = false;
                    }
                }
            }

            return true;
        }

        [HarmonyPatch("CrossFadeMusic")]
        [HarmonyPrefix]
        static bool SwapCrossTheme(ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);

            AudioClip new_clip = GetNewSong(song_title);

            if (new_clip != null)
            {
                if (clean_name == "Ignition" && MusicReplacerBase.CurrentSong == "Ignition")
                {
                    MusicReplacerBase.mls.LogInfo("Ignition already playing in CrossFade (replacement), skipping.");
                    return false;
                }

                audioClip = new_clip;
                MusicReplacerBase.mls.LogInfo("Replaced: " + song_title + " with: " + new_clip.ToString());

                MusicReplacerBase.CurrentSong = clean_name;

                if (clean_name == "Ignition")
                {
                    // Only loop Ignition if we are in the main menu
                    if (SceneManager.GetActiveScene().name == "MainMenu")
                    {
                        repeat = MusicReplacerBase.LoopSetting;
                    }
                    else
                    {
                        repeat = false;
                    }
                }
            }

            return true;
        }

        [HarmonyPatch("StopMusic")]
        [HarmonyPrefix]
        static bool PreventStop()
        {
            if (MusicReplacerBase.CurrentSong == "Ignition")
            {
                // Only prevent stop if we have a valid replacement to keep playing
                if (MusicReplacerBase.AudioDict.ContainsKey("Ignition") && MusicReplacerBase.AudioDict["Ignition"].Count > 0)
                {
                    MusicReplacerBase.mls.LogInfo("Preventing StopMusic during Ignition (replacement active).");
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch("FadeOut")]
        [HarmonyPrefix]
        static bool PreventFadeOut()
        {
            if (MusicReplacerBase.CurrentSong == "Ignition")
            {
                // Only prevent fade out if we have a valid replacement to keep playing
                if (MusicReplacerBase.AudioDict.ContainsKey("Ignition") && MusicReplacerBase.AudioDict["Ignition"].Count > 0)
                {
                    MusicReplacerBase.mls.LogInfo("Preventing FadeOut during Ignition (replacement active).");
                    return false;
                }
            }
            return true;
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
