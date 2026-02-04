using HarmonyLib;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Items;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class RoundManagerPatch
{
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.waitForScrapToSpawnToSync))]
    [HarmonyPostfix]
    private static IEnumerator SpawnMirrorObjects(IEnumerator result)
    {
        while (result.MoveNext()) yield return result.Current;

        int maxSpawn = Random.Range(ConfigManager.minMirrorScraps.Value, ConfigManager.maxMirrorScraps.Value + 1);
        int nbSpawn = 0;
        foreach (GrabbableObject grabbableObject in LFCSpawnRegistry.GetAllAs<GrabbableObject>())
        {
            if (string.IsNullOrEmpty(grabbableObject.itemProperties?.itemName)) continue;
            if (!(string.IsNullOrEmpty(ConfigManager.scrapExclusions.Value) || !ConfigManager.scrapExclusions.Value.Contains(grabbableObject.itemProperties.itemName))) continue;
            if (!grabbableObject.isInFactory || grabbableObject.isInShipRoom || grabbableObject.scrapValue <= 0) continue;

            GrabbableObject upsideDownObject = SpawnUpsideDownObject(grabbableObject.itemProperties);
            if (upsideDownObject != null)
            {
                GameObject gameObject = Object.Instantiate(StrangerThings.upsideDownMirrorObject, upsideDownObject.transform.position, Quaternion.identity);
                NetworkObject networkObject = gameObject.GetComponent<NetworkObject>();
                networkObject.Spawn();
                StrangerThingsNetworkManager.Instance.AddToMirrorEveryoneRpc(networkObject, upsideDownObject.GetComponent<NetworkObject>(), grabbableObject.GetComponent<NetworkObject>());
            }
            if (maxSpawn <= nbSpawn++) break;
        }
    }

    public static GrabbableObject SpawnUpsideDownObject(Item itemToSpawn, int value = 0)
    {
        List<RandomScrapSpawn> listRandomScrapSpawn = Object.FindObjectsOfType<RandomScrapSpawn>().ToList();
        if (listRandomScrapSpawn.Any())
        {
            LFCUtilities.Shuffle(listRandomScrapSpawn);
            int indexRandomScrapSpawn = new System.Random().Next(0, listRandomScrapSpawn.Count);
            RandomScrapSpawn randomScrapSpawn = listRandomScrapSpawn[indexRandomScrapSpawn];
            RoundManager roundManager = RoundManager.Instance;
            randomScrapSpawn.transform.position = roundManager.GetRandomNavMeshPositionInBoxPredictable(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, roundManager.navHit, roundManager.AnomalyRandom) + (Vector3.up * itemToSpawn.verticalOffset);

            Vector3 position = randomScrapSpawn.transform.position + (Vector3.up * 0.5f);
            GrabbableObject grabbableObject = LFCObjectsManager.SpawnObjectForServer(itemToSpawn.spawnPrefab, position);
            if (grabbableObject is not UpsideDownObject)
                StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(grabbableObject.GetComponent<NetworkObject>(), true);
            LFCNetworkManager.Instance.SetScrapValueEveryoneRpc(grabbableObject.GetComponent<NetworkObject>(), value);

            return grabbableObject;
        }
        return null;
    }

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.ResetEnemyTypesSpawnedCounts))]
    [HarmonyPostfix]
    private static void ResetEnemyTypesSpawnedCounts(RoundManager __instance)
    {
        foreach (KeyValuePair<EnemyType, int> upsideDownEnemy in StrangerThings.upsideDownEnemies)
        {
            EnemyType enemyType = upsideDownEnemy.Key;
            enemyType.numberSpawned = 0;
            foreach (EnemyAI enemy in __instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType)
                    enemyType.numberSpawned++;
            }
        }
    }

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.PlayRandomClip))]
    [HarmonyPrefix]
    private static bool PlayRandomClip(AudioSource audioSource)
    {
        GameObject audioObj = audioSource?.gameObject;
        if (audioObj == null) return true;

        GameObject player = LFCUtilities.LocalPlayer?.gameObject;
        if (player == null) return true;

        if (audioObj.TryGetComponentInParent(out EnemyAI enemyAI))
            return DimensionRegistry.AreInSameDimension(enemyAI.gameObject, player);

        if (audioObj.TryGetComponentInParent(out GrabbableObject grabbableObject))
            return DimensionRegistry.AreInSameDimension(grabbableObject.gameObject, player);

        // Pas un ennemi / pas un objet
        return true;
    }
}