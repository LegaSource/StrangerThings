using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Managers;

public class StrangerThingsNetworkManager : NetworkBehaviour
{
    public static StrangerThingsNetworkManager Instance;

    public void Awake() => Instance = this;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetPlayerInUpsideDownEveryoneRpc(int playerId, bool isInUpsideDown)
    {
        GameObject playerObj = StartOfRound.Instance.allPlayerObjects[playerId];
        DimensionRegistry.SetInUpsideDown(playerObj, isInUpsideDown);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetGObjectInUpsideDownEveryoneRpc(NetworkObjectReference obj, bool isInUpsideDown)
    {
        if (obj.TryGet(out NetworkObject networkObject))
        {
            DimensionRegistry.SetInUpsideDown(networkObject.gameObject, isInUpsideDown);
            if (LFCUtilities.IsServer && networkObject.gameObject.TryGetComponentInChildren<GrabbableObject>(out _))
                HoarderBugAI.RefreshGrabbableObjectsInMapList();
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void AddToMirrorEveryoneRpc(NetworkObjectReference scriptObj, NetworkObjectReference mirrorObj, NetworkObjectReference twinObj)
    {
        if (!scriptObj.TryGet(out NetworkObject scriptNetworkObject) || !mirrorObj.TryGet(out NetworkObject mirrorNetworkObject) || !twinObj.TryGet(out NetworkObject twinNetworkObject))
            return;

        if (scriptNetworkObject.TryGetComponent(out UpsideDownMirrorBehaviour upsideDownMirrorBehaviour))
        {
            GrabbableObject mirror = mirrorNetworkObject.GetComponentInChildren<GrabbableObject>();
            upsideDownMirrorBehaviour.mirror = mirror;
            scriptNetworkObject.transform.SetParent(mirror.transform, worldPositionStays: true);

            GrabbableObject twin = twinNetworkObject.GetComponentInChildren<GrabbableObject>();
            upsideDownMirrorBehaviour.twin = twin;
            upsideDownMirrorBehaviour.twinRenderers = twin.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToList();
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayPoisonExplosionEveryoneRpc(Vector3 position)
    {
        if (LFCUtilities.IsServer)
            _ = StartCoroutine(PoisonCoroutine(position, duration: 4f));

        LFCGlobalManager.PlayParticle(tag: $"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.poisonExplosionParticle.name}",
                position: position,
                rotation: Quaternion.Euler(90f, 0f, 0f),
                scaleFactor: 4f,
                active: DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));

        LFCGlobalManager.PlayAudio(tag: $"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.poisonExplosionAudio.name}",
                position: position,
                active: DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));
    }

    public IEnumerator PoisonCoroutine(Vector3 position, float duration)
    {
        float timePassed = 0f;
        while (timePassed < duration)
        {
            foreach (Collider hitCollider in Physics.OverlapSphere(position, Mathf.Max(timePassed * 5f, 7.5f), StartOfRound.Instance.playersMask, QueryTriggerInteraction.Collide))
            {
                PlayerControllerB player = hitCollider.GetComponent<PlayerControllerB>();
                if (player == null || player.isPlayerDead || !DimensionRegistry.IsInUpsideDown(player.gameObject)) continue;

                LFCNetworkManager.Instance.ApplyStatusEveryoneRpc(-1, (int)player.playerClientId, (int)LFCStatusEffectRegistry.StatusEffectType.POISON, 10, 20);
            }

            yield return new WaitForSeconds(0.2f);
            timePassed += 0.2f;
        }
    }
}
