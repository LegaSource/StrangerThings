using HarmonyLib;
using StrangerThings.Registries;
using Unity.Netcode;

namespace StrangerThings.Patches;

public class NetworkBehaviourPatch
{
    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.InternalOnNetworkSpawn))]
    [HarmonyPostfix]
    private static void SpawnNetworkBehaviour(ref NetworkBehaviour __instance)
    {
        if (GameNetworkManager.Instance?.localPlayerController == null) return;

        if (DimensionRegistry.IsWhitelisted(__instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
