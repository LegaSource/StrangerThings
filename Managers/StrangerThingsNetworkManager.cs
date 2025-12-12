using StrangerThings.Registries;
using Unity.Netcode;

namespace StrangerThings.Managers;

public class StrangerThingsNetworkManager : NetworkBehaviour
{
    public static StrangerThingsNetworkManager Instance;

    public void Awake() => Instance = this;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetGObjectInUpsideDownEveryoneRpc(NetworkObjectReference obj)
    {
        if (!obj.TryGet(out NetworkObject networkObject)) return;
        DimensionRegistry.SetUpsideDown(networkObject.gameObject, !DimensionRegistry.IsInUpsideDown(networkObject.gameObject));

        GrabbableObject grabbableObject = networkObject.GetComponentInChildren<GrabbableObject>();
        if (grabbableObject != null) HoarderBugAI.RefreshGrabbableObjectsInMapList();
    }
}
