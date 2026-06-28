using GameNetcodeStuff;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.MapObjects;

public class BatsHorde : NetworkBehaviour
{
    public ParticleSystem BatsParticle;
    public AudioSource AudioBatsHorde;
    public AudioClip BatsSound;

    public bool isActive = true;
    public float batsTimer = 0f;
    public float batsCooldown = 60f;

    public void Start() => DimensionRegistry.SetInUpsideDown(gameObject, true);

    private void OnTriggerEnter(Collider collider)
    {
        if (isActive && collider != null && collider.TryGetComponent(out PlayerControllerB player))
        {
            isActive = false;
            if (BatsParticle.isPlaying)
                BatsParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            AudioBatsHorde.PlayOneShot(BatsSound);
            if (LFCUtilities.ShouldBeLocalPlayer(player))
                player.JumpToFearLevel(0.9f);
        }
    }

    public void Update()
        => LFCUtilities.UpdateTimer(ref batsTimer, batsCooldown, !isActive, () =>
        {
            isActive = true;
            if (!BatsParticle.isPlaying) BatsParticle.Play();
        });
}
