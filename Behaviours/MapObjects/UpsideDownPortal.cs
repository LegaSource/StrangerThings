using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.MapObjects;

public class UpsideDownPortal : NetworkBehaviour, IHittable
{
    public InteractTrigger PortalTrigger;
    public ParticleSystem PortalSpores;

    public bool isOutside;
    public bool isFake;
    public bool isLocked = false;
    public bool isCorrupted = false;

    public float lockTimer = 0f;

    public PlayerControllerB corruptedPlayer;
    public Coroutine disableCoroutine;

    private readonly Color baseColor = new Color(0.2f, 0.48f, 0.84f);
    private readonly Color corruptedColor = new Color(1f, 0f, 0f);

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void InitializeEveryoneRpc(bool isOutside, bool isFake)
    {
        this.isOutside = isOutside;
        this.isFake = isFake;
        PortalTrigger.gameObject.SetActive(!isFake);
        if (!isFake)
            MapObjectsManager.AddPortal(this);
    }

    public void PortalInteraction()
    {
        PlayerControllerB player = LFCUtilities.LocalPlayer;
        if (isCorrupted)
        {
            if (!DimensionRegistry.IsInUpsideDown(player?.gameObject) || player == corruptedPlayer)
                RestorePortalEveryoneRpc();
            else
                HUDManager.Instance.DisplayTip("Impossible action", "The portal is corrupted. It needs to be restored.");
            return;
        }
        if (isLocked)
        {
            HUDManager.Instance.DisplayTip("Impossible action", "The portal seems to be blocked for now...");
            return;
        }
        if (player != null)
        {
            isLocked = true;
            StrangerThingsNetworkManager.Instance.SetPlayerInUpsideDownEveryoneRpc((int)player.playerClientId, !DimensionRegistry.IsInUpsideDown(player.gameObject));
        }
    }

    public void CorruptPortalForServer(PlayerControllerB player)
    {
        isCorrupted = true;
        corruptedPlayer = player;

        CorruptPortalEveryoneRpc();
        CorruptPlayerEveryoneRpc(corruptedPlayer != null ? (int)corruptedPlayer.playerClientId : -1);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void CorruptPortalEveryoneRpc()
    {
        isCorrupted = true;

        ParticleSystem.MainModule main = PortalSpores.main;
        main.startColor = corruptedColor;
        ParticleSystem.EmissionModule emission = PortalSpores.emission;
        emission.rateOverTime = 7f;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void CorruptPlayerEveryoneRpc(int playerId)
    {
        if (playerId != -1)
        {
            corruptedPlayer = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
            DimensionRegistry.SetInUpsideDown(corruptedPlayer.gameObject, true);
            if (LFCUtilities.ShouldBeLocalPlayer(corruptedPlayer))
            {
                if (ConfigManager.globalTips.Value)
                    HUDManager.Instance.DisplayTip("Information", "A portal has been corrupted. You must find a way to restore it in order to escape the dimension.");
                LFCCustomPassManager.SetupAuraForObjects([gameObject], LegaFusionCore.LegaFusionCore.wallhackShader, $"{StrangerThings.modName}Portal", Color.red);
            }
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void RestorePortalEveryoneRpc()
    {
        if (LFCUtilities.ShouldBeLocalPlayer(corruptedPlayer))
            LFCCustomPassManager.RemoveAuraByTag($"{StrangerThings.modName}Portal");

        isCorrupted = false;
        corruptedPlayer = null;

        ParticleSystem.MainModule main = PortalSpores.main;
        main.startColor = baseColor;
        ParticleSystem.EmissionModule emission = PortalSpores.emission;
        emission.rateOverTime = 4f;
    }

    public void Update()
    {
        if (isLocked)
        {
            lockTimer += Time.deltaTime;
            if (lockTimer >= ConfigManager.portalLockDuration.Value)
            {
                isLocked = false;
                lockTimer = 0f;
            }
        }
        if (LFCUtilities.IsServer && isCorrupted && corruptedPlayer != null && corruptedPlayer.isPlayerDead)
            RestorePortalEveryoneRpc();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFake && LFCUtilities.IsServer && other.CompareTag("Player"))
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (isCorrupted)
            {
                if (corruptedPlayer == null && !DimensionRegistry.IsInUpsideDown(player.gameObject))
                {
                    corruptedPlayer = player;
                    CorruptPlayerEveryoneRpc((int)player.playerClientId);
                }
                return;
            }

            DemogorgonHunterAI demogorgon = LFCSpawnRegistry.GetSetExact<DemogorgonHunterAI>()?
                .Cast<DemogorgonHunterAI>()?
                .FirstOrDefault(d => d.canSet
                    && Vector3.Distance(d.transform.position, player.transform.position) > 50f
                    && d.currentBehaviourStateIndex == (int)DemogorgonAI.State.WANDERING);
            if (demogorgon != null)
            {
                demogorgon.canSet = false;
                demogorgon.isHunting = true;
                demogorgon.setCoroutine ??= demogorgon.StartCoroutine(demogorgon.SetCoroutine(player));
            }
        }
    }

    public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (!isFake)
        {
            disableCoroutine ??= StartCoroutine(DisableCoroutine());
            return true;
        }
        return false;
    }

    public IEnumerator DisableCoroutine()
    {
        PortalTrigger.gameObject.SetActive(false);
        yield return new WaitForSeconds(5f);

        PortalTrigger.gameObject.SetActive(true);
        disableCoroutine = null;
    }
}