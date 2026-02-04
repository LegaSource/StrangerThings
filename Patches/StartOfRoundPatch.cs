using GameNetcodeStuff;
using HarmonyLib;
using StrangerThings.Managers;
using StrangerThings.Registries;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class StartOfRoundPatch
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void StartRound(StartOfRound __instance)
    {
        if (NetworkManager.Singleton.IsHost && StrangerThingsNetworkManager.Instance == null)
        {
            GameObject gameObject = Object.Instantiate(StrangerThings.managerPrefab, __instance.transform.parent);
            gameObject.GetComponent<NetworkObject>().Spawn();
            StrangerThings.mls.LogInfo("Spawning StrangerThingsNetworkManager");
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipLeave))]
    [HarmonyPostfix]
    public static void EndRound(StartOfRound __instance)
    {
        foreach (PlayerControllerB player in __instance.allPlayerScripts)
        {
            if (DimensionRegistry.IsInUpsideDown(player.gameObject))
                DimensionRegistry.SetInUpsideDown(player.gameObject, false);
        }
        MapObjectsManager.upsideDownPortals.Clear();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnDisable))]
    [HarmonyPostfix]
    public static void OnDisable() => StrangerThingsNetworkManager.Instance = null;
}
