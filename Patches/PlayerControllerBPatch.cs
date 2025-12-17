using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StrangerThings.Patches;

public class PlayerControllerBPatch
{
    private static bool canFlick = false;
    private static float flickerTimer = 0f;
    private static readonly float flickerCooldown = 0.5f;

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

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
    [HarmonyPostfix]
    private static void UpdatePlayer(ref PlayerControllerB __instance)
    {
        if (LFCUtilities.ShouldBeLocalPlayer(__instance))
        {
            LFCUtilities.UpdateTimer(ref flickerTimer, flickerCooldown, !canFlick, () => canFlick = true);
            return;
        }
        if (!canFlick || !DimensionRegistry.IsInUpsideDown(__instance.gameObject)) return;

        canFlick = false;
        Animator bestPoweredLight = null;
        float bestDistance = float.MaxValue;
        foreach (Animator poweredLight in RoundManager.Instance?.allPoweredLightsAnimators)
        {
            float distance = (poweredLight.transform.position - __instance.transform.position).sqrMagnitude;
            if (!LFCPoweredLightsRegistry.IsLocked(poweredLight) && distance <= 50f)
            {
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPoweredLight = poweredLight;
                }
            }
        }
        bestPoweredLight?.SetTrigger("Flicker");

        HashSet<Component> flashlights = LFCSpawnRegistry.GetSetExact<FlashlightItem>();
        if (flashlights == null) return;

        foreach (FlashlightItem flashlight in flashlights.Cast<FlashlightItem>())
        {
            if (!DimensionRegistry.IsInUpsideDown(flashlight.gameObject) && (flashlight.transform.position - __instance.transform.position).sqrMagnitude <= 25f)
                LFCObjectStateRegistry.AddFlickeringFlashlight(flashlight, $"{StrangerThings.modName}{__instance.playerUsername}");
            else
                LFCObjectStateRegistry.RemoveFlickeringFlashlight(flashlight, $"{StrangerThings.modName}{__instance.playerUsername}");
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPostfix]
    private static void KillPlayer(ref PlayerControllerB __instance)
    {
        if (DimensionRegistry.IsInUpsideDown(__instance.gameObject))
            DimensionRegistry.SetUpsideDown(__instance.gameObject, false);
    }
}
