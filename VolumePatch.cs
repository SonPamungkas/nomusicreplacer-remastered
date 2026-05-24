using HarmonyLib;
using UnityEngine;

namespace NOMusicReplacer.Patch
{
    [HarmonyPatch(typeof(AudioSource))]
    internal class VolumePatch
    {
        [HarmonyPatch("volume", MethodType.Setter)]
        [HarmonyPrefix]
        static void SetVolumePrefix(AudioSource __instance, ref float value)
        {
            if (__instance.clip == null) return;
            
            string packName = null;
            if (MusicReplacerBase.ClipToPack.TryGetValue(__instance.clip.GetInstanceID(), out var pName))
            {
                packName = pName;
            }
            else
            {
                packName = MusicReplacerBase.GetCleanName(__instance.clip.name);
            }

            if (MusicReplacerBase.VolumeConfigs.TryGetValue(packName, out var config))
            {
                value *= (config.Value / 100f);
            }
            else if (MusicReplacerBase.DeepDiveEnabled.Value && MusicReplacerBase.CheckedDeepDiveClips.Contains(__instance.clip.GetInstanceID()))
            {
                value *= (MusicReplacerBase.GlobalDeepDiveVolume.Value / 100f);
            }
        }
    }
}
