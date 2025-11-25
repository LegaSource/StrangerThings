using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Patches;

public class PlayerControllerBPatch
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPostfix]
    private static void StartPlayer(ref PlayerControllerB __instance)
    {
        if (LFCUtilities.ShouldBeLocalPlayer(__instance))
        {
            if (UpsideDownAtmosphereController.Instance == null)
                _ = Object.Instantiate(StrangerThings.upsideDownAtmosphere, __instance.gameplayCamera.transform);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPostfix]
    private static void KillPlayer(ref PlayerControllerB __instance)
    {
        if (LFCUtilities.ShouldBeLocalPlayer(__instance) && DimensionRegistry.IsInUpsideDown(__instance.gameObject))
            DimensionRegistry.SetUpsideDown(__instance.gameObject, false);
    }
}
