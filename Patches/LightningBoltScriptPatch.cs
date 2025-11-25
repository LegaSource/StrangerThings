using DigitalRuby.ThunderAndLightning;
using HarmonyLib;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Patches;

public class LightningBoltScriptPatch
{
    [HarmonyPatch(typeof(LightningBoltScript), nameof(LightningBoltScript.CreateParameters))]
    [HarmonyPostfix]
    private static void UpsideDownStrike(ref LightningBoltParameters __result)
    {
        if (!DimensionRegistry.IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject)) return;

        Color32 red = new Color32(230, 25, 25, 255);

        __result.Color = red;
        __result.MainTrunkTintColor = red;

        __result.Intensity = 1.6f;
        __result.GlowIntensity = 1.4f;
        __result.GlowWidthMultiplier = 1.2f;

        // Génère plus de branches à l'éclair
        __result.Forkedness = 1.2f;
    }
}
