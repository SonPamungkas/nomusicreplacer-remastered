using HarmonyLib;
using UnityEngine;
using NOMusicReplacer;

namespace NOMusicReplacer.Patch
{
    [HarmonyPatch(typeof(AudioSource))]
    internal class DeepDivePatch
    {
        static void EnsureScaler(AudioSource source)
        {
            if (source.gameObject.GetComponent<MusicVolumeScaler>() == null)
            {
                source.gameObject.AddComponent<MusicVolumeScaler>();
            }
        }

        [HarmonyPatch("PlayOneShot", new System.Type[] { typeof(AudioClip), typeof(float) })]
        [HarmonyPrefix]
        static bool PlayOneShotPrefix(AudioSource __instance, ref AudioClip clip, ref float volumeScale)
        {
            if (!MusicReplacerBase.DeepDiveEnabled.Value || clip == null) return true;
            AudioClip newClip = MusicReplacerBase.GetDeepDiveReplacement(clip.name);
            if (newClip != null)
            {
                clip = newClip;
            }
            if (MusicReplacerBase.CheckedDeepDiveClips.Contains(clip.GetInstanceID()))
            {
                volumeScale *= (MusicReplacerBase.GlobalDeepDiveVolume.Value / 100f);
            }
            return true;
        }

        [HarmonyPatch("clip", MethodType.Setter)]
        [HarmonyPrefix]
        static bool SetClipPrefix(AudioSource __instance, ref AudioClip value)
        {
            if (!MusicReplacerBase.DeepDiveEnabled.Value || value == null) return true;
            AudioClip newClip = MusicReplacerBase.GetDeepDiveReplacement(value.name);
            if (newClip != null)
            {
                value = newClip;
            }
            EnsureScaler(__instance);
            return true;
        }

        [HarmonyPatch("Play", new System.Type[0])]
        [HarmonyPrefix]
        static bool PlayPrefix(AudioSource __instance)
        {
            if (!MusicReplacerBase.DeepDiveEnabled.Value || __instance.clip == null) return true;
            
            if (!MusicReplacerBase.CheckedDeepDiveClips.Contains(__instance.clip.GetInstanceID()))
            {
                MusicReplacerBase.CheckedDeepDiveClips.Add(__instance.clip.GetInstanceID());
                AudioClip newClip = MusicReplacerBase.GetDeepDiveReplacement(__instance.clip.name);
                if (newClip != null)
                {
                    __instance.clip = newClip;
                }
            }
            EnsureScaler(__instance);
            return true;
        }
    }
}
