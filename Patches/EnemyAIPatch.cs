using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StrangerThings.Patches;

public class EnemyAIPatch
{
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Start))]
    [HarmonyPostfix]
    private static void StartEnemy(ref EnemyAI __instance)
    {
        if (DimensionRegistry.IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);

        if (LFCUtilities.IsServer && /*__instance.enemyType != null && !__instance.enemyType.isDaytimeEnemy &&*/ !DimensionRegistry.IsInUpsideDown(__instance.gameObject))
            SpawnUpsideDownEnemy(__instance.isOutside);
    }

    private static void SpawnUpsideDownEnemy(bool isOutside)
    {
        /*RoundManager rm = RoundManager.Instance;

        Vector3 spawnPosition = isOutside
            ? rm.outsideAINodes[Random.Range(0, rm.outsideAINodes.Length)].transform.position
            : rm.insideAINodes[Random.Range(0, rm.insideAINodes.Length)].transform.position;
        spawnPosition = rm.GetRandomNavMeshPositionInRadiusSpherical(spawnPosition);

        GameObject gameObject = Object.Instantiate(StrangerThings.demogorgonEnemy.enemyPrefab, spawnPosition, Quaternion.Euler(Vector3.zero));
        NetworkObject networkObject = gameObject.GetComponentInChildren<NetworkObject>();
        networkObject.Spawn(destroyWithScene: true);
        StrangerThingsNetworkManager.Instance.SetInUpsideDownEveryoneRpc(networkObject, true);*/

        /*RoundManager rm = RoundManager.Instance;

        EnemyType enemyType = isOutside
            ? GetRandomEnemy(rm.currentLevel.OutsideEnemies)
            : GetRandomEnemy(rm.currentLevel.Enemies);
        if (enemyType == null) return;

        Vector3 spawnPosition = isOutside
            ? rm.outsideAINodes[Random.Range(0, rm.outsideAINodes.Length)].transform.position
            : rm.insideAINodes[Random.Range(0, rm.insideAINodes.Length)].transform.position;
        spawnPosition = rm.GetRandomNavMeshPositionInRadiusSpherical(spawnPosition);

        GameObject gameObject = Object.Instantiate(enemyType.enemyPrefab, spawnPosition, Quaternion.Euler(Vector3.zero));
        NetworkObject networkObject = gameObject.GetComponentInChildren<NetworkObject>();
        networkObject.Spawn(destroyWithScene: true);
        StrangerThingsNetworkManager.Instance.SetInUpsideDownEveryoneRpc(networkObject, true);
        enemyType.numberSpawned++;*/
    }

    public static EnemyType GetRandomEnemy(List<SpawnableEnemyWithRarity> enemies)
    {
        List<SpawnableEnemyWithRarity> validEnemies = enemies
            .Where(e => e.enemyType != null && e.enemyType.numberSpawned < e.enemyType.MaxCount && !e.enemyType.spawningDisabled && e.rarity > 0)
            .ToList();
        if (validEnemies.Count == 0) return null;

        int totalWeight = validEnemies.Sum(e => e.rarity);
        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (SpawnableEnemyWithRarity enemy in validEnemies)
        {
            cumulative += enemy.rarity;
            if (roll < cumulative) return enemy.enemyType;
        }

        return null;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
    [HarmonyPrefix]
    private static bool PlayerIsTargetable(ref EnemyAI __instance, ref bool __result, PlayerControllerB playerScript)
    {
        if (!DimensionRegistry.AreInSameDimension(__instance.gameObject, playerScript.gameObject))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.NavigateTowardsTargetPlayer))]
    [HarmonyPrefix]
    private static bool NavigateTowardsPlayer(ref EnemyAI __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(__instance.gameObject, __instance.targetPlayer.gameObject))
        {
            // Naviguer vers le portail le temps que le monstre perde le joueur
            UpsideDownPortal upsideDownPortal = DimensionRegistry.GetClosestPortal(__instance.transform.position);
            if (upsideDownPortal == null) return true;

            __instance.destination = upsideDownPortal.transform.position;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.TargetClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventTargetClosestPlayer(ref EnemyAI __instance, ref bool __result)
    {
        if (__result && __instance.targetPlayer != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __instance.targetPlayer.gameObject))
            __result = false;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CheckLineOfSightForClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventCheckLineOfSightForClosestPlayer(ref EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CheckLineOfSightForPlayer))]
    [HarmonyPostfix]
    private static void PreventCheckLineOfSightForPlayer(ref EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.GetClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventGetClosestPlayer(ref EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }
}
