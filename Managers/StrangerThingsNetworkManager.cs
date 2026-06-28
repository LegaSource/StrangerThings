using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using LethalStatus.Managers;
using LethalStatus.StatusEffects;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Behaviours.Scripts.Projectiles;
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
    public void SpawnUpsideDownTreesEveryoneRpc(int seed)
    {
        UpsideDownAtmosphereController upsideDownAtmosphere = UpsideDownAtmosphereController.Instance;
        if (upsideDownAtmosphere != null)
        {
            System.Random random = new System.Random(seed);
            upsideDownAtmosphere.AliveTrees = LFCTreesRegistry.GetTrees().ToHashSet();

            IOrderedEnumerable<GameObject> sortedTrees = upsideDownAtmosphere.AliveTrees.OrderBy(t => t.transform.position.sqrMagnitude);
            foreach (GameObject aliveTree in sortedTrees)
            {
                GameObject[] treeObjs = [StrangerThings.Tree1Obj, StrangerThings.Tree2Obj, StrangerThings.Tree3Obj];
                GameObject treeObj = Instantiate(treeObjs[random.Next(0, treeObjs.Length)], aliveTree.transform.position, aliveTree.transform.rotation);
                treeObj.transform.localScale *= Mathf.Lerp(1f, 2.5f, (float)random.NextDouble());
                treeObj.SetActive(false);

                _ = upsideDownAtmosphere.DeadTrees.Add(treeObj);
                LFCTreesRegistry.AddTree(treeObj);
            }
        }
    }

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

        if (scriptNetworkObject.TryGetComponent(out UpsideDownMirrorBehaviour mirrorBehaviour))
        {
            GrabbableObject mirror = mirrorNetworkObject.GetComponentInChildren<GrabbableObject>();
            mirrorBehaviour.mirror = mirror;
            scriptNetworkObject.transform.SetParent(mirror.transform, worldPositionStays: true);

            GrabbableObject twin = twinNetworkObject.GetComponentInChildren<GrabbableObject>();
            mirrorBehaviour.twin = twin;
            mirrorBehaviour.twinRenderers = twin.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToList();
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
            Collider[] overlapBuffer = new Collider[64];
            int count = Physics.OverlapSphereNonAlloc(position, Mathf.Max(timePassed * 5f, 7.5f), overlapBuffer, StartOfRound.Instance.playersMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (overlapBuffer[i].TryGetComponent(out PlayerControllerB player) && !player.isPlayerDead && DimensionRegistry.IsInUpsideDown(player.gameObject))
                    LSNetworkManager.Instance.ApplyStatusEveryoneRpc(-1, (int)player.playerClientId, (int)LSStatusEffectRegistry.StatusEffectType.POISON, 10, 20);
            }

            yield return new WaitForSeconds(0.2f);
            timePassed += 0.2f;
        }
    }

    [Rpc(SendTo.NotServer, RequireOwnership = false)]
    public void SyncDoorPositionNotServerRpc(NetworkObjectReference doorObj, Vector3 position, Quaternion rotation, bool hasLanded)
    {
        if (doorObj.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out DoorProjectile doorProjectile))
            doorProjectile.SyncPosition(position, rotation, hasLanded);
    }
}
