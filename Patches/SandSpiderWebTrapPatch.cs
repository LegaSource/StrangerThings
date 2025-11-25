using HarmonyLib;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class SandSpiderWebTrapPatch
{
    [HarmonyPatch(typeof(SandSpiderWebTrap), nameof(SandSpiderWebTrap.Awake))]
    [HarmonyPostfix]
    private static void AwakeSpiderWeb(ref SandSpiderWebTrap __instance)
    {
        if (GameNetworkManager.Instance?.localPlayerController != null)
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
