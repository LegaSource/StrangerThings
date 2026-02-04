using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class EnemyAIPatch
{
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Start))]
    [HarmonyPostfix]
    private static void StartEnemy(ref EnemyAI __instance)
    {
        if (LFCUtilities.IsServer && __instance is not UpsideDownEnemyAI)
            SpawnUpsideDownEnemy(__instance.isOutside, __instance.transform.position);
    }

    private static void SpawnUpsideDownEnemy(bool isOutside, Vector3 position)
    {
        EnemyType enemyType = GetRandomEnemy(isOutside);
        if (enemyType != null)
        {
            position = RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(position);
            GameObject gameObject = Object.Instantiate(enemyType.enemyPrefab, position, Quaternion.identity);
            NetworkObject networkObject = gameObject.GetComponentInChildren<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);
            enemyType.numberSpawned++;
            RoundManager.Instance.SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
        }
    }

    public static EnemyType GetRandomEnemy(bool isOutside)
    {
        List<EnemyType> eligibleEnemies = GetEligibleEnemies(isOutside);
        return eligibleEnemies.Count > 0 ? eligibleEnemies[new System.Random().Next(eligibleEnemies.Count)] : null;
    }

    public static List<EnemyType> GetEligibleEnemies(bool isOutside)
    {
        List<EnemyType> eligibleEnemies = [];
        foreach (KeyValuePair<EnemyType, int> upsideDownEnemy in StrangerThings.upsideDownEnemies)
        {
            EnemyType enemyType = upsideDownEnemy.Key;
            EnemyAI enemy = enemyType.enemyPrefab.GetComponent<EnemyAI>();
            if (enemy != null && enemy.isOutside == isOutside && enemyType.numberSpawned < enemyType.MaxCount)
            {
                for (int i = 0; i < upsideDownEnemy.Value; i++)
                    eligibleEnemies.Add(enemyType);
            }
        }
        return eligibleEnemies;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
    [HarmonyPrefix]
    private static bool PlayerIsTargetable(ref EnemyAI __instance, ref bool __result, PlayerControllerB playerScript)
    {
        if (!DimensionRegistry.AreInSameDimension(__instance.gameObject, playerScript.gameObject)
            && (__instance is not DemogorgonKidnapperAI demogorgon || demogorgon.currentBehaviourStateIndex != (int)DemogorgonAI.State.WANDERING))
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
            UpsideDownPortal upsideDownPortal = MapObjectsManager.GetClosestPortal(__instance.transform.position);
            if (upsideDownPortal == null) return true;

            __instance.destination = upsideDownPortal.transform.position;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.TargetClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventTargetClosestPlayer(EnemyAI __instance, ref bool __result)
    {
        if (__result && __instance.targetPlayer != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __instance.targetPlayer.gameObject))
            __result = false;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CheckLineOfSightForClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventCheckLineOfSightForClosestPlayer(EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CheckLineOfSightForPlayer))]
    [HarmonyPostfix]
    private static void PreventCheckLineOfSightForPlayer(EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.GetClosestPlayer))]
    [HarmonyPostfix]
    private static void PreventGetClosestPlayer(EnemyAI __instance, ref PlayerControllerB __result)
    {
        if (__result != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __result.gameObject))
            __result = null;
    }
}
