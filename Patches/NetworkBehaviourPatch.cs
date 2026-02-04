using HarmonyLib;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Behaviours.Items;
using StrangerThings.Registries;
using Unity.Netcode;

namespace StrangerThings.Patches;

public class NetworkBehaviourPatch
{
    [HarmonyPatch(typeof(NetworkBehaviour), nameof(NetworkBehaviour.InternalOnNetworkSpawn))]
    [HarmonyPostfix]
    private static void SpawnNetworkBehaviour(NetworkBehaviour __instance)
    {
        if (!__instance.gameObject.TryGetComponent<VehicleController>(out _) && (DimensionRegistry.IsWhitelisted(__instance.gameObject) || __instance is GrabbableObject))
        {
            if (__instance is UpsideDownObject || __instance is UpsideDownEnemyAI)
                DimensionRegistry.SetInUpsideDown(__instance.gameObject, true);
            else
                DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
        }
    }
}
