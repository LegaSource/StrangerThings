using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Managers;

public class StrangerThingsNetworkManager : NetworkBehaviour
{
    public static StrangerThingsNetworkManager Instance;

    public void Awake() => Instance = this;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetPlayerInUpsideDownEveryoneRpc(int playerId)
    {
        GameObject playerObj = StartOfRound.Instance.allPlayerObjects[playerId];
        DimensionRegistry.SetUpsideDown(playerObj, !DimensionRegistry.IsInUpsideDown(playerObj));
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetGObjectInUpsideDownEveryoneRpc(NetworkObjectReference obj)
    {
        if (obj.TryGet(out NetworkObject networkObject))
        {
            DimensionRegistry.SetUpsideDown(networkObject.gameObject, !DimensionRegistry.IsInUpsideDown(networkObject.gameObject));

            if (LFCUtilities.IsServer && networkObject.GetComponentInChildren<GrabbableObject>() != null)
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
            upsideDownMirrorBehaviour.twinRenderers = twin.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
        }
    }
}
