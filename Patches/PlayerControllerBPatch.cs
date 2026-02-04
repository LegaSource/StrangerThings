using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StrangerThings.Patches;

public class PlayerControllerBPatch
{
    private static bool canFlick = false;
    private static float flickerTimer = 0f;
    private static readonly float flickerCooldown = 0.5f;

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPostfix]
    private static void StartPlayer(PlayerControllerB __instance)
    {
        if (LFCUtilities.ShouldBeLocalPlayer(__instance))
        {
            if (UpsideDownAtmosphereController.Instance == null)
                _ = Object.Instantiate(StrangerThings.upsideDownAtmosphere, __instance.gameplayCamera.transform);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
    [HarmonyPrefix]
    private static void PreUpdatePlayer(PlayerControllerB __instance, bool ___isCameraDisabled, bool ___isPlayerControlled, bool ___isHostPlayerObject, bool ___isTestingPlayer)
    {
        if (___isCameraDisabled && ((__instance.IsOwner && ___isPlayerControlled && (!__instance.IsServer || ___isHostPlayerObject)) || ___isTestingPlayer) && DimensionRegistry.IsInUpsideDown(__instance.gameObject))
            DimensionRegistry.SetInUpsideDown(LFCUtilities.LocalPlayer.gameObject, false);
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
    [HarmonyPostfix]
    private static void PostUpdatePlayer(PlayerControllerB __instance)
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

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SetHoverTipAndCurrentInteractTrigger))]
    [HarmonyPrefix]
    private static bool SetHoverTipMirrorFusion(PlayerControllerB __instance)
        => !DimensionRegistry.IsInUpsideDown(__instance.gameObject) || string.IsNullOrEmpty(__instance.cursorTip.text) || !__instance.cursorTip.text.Equals(Constants.MIRROR_FUSION);

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Interact_performed))]
    [HarmonyPrefix]
    private static bool MirrorFusionActivate(PlayerControllerB __instance, ref InputAction.CallbackContext context)
    {
        if (!context.performed
            || !LFCUtilities.ShouldBeLocalPlayer(__instance)
            || !DimensionRegistry.IsInUpsideDown(__instance.gameObject)
            || __instance.isPlayerDead
            || !__instance.isPlayerControlled
            || __instance.isGrabbingObjectAnimation
            || __instance.inSpecialMenu
            || __instance.quickMenuManager.isMenuOpen)
        {
            return true;
        }

        GrabbableObject grabbableObject = __instance.currentlyHeldObjectServer;
        if (grabbableObject != null && grabbableObject.gameObject.TryGetComponentInChildren(out UpsideDownMirrorBehaviour behaviour) && behaviour.canFusion)
        {
            behaviour.CompleteFusionServerRpc();
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc))]
    [HarmonyPostfix]
    private static void KillPlayerForClients(PlayerControllerB __instance)
    {
        if (LFCUtilities.LocalPlayer != __instance && DimensionRegistry.IsInUpsideDown(__instance.gameObject))
            DimensionRegistry.SetInUpsideDown(__instance.gameObject, false);
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SpectateNextPlayer))]
    [HarmonyPostfix]
    private static void SwitchSpectatedPlayer(PlayerControllerB __instance)
    {
        if (__instance.spectatedPlayerScript != null && !DimensionRegistry.AreInSameDimension(__instance.spectatedPlayerScript.gameObject, __instance.gameObject))
            DimensionRegistry.SetInUpsideDown(__instance.gameObject, DimensionRegistry.IsInUpsideDown(__instance.spectatedPlayerScript.gameObject));
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ResetZAndXRotation))]
    [HarmonyPostfix]
    private static void ResetZAndXRotation(PlayerControllerB __instance)
    {
        if (LFCUtilities.LocalPlayer != __instance && !DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject))
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
