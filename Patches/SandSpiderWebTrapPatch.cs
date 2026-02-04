using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class SandSpiderWebTrapPatch
{
    [HarmonyPatch(typeof(SandSpiderWebTrap), nameof(SandSpiderWebTrap.Awake))]
    [HarmonyPostfix]
    private static void AwakeSpiderWeb(SandSpiderWebTrap __instance)
    {
        if (LFCUtilities.LocalPlayer != null)
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
