using GameNetcodeStuff;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Managers;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using static LegaFusionCore.Registries.LFCShipFeatureRegistry;

namespace StrangerThings.Registries;

public class DimensionRegistry : MonoBehaviour
{
    public static HashSet<UpsideDownPortal> upsideDownPortals = [];
    private static readonly HashSet<GameObject> upsideDownEntities = [];
    private static readonly Dictionary<GameObject, EntityState> visibilityStates = [];

    public static void SpawnPortalsForServer()
    {
        if (!LFCUtilities.IsServer || upsideDownPortals.Count >= 4) return;

        const float minDistance = 50f;
        List<Vector3> selectedPositions = [];
        StartOfRound.Instance.allPlayerScripts.Where(p => !p.isPlayerDead).ToList().ForEach(p => selectedPositions.Add(p.transform.position));

        LFCUtilities.Shuffle(RoundManager.Instance.outsideAINodes);
        LFCUtilities.Shuffle(RoundManager.Instance.insideAINodes);

        // Garantir au moins un portail à l'intérieur et à l'extérieur
        List<bool> portalTypes = [true, false];
        while (portalTypes.Count < 10)
            portalTypes.Add(Random.value > 0.5f);

        foreach (bool isOutside in portalTypes)
        {
            float maxDistance = float.MinValue;
            Vector3 bestPosition = Vector3.zero;
            GameObject lastNodeSaved = null;

            List<GameObject> nodes = (isOutside ? RoundManager.Instance.outsideAINodes : RoundManager.Instance.insideAINodes).ToList();
            float radius = isOutside ? 10f : 2f;

            foreach (GameObject node in nodes)
            {
                Vector3 candidatePosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(node.transform.position, radius, default, new System.Random()) + Vector3.up;
                if (!Physics.Raycast(candidatePosition, Vector3.down, out RaycastHit hit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault)) continue;

                Vector3 validPosition = hit.point;

                // Calculer la distance minimale avec les positions sélectionnées
                float minDistanceToSelected = selectedPositions.Count > 0
                    ? selectedPositions.Min(p => Vector3.Distance(p, validPosition))
                    : float.MaxValue;

                // Garder la position la plus éloignée des autres sélectionnées
                if (minDistanceToSelected > minDistance || minDistanceToSelected > maxDistance)
                {
                    maxDistance = minDistanceToSelected;
                    bestPosition = validPosition;
                    lastNodeSaved = node;

                    if (minDistanceToSelected > minDistance) break;
                }
            }

            if (bestPosition != Vector3.zero)
            {
                selectedPositions.Add(bestPosition);
                _ = nodes.Remove(lastNodeSaved);

                SpawnUpsideDownPortalForServer(bestPosition, isOutside);
            }
        }
    }

    public static void SpawnUpsideDownPortalForServer(Vector3 position, bool isOutside)
    {
        GameObject gameObject = Instantiate(StrangerThings.upsideDownPortal, position + (Vector3.down * 0.1f), Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
        gameObject.GetComponent<NetworkObject>().Spawn(true);
        gameObject.GetComponent<UpsideDownPortal>().InitializeEveryoneRpc(isOutside);
    }

    private class EntityState
    {
        public HashSet<Renderer> disabledRenderers = [];
        public HashSet<Light> disabledLights = [];
        public HashSet<Collider> disabledColliders = [];
        public HashSet<TextMeshProUGUI> disabledTextMeshes = [];
        public HashSet<ScanNodeProperties> disabledScanNodes = [];
        public HashSet<InteractTrigger> disabledTriggers = [];
        public HashSet<ParticleSystem> disabledParticles = [];
        public Dictionary<AudioSource, float> audioVolumes = [];
    }

    public static void SetUpsideDown(GameObject entity, bool isInUpsideDown)
    {
        _ = isInUpsideDown ? upsideDownEntities.Add(entity) : upsideDownEntities.Remove(entity);

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
                UpdateVisibilityState(player.gameObject);
                UpdateFlickeringFlashlights(player);
            }

            StartOfRound.Instance.UpdatePlayerVoiceEffects();
            return;
        }

        UpdateVisibilityState(entity);
    }
    public static bool IsInUpsideDown(GameObject entity) => upsideDownEntities.Contains(entity);
    public static bool AreInSameDimension(GameObject a, GameObject b) => IsInUpsideDown(a) == IsInUpsideDown(b);

    public static void RefreshStatesForLocalClient()
    {
        foreach (NetworkBehaviour networkBehaviour in FindObjectsOfType<NetworkBehaviour>(true))
        {
            if (networkBehaviour.IsSpawned && IsWhitelisted(networkBehaviour.gameObject))
                UpdateVisibilityState(networkBehaviour.gameObject);
        }

        foreach (SandSpiderWebTrap webTrap in FindObjectsOfType<SandSpiderWebTrap>(true))
            UpdateVisibilityState(webTrap.gameObject);

        foreach (DeadBodyInfo deadBodyInfo in FindObjectsOfType<DeadBodyInfo>(true))
            UpdateVisibilityState(deadBodyInfo.gameObject);

        foreach (GrabbableObject grabbableObject in LFCSpawnRegistry.GetAllAs<GrabbableObject>())
        {
            if (GameNetworkManager.Instance.localPlayerController.ItemSlots.Contains(grabbableObject))
                StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(grabbableObject.GetComponent<NetworkObject>());
            else
                UpdateVisibilityState(grabbableObject.gameObject);
        }

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (LFCUtilities.ShouldNotBeLocalPlayer(player))
                UpdateVisibilityState(player.gameObject);
        }
    }

    private static void UpdateLightsVisibilityForLocalClient()
    {
        foreach (Animator poweredLight in RoundManager.Instance.allPoweredLightsAnimators)
        {
            if (IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject))
                LFCPoweredLightsRegistry.AddLock(poweredLight, StrangerThings.modName);
            else
                LFCPoweredLightsRegistry.RemoveLock(poweredLight, StrangerThings.modName);
        }
    }

    private static void UpdateShipFeaturesForLocalClient()
    {
        if (IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject))
        {
            AddLock(ShipFeatureType.SHIP_LIGHTS, StrangerThings.modName);
            AddLock(ShipFeatureType.MAP_SCREEN, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_DOORS, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_LEVER, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TERMINAL, StrangerThings.modName);
            AddLock(ShipFeatureType.ITEM_CHARGER, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TV, StrangerThings.modName);
            AddLock(ShipFeatureType.SHIP_TELEPORTERS, StrangerThings.modName);
            return;
        }
        ClearLocks(StrangerThings.modName);
    }

    public static void UpdateVisibilityState(GameObject entity)
    {
        if (AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, entity))
            Restore(entity);
        else
            Hide(entity);
    }

    private static void Hide(GameObject entity)
    {
        if (!visibilityStates.TryGetValue(entity, out EntityState state))
        {
            state = new EntityState();
            visibilityStates[entity] = state;
        }

        foreach (Renderer renderer in entity.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
            {
                renderer.enabled = false;
                _ = state.disabledRenderers.Add(renderer);
            }
        }
        foreach (Light light in entity.GetComponentsInChildren<Light>(true))
        {
            if (light.enabled)
            {
                light.enabled = false;
                _ = state.disabledLights.Add(light);
            }
        }
        foreach (Collider collider in entity.GetComponentsInChildren<Collider>(true))
        {
            if (collider.enabled)
            {
                collider.enabled = false;
                _ = state.disabledColliders.Add(collider);
            }
        }
        foreach (TextMeshProUGUI textMesh in entity.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (textMesh.enabled)
            {
                textMesh.enabled = false;
                _ = state.disabledTextMeshes.Add(textMesh);
            }
        }
        foreach (ScanNodeProperties scanNode in entity.GetComponentsInChildren<ScanNodeProperties>(true))
        {
            if (scanNode.enabled && scanNode.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
                _ = state.disabledScanNodes.Add(scanNode);
            }
        }
        foreach (InteractTrigger interactTrigger in entity.GetComponentsInChildren<InteractTrigger>(true))
        {
            if (interactTrigger.enabled && interactTrigger.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
                _ = state.disabledTriggers.Add(interactTrigger);
            }
        }
        foreach (ParticleSystem particle in entity.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particle.isPlaying)
            {
                particle.Stop();
                _ = state.disabledParticles.Add(particle);
            }
        }
        foreach (AudioSource audioSource in entity.GetComponentsInChildren<AudioSource>(true))
        {
            if (audioSource.volume > 0f)
            {
                state.audioVolumes[audioSource] = audioSource.volume;
                audioSource.volume = 0f;
            }
        }
    }

    private static void Restore(GameObject entity)
    {
        if (!visibilityStates.TryGetValue(entity, out EntityState state)) return;

        foreach (Renderer renderer in state.disabledRenderers)
            if (renderer != null) renderer.enabled = true;
        foreach (Light light in state.disabledLights)
            if (light != null) light.enabled = true;
        foreach (Collider collider in state.disabledColliders)
            if (collider != null) collider.enabled = true;
        foreach (TextMeshProUGUI textMesh in state.disabledTextMeshes)
            if (textMesh != null) textMesh.enabled = true;
        foreach (ScanNodeProperties scanNode in state.disabledScanNodes)
            if (scanNode != null && scanNode.TryGetComponent(out Collider collider)) collider.enabled = true;
        foreach (InteractTrigger interactTrigger in state.disabledTriggers)
            if (interactTrigger != null && interactTrigger.TryGetComponent(out Collider collider)) collider.enabled = true;
        foreach (ParticleSystem particle in state.disabledParticles)
            particle?.Play();
        foreach (KeyValuePair<AudioSource, float> kv in state.audioVolumes)
            if (kv.Key) kv.Key.volume = kv.Value;

        _ = visibilityStates.Remove(entity);
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
        => gObject != null && (gObject.TryGetComponent<EnemyAI>(out _) || ConfigManager.visibilityStateInclusions.Value.Contains(gObject.name));

    public static UpsideDownPortal GetClosestPortal(Vector3 position)
    {
        if (upsideDownPortals == null || upsideDownPortals.Count == 0) return null;

        UpsideDownPortal closest = null;
        float closestDistance = float.MaxValue;

        foreach (UpsideDownPortal portal in upsideDownPortals)
        {
            if (portal == null || portal.transform == null) continue;

            float distance = Vector3.SqrMagnitude(portal.transform.position - position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = portal;
            }
        }

        return closest;
    }
}