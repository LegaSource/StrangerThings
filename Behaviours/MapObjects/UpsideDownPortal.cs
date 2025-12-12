using GameNetcodeStuff;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Registries;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.MapObjects;

public class UpsideDownPortal : NetworkBehaviour
{
    public bool isOutside;
    public bool isLocked = false;
    public float lockTimer = 0f;
    public float lockDuration = 60f;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void InitializeEveryoneRpc(bool isOutside)
    {
        _ = DimensionRegistry.upsideDownPortals.Add(this);
        this.isOutside = isOutside;
    }

    public void PortalInteraction()
    {
        if (isLocked)
        {
            HUDManager.Instance.DisplayTip("Impossible action", "The portal seems to be blocked for now...");
            return;
        }
        SetPlayerInUpsideDownEveryoneRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetPlayerInUpsideDownEveryoneRpc(int playerId)
    {
        isLocked = true;
        GameObject playerObj = StartOfRound.Instance.allPlayerObjects[playerId];
        DimensionRegistry.SetUpsideDown(playerObj, !DimensionRegistry.IsInUpsideDown(playerObj));
    }

    public void Update()
    {
        if (isLocked)
        {
            lockTimer += Time.deltaTime;
            if (lockTimer >= lockDuration)
            {
                isLocked = false;
                lockTimer = 0f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (LFCUtilities.IsServer && other.CompareTag("Player"))
        {
            DemogorgonAI demogorgon = LFCSpawnRegistry.GetAllAs<EnemyAI>()
                .OfType<DemogorgonAI>()
                .FirstOrDefault(d => d.canSet && d.currentBehaviourStateIndex == (int)DemogorgonAI.State.WANDERING);
            if (demogorgon != null)
            {
                demogorgon.isHunting = true;
                demogorgon.setCoroutine ??= demogorgon.StartCoroutine(demogorgon.SetCoroutine(other.GetComponent<PlayerControllerB>()));
            }
        }
    }
}