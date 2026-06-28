using GameNetcodeStuff;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Items;
using Unity.Netcode;

namespace StrangerThings.Behaviours.MapObjects;

public class AntennaHazard : NetworkBehaviour
{
    public AntennaItem antennaItem;
    public PlayerControllerB previousPlayerHeldBy;

    public void AntennaInteraction() => RestoreAntennaItemEveryoneRpc((int)LFCUtilities.LocalPlayer.playerClientId);

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void RestoreAntennaItemEveryoneRpc(int playerId)
    {
        antennaItem.isBeingUsed = false;
        antennaItem.EnablePhysics(enable: true);
        if (LFCUtilities.IsServer)
        {
            LFCNetworkManager.Instance.ForceGrabObjectEveryoneRpc(antennaItem.GetComponent<NetworkObject>(), playerId);
            Destroy(gameObject);
        }
    }
}
