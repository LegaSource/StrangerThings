using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class GrabbableObjectPatch
{
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.EnableItemMeshes))]
    [HarmonyPostfix]
    private static void EnableItemMeshes(GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.PocketItem))]
    [HarmonyPostfix]
    private static void PocketItem(GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItem))]
    [HarmonyPostfix]
    private static void DiscardItem(GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
