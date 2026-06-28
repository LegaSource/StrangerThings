using GameNetcodeStuff;
using HarmonyLib;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Linq;
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
    public static void EndGame(StartOfRound __instance)
    {
        foreach (PlayerControllerB player in __instance.allPlayerScripts)
        {
            if (DimensionRegistry.IsInUpsideDown(player.gameObject))
                DimensionRegistry.SetInUpsideDown(player.gameObject, false);
        }

        UpsideDownAtmosphereController upsideDownAtmosphere = UpsideDownAtmosphereController.Instance;
        if (upsideDownAtmosphere != null)
        {
            foreach (GameObject deadTree in upsideDownAtmosphere.DeadTrees.ToList())
                if (deadTree != null) Object.Destroy(deadTree);
            Object.Destroy(upsideDownAtmosphere.BatsSky);

            upsideDownAtmosphere.AliveTrees.Clear();
            upsideDownAtmosphere.DeadTrees.Clear();
            upsideDownAtmosphere.BatsSky = null;
        }
        MapObjectsManager.GetUpsideDownPortals().Clear();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnDisable))]
    [HarmonyPostfix]
    public static void OnDisable() => StrangerThingsNetworkManager.Instance = null;
}
