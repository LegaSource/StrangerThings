using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Behaviours.Scripts;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class DoorLockPatch
{
    [HarmonyPatch(typeof(DoorLock), nameof(DoorLock.OnTriggerStay))]
    [HarmonyPostfix]
    private static void BreakDoor(ref DoorLock __instance, ref Collider other)
    {
        if (!LFCUtilities.IsServer || __instance.isDoorOpened || __instance.GetComponent<DoorProjectile>() != null || other == null || !other.CompareTag("Enemy")) return;

        EnemyAICollisionDetect collision = other.GetComponent<EnemyAICollisionDetect>();
        if (collision == null || collision.mainScript is not DemogorgonAI demogorgon || !demogorgon.isDashing) return;

        Vector3 direction = (demogorgon.targetPlayer.transform.position - demogorgon.transform.position).normalized * 5f;
        demogorgon.BreakDoorEveryoneRpc(__instance.GetComponentInParent<NetworkObject>(), direction);

        demogorgon.agent.speed = 0f;
        demogorgon.agent.velocity = Vector3.zero;
    }
}