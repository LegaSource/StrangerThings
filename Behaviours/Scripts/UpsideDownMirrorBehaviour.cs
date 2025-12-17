using GameNetcodeStuff;
using LegaFusionCore.Behaviours.Shaders;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Patches;
using StrangerThings.Registries;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts;

public class UpsideDownMirrorBehaviour : NetworkBehaviour
{
    public GrabbableObject mirror;
    public GrabbableObject twin;
    public List<Renderer> twinRenderers = [];

    public bool canFusion = false;
    public int valueMultiplier = 3;

    public ParticleSystem heldParticles;
    public float fxTickInterval = 0.1f;
    private float fxTick;

    private void Update()
    {
        if (heldParticles == null || twin == null || mirror == null) return;

        fxTick -= Time.deltaTime;
        if (fxTick <= 0f)
        {
            fxTick = fxTickInterval;
            UpdateHeldFxLayeredColor(mirror.playerHeldBy);
        }
        ShowAuraTwinObject(mirror.playerHeldBy);
    }

    private void UpdateHeldFxLayeredColor(PlayerControllerB player)
    {
        if (!mirror.isHeld || mirror.isPocketed || !LFCUtilities.ShouldBeLocalPlayer(player) || DimensionRegistry.AreInSameDimension(mirror.gameObject, twin.gameObject))
        {
            if (heldParticles != null && heldParticles.isPlaying)
                heldParticles.Stop();
            return;
        }

        if (!heldParticles.isPlaying)
            heldParticles.Play();

        float distance = Vector3.Distance(mirror.transform.position, twin.transform.position);
        GetLayerValues(distance, out Color color, out float layerMin, out float layerMax);
        float proximityFactor = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(layerMax, layerMin, distance)), 2f);

        ParticleSystem.MainModule main = heldParticles.main;
        main.startColor = color;
        main.startLifetime = Mathf.Lerp(2f, 1f, proximityFactor);
        main.startSize = Mathf.Lerp(0.1f, 1f, proximityFactor);
        main.startSpeed = Mathf.Lerp(0.02f, 0.1f, proximityFactor);

        ParticleSystem.EmissionModule emission = heldParticles.emission;
        emission.rateOverTime = Mathf.Lerp(2f, 10f, proximityFactor);
    }

    private void GetLayerValues(float distance, out Color color, out float layerMin, out float layerMax)
    {
        if (distance > 60f)
        {
            color = new Color(0.3f, 0.6f, 1f);
            layerMin = 60f;
            layerMax = 100f;
        }
        else if (distance > 25f)
        {
            color = new Color(0.7f, 0.3f, 1f);
            layerMin = 25f;
            layerMax = 60f;
        }
        else
        {
            color = new Color(1f, 0f, 0f);
            layerMin = 0f;
            layerMax = 25f;
        }
    }

    public void ShowAuraTwinObject(PlayerControllerB player)
    {
        if (!LFCUtilities.ShouldBeLocalPlayer(player) || !DimensionRegistry.IsInUpsideDown(player.gameObject)) return;
        if (!mirror.isHeld
            || mirror.isPocketed
            || !twin.isHeld
            || twin.isPocketed
            || !LFCUtilities.ShouldBeLocalPlayer(player)
            || DimensionRegistry.AreInSameDimension(mirror.gameObject, twin.gameObject)
            || !player.HasLineOfSightToPosition(twin.transform.position, 20f, 3, 1f))
        {
            RemoveAuraTwinObject();
            return;
        }

        canFusion = true;
        twinRenderers?.ForEach(r => r.enabled = true);
        _ = StartOfRoundPatch.auraBypass.Add(twin.gameObject);
        CustomPassManager.SetupAuraForObjects([twin.gameObject], LegaFusionCore.LegaFusionCore.transparentShader, $"{StrangerThings.modName}TwinObject", Color.yellow);
    }

    public void RemoveAuraTwinObject()
    {
        if (DimensionRegistry.IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject))
        {
            canFusion = false;
            twinRenderers?.ForEach(r => r.enabled = false);
            _ = StartOfRoundPatch.auraBypass.Remove(twin.gameObject);
            CustomPassManager.RemoveAuraByTag($"{StrangerThings.modName}TwinObject");
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void CompleteFusionServerRpc()
    {
        LFCNetworkManager.Instance.SetScrapValueEveryoneRpc(twin.GetComponent<NetworkObject>(), twin.scrapValue * valueMultiplier);
        LFCNetworkManager.Instance.DestroyObjectEveryoneRpc(mirror.GetComponent<NetworkObject>());
        if (twin.playerHeldBy != null)
            LFCNetworkManager.Instance.ForceDiscardObjectEveryoneRpc(twin.GetComponent<NetworkObject>(), (int)twin.playerHeldBy.playerClientId);
        Destroy(gameObject);
    }

    public override void OnDestroy()
    {
        RemoveAuraTwinObject();
        base.OnDestroy();
    }
}