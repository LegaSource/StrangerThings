using HarmonyLib;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

internal class DeadBodyInfoPatch
{
    [HarmonyPatch(typeof(DeadBodyInfo), nameof(DeadBodyInfo.Awake))]
    [HarmonyPostfix]
    private static void AwakeDeadBodyInfo(ref DeadBodyInfo __instance)
    {
        if (GameNetworkManager.Instance?.localPlayerController != null)
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
