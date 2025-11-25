using GameNetcodeStuff;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.MapObjects;

public class UpsideDownPortal : NetworkBehaviour
{
    public bool isOutside;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void InitializeEveryoneRpc(bool isOutside)
    {
        _ = DimensionRegistry.upsideDownPortals.Add(this);
        this.isOutside = isOutside;
    }

    public void PortalInteraction() => StrangerThingsNetworkManager.Instance.SetPlayerInUpsideDownEveryoneRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);

    private void OnTriggerEnter(Collider other)
    {
        if (LFCUtilities.IsServer && other.CompareTag("Player"))
        {
            StrangerThings.mls.LogError("OnTriggerEnter : " + other.GetComponent<PlayerControllerB>().playerUsername);
            DemogorgonAI demogorgon = LFCSpawnRegistry.GetAllAs<EnemyAI>()
                .OfType<DemogorgonAI>()
                .FirstOrDefault(d => d.canSet && d.currentBehaviourStateIndex == (int)DemogorgonAI.State.WANDERING);
            demogorgon?.SetEveryoneRpc((int)other.GetComponent<PlayerControllerB>().playerClientId);
        }
    }
}