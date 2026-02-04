using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class FlashlightItemPatch
{
    [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.SwitchFlashlight))]
    [HarmonyPostfix]
    private static void SwitchFlashlight(FlashlightItem __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.PocketFlashlightClientRpc))]
    [HarmonyPostfix]
    private static void PocketFlashlightForClients(FlashlightItem __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
