using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using NOMusicReplacer;

namespace NOMusicReplacer.Patch
{
    [HarmonyPatch(typeof(MusicManager))]
    internal class MusicPatch
    {
        static void EnsureScalers(MusicManager __instance)
        {
            var sources = __instance.GetComponentsInChildren<AudioSource>(true);
            foreach (var src in sources)
            {
                if (src.gameObject.GetComponent<MusicVolumeScaler>() == null)
                {
                    src.gameObject.AddComponent<MusicVolumeScaler>();
                }
            }
        }

        [HarmonyPatch("PlayMusic")]
        [HarmonyPrefix]
        static bool SwapTheme(MusicManager __instance, ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);

            if (clean_name == "Ignition" && MusicReplacerBase.CurrentSong == "Ignition")
            {
                MusicReplacerBase.mls.LogInfo("Ignition already playing (replacement), skipping restart.");
                return false;
            }

            AudioClip new_clip = GetNewSong(song_title);

            if (new_clip != null)
            {
                audioClip = new_clip;
            }

            return true;
        }

        [HarmonyPatch("PlayMusic")]
        [HarmonyPostfix]
        static void PlayMusicPostfix(MusicManager __instance)
        {
            EnsureScalers(__instance);
        }

        [HarmonyPatch("CrossFadeMusic")]
        [HarmonyPrefix]
        static bool SwapCrossTheme(MusicManager __instance, ref AudioClip audioClip, ref bool repeat)
        {
            string song_title = audioClip.ToString();
            string clean_name = MusicReplacerBase.GetCleanName(song_title);

            if (clean_name == "Ignition" && MusicReplacerBase.CurrentSong == "Ignition")
            {
                MusicReplacerBase.mls.LogInfo("Ignition already playing (crossfade replacement), skipping restart.");
                return false;
            }

            AudioClip new_clip = GetNewSong(song_title);

            if (new_clip != null)
            {
                audioClip = new_clip;
            }

            return true;
        }

        [HarmonyPatch("CrossFadeMusic")]
        [HarmonyPostfix]
        static void CrossFadeMusicPostfix(MusicManager __instance)
        {
            EnsureScalers(__instance);
        }

        static AudioClip GetNewSong(string song_title)
        {
            string target_key;

            if (MusicReplacerBase.ConversionDict.ContainsKey(song_title))
            {
                target_key = MusicReplacerBase.ConversionDict[song_title];
            }
            else
            {
                target_key = MusicReplacerBase.GetCleanName(song_title);
                MusicReplacerBase.ConversionDict.Add(song_title, target_key);
            }

            AudioClip new_clip = MusicReplacerBase.GetReplacement(target_key);
            
            if (new_clip != null)
            {
                MusicReplacerBase.CurrentSong = target_key;
                MusicReplacerBase.mls.LogInfo("Replacing \"" + song_title + "\" with \"" + new_clip.name + "\"");
                return new_clip;
            }

            MusicReplacerBase.CurrentSong = target_key; // Keep track even if original plays
            MusicReplacerBase.mls.LogInfo("No replacement found for \"" + target_key + "\". Playing original.");
            return null;
        }
    }
}
