using HarmonyLib;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class GrabbableObjectPatch
{
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.ItemActivate))]
    [HarmonyPrefix]
    private static bool MirrorFusionActivate(ref GrabbableObject __instance)
    {
        UpsideDownMirrorBehaviour upsideDownMirrorBehaviour = __instance.GetComponentInChildren<UpsideDownMirrorBehaviour>();
        if (upsideDownMirrorBehaviour != null && upsideDownMirrorBehaviour.canFusion)
        {
            upsideDownMirrorBehaviour.CompleteFusionServerRpc();
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.EnableItemMeshes))]
    [HarmonyPostfix]
    private static void EnableItemMeshes(ref GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.PocketItem))]
    [HarmonyPostfix]
    private static void PocketItem(ref GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }

    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.DiscardItem))]
    [HarmonyPostfix]
    private static void DiscardItem(ref GrabbableObject __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
