using GameNetcodeStuff;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;
using static LegaFusionCore.Registries.LFCShipFeatureRegistry;

namespace StrangerThings.Registries;

public class DimensionRegistry : MonoBehaviour
{
    private static readonly HashSet<GameObject> upsideDownEntities = [];

    public static GameObject GetUpsideDownEntity(GameObject entity)
    {
        _ = upsideDownEntities.RemoveWhere(e => e == null);
        return upsideDownEntities.FirstOrDefault(e => e == entity);
    }

    public static bool CanSetInUpsideDown(GameObject entity, bool isInUpsideDown)
    {
        PlayerControllerB player = LFCUtilities.GetSafeComponent<PlayerControllerB>(entity);
        if (player != null && !player.isPlayerDead && !isInUpsideDown)
        {
            // Checks pour savoir si le joueur peut quitter l'Upside Down
            foreach (UpsideDownPortal upsideDownPortal in MapObjectsManager.GetUpsideDownPortals())
            {
                if (upsideDownPortal.corruptedPlayer == player)
                {
                    if (LFCUtilities.ShouldBeLocalPlayer(player))
                        HUDManager.Instance.DisplayTip("Impossible action", "You have been corrupted. You must find another way out.");
                    return false;
                }
            }
        }
        return isInUpsideDown ? upsideDownEntities.Add(entity) : upsideDownEntities.Remove(entity);
    }

    public static void SetInUpsideDown(GameObject entity, bool isInUpsideDown)
    {
        if (LFCUtilities.LocalPlayer == null || entity == null || !CanSetInUpsideDown(entity, isInUpsideDown))
            return;

        PlayerControllerB player = LFCUtilities.GetSafeComponent<PlayerControllerB>(entity);
        if (player != null)
        {
            if (LFCUtilities.ShouldBeLocalPlayer(player))
            {
                RefreshStatesForLocalClient();
                UpdateLightsVisibilityForLocalClient();
                UpdateShipFeaturesForLocalClient();
                UpdateFlickeringFlashlights(player);
                UpsideDownAtmosphereController.Instance.SetUpsideDownState(isInUpsideDown);
            }
            else
            {
                if (LFCUtilities.LocalPlayer.isPlayerDead && LFCUtilities.LocalPlayer.spectatedPlayerScript == player)
                {
                    SetInUpsideDown(LFCUtilities.LocalPlayer.gameObject, isInUpsideDown);
                    return;
                }
                UpdateVisibilityState(player.gameObject);
                UpdateFlickeringFlashlights(player);
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
            return;
        }

        UpdateVisibilityState(entity);
    }
    public static bool IsInUpsideDown(GameObject entity) => entity != null && GetUpsideDownEntity(entity);
    public static bool AreInSameDimension(GameObject a, GameObject b)
        => a == null || b == null || IsBlacklisted(a) || IsBlacklisted(b) || IsInUpsideDown(a) == IsInUpsideDown(b);

    public static void RefreshStatesForLocalClient()
    {
        foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>(true))
        {
            if (networkBehaviour.IsSpawned && IsWhitelisted(networkBehaviour.gameObject))
                UpdateVisibilityState(networkBehaviour.gameObject);
        }
        foreach (GrabbableObject grabbableObject in LFCSpawnRegistry.GetAllAs<GrabbableObject>())
        {
            if (LFCUtilities.LocalPlayer.ItemSlots.Contains(grabbableObject))
                StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(grabbableObject.GetComponent<NetworkObject>(), IsInUpsideDown(LFCUtilities.LocalPlayer.gameObject));
            else
                UpdateVisibilityState(grabbableObject.gameObject);
        }
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (LFCUtilities.ShouldNotBeLocalPlayer(player))
                UpdateVisibilityState(player.gameObject);
        }
        foreach (SandSpiderWebTrap webTrap in FindObjectsOfType<SandSpiderWebTrap>(true))
            UpdateVisibilityState(webTrap.gameObject);
        foreach (DeadBodyInfo deadBodyInfo in FindObjectsOfType<DeadBodyInfo>(true))
            UpdateVisibilityState(deadBodyInfo.gameObject);
    }

    public static void UpdateVisibilityState(GameObject entity)
    {
        if (AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, entity))
            LFCVisibilityRegistry.Restore(entity, $"{StrangerThings.modName}Dimension");
        else
            LFCVisibilityRegistry.Hide(entity, $"{StrangerThings.modName}Dimension");
    }

    private static void UpdateLightsVisibilityForLocalClient()
    {
        foreach (Animator poweredLight in RoundManager.Instance.allPoweredLightsAnimators)
        {
            if (IsInUpsideDown(LFCUtilities.LocalPlayer.gameObject))
                LFCPoweredLightsRegistry.AddLock(poweredLight, StrangerThings.modName);
            else
                LFCPoweredLightsRegistry.RemoveLock(poweredLight, StrangerThings.modName);
        }
    }

    private static void UpdateShipFeaturesForLocalClient()
    {
        if (IsInUpsideDown(LFCUtilities.LocalPlayer.gameObject))
        {
            AddLock(ShipFeatureType.SHIP_LIGHTS, StrangerThings.modName);
            AddLock(ShipFeatureType.MAP_SCREEN, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_DOORS, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_LEVER, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TERMINAL, StrangerThings.modName);
            AddLock(ShipFeatureType.ITEM_CHARGER, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TV, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TELEPORTERS, StrangerThings.modName);
            AddLock(ShipFeatureType.SMART_CUPBOARD, StrangerThings.modName);
            return;
        }
        ClearLocks(StrangerThings.modName);
    }

    private static void UpdateFlickeringFlashlights(PlayerControllerB player)
    {
        if (!IsInUpsideDown(player.gameObject))
        {
            HashSet<Component> flashlights = LFCSpawnRegistry.GetSetExact<FlashlightItem>();
            if (flashlights != null)
            {
                foreach (FlashlightItem flashlight in flashlights.Cast<FlashlightItem>())
                    LFCObjectStateRegistry.RemoveFlickeringFlashlight(flashlight, $"{StrangerThings.modName}{player.playerUsername}");
            }
        }
    }

    public static bool IsWhitelisted(GameObject gObject)
        => gObject != null
            && !IsBlacklisted(gObject)
            && (gObject.TryGetComponent<EnemyAI>(out _)
                || gObject.TryGetComponent<VehicleController>(out _)
                || gObject.TryGetComponent<RockProjectile>(out _)
                || gObject.TryGetComponent<AntennaHazard>(out _)
                || LFCUtilities.HasNameFromList(LFCUtilities.GetGameObjectName(gObject), ConfigManager.visibilityStateInclusions.Value));
    public static bool IsBlacklisted(GameObject gObject)
        => gObject != null && LFCUtilities.HasNameFromList(LFCUtilities.GetGameObjectName(gObject), ConfigManager.visibilityStateExclusions.Value);
}