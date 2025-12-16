using HarmonyLib;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class FlashlightItemPatch
{
    [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.SwitchFlashlight))]
    [HarmonyPostfix]
    private static void SwitchFlashlight(ref FlashlightItem __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.PocketFlashlightClientRpc))]
    [HarmonyPostfix]
    private static void PocketFlashlightForClients(ref FlashlightItem __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
