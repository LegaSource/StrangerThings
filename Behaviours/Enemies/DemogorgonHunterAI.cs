using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class DemogorgonHunterAI : DemogorgonAI
{
    public AudioClip ScreamSound;

    public float setTimer = 30f;
    public float huntTimer = 0f;

    public float setCooldown = 60f;
    public float huntDuration = 30f;

    public bool canSet = false;
    public bool isHunting = false;

    public Coroutine setCoroutine;

    protected override bool IgnoreLoseTargetConditions => isHunting;

    protected override void UpdateCooldowns()
    {
        base.UpdateCooldowns();

        LFCUtilities.UpdateTimer(ref setTimer, setCooldown, !canSet, () => canSet = true);
        LFCUtilities.UpdateTimer(ref huntTimer, huntDuration, isHunting, () => isHunting = false);
    }

    public override void CancelActionsForServer()
    {
        base.CancelActionsForServer();

        if (LFCUtilities.IsServer)
            CancelSetCoroutine();
    }

    public override void DoWandering()
    {
        if (setCoroutine != null) return;

        agent.speed = 4f;
        if (this.FoundClosestPlayerInRange(25, 10))
        {
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (DimensionRegistry.IsInUpsideDown(gameObject))
        {
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.PORTALING);
        }
    }

    public IEnumerator SetCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;

        DoAnimationEveryoneRpc("startSetIn");
        PlayAudioEveryoneRpc((int)Sound.SET);
        yield return this.WaitForFullAnimation("setin");

        DoAnimationEveryoneRpc("startSet");
        yield return this.WaitForFullAnimation("set");

        UpsideDownPortal upsideDownPortal = MapObjectsManager.SpawnUpsideDownPortalForServer(transform.position, isOutside, isFake: true);
        DoAnimationEveryoneRpc("startSetOut");
        yield return this.WaitForFullAnimation("setout");

        closestPortal = MapObjectsManager.GetClosestPortal(player.transform.position);
        yield return DigCoroutine(DimensionRegistry.IsInUpsideDown(player.gameObject));

        targetPlayer = player;
        Destroy(upsideDownPortal.gameObject);
        DoAnimationEveryoneRpc("startMove");
        SwitchToBehaviourClientRpc((int)State.CHASING);

        setCoroutine = null;
    }

    public void CancelSetCoroutine()
    {
        if (setCoroutine != null)
        {
            StopCoroutine(setCoroutine);
            setCoroutine = null;
            closestPortal = null;
            canSet = true;
            setTimer = 0f;
        }
    }

    public override void DoPortaling()
    {
        if (portalingCoroutine != null) return;

        agent.speed = 7f;
        if (!IsFleeing() && this.FoundClosestPlayerInRange(25, 10))
        {
            closestPortal = null;
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        closestPortal ??= MapObjectsManager.GetClosestPortal(transform.position);
        if (closestPortal == null)
        {
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (Vector3.Distance(transform.position, closestPortal.transform.position) <= 1f)
        {
            portalingCoroutine ??= StartCoroutine(PortalingCoroutine(!DimensionRegistry.IsInUpsideDown(gameObject)));
            return;
        }
        _ = SetDestinationToPosition(closestPortal.transform.position);
    }

    public IEnumerator PortalingCoroutine(bool isInUpsideDown)
    {
        agent.speed = 0f;
        yield return DigCoroutine(isInUpsideDown);

        DoAnimationEveryoneRpc("startMove");
        if (!this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer) || ShouldLoseTarget(distanceWithPlayer))
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }
        else
        {
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }

        portalingCoroutine = null;
    }

    public IEnumerator DigCoroutine(bool isInUpsideDown)
    {
        DoAnimationEveryoneRpc("startDig");
        PlayAudioEveryoneRpc((int)Sound.DIG);
        yield return this.WaitForFullAnimation("dig");

        DoAnimationEveryoneRpc("startDigIn");
        yield return this.WaitForFullAnimation("digin");

        LFCNetworkManager.Instance.TeleportEnemyEveryoneRpc(thisNetworkObject, closestPortal.transform.position, closestPortal.isOutside);
        if (DimensionRegistry.IsInUpsideDown(gameObject) != isInUpsideDown)
        {
            StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(thisNetworkObject, isInUpsideDown);
            if (isInUpsideDown) RestoreEnemyHealthEveryoneRpc();
        }

        DoAnimationEveryoneRpc("startDigOut");
        if (isHunting) PlayScreamAudioEveryoneRpc();
        yield return this.WaitForFullAnimation("digout");

        closestPortal = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayScreamAudioEveryoneRpc()
    {
        if (LFCUtilities.LocalPlayer != null)
        {
            GameObject audioObj = new GameObject("ScreamAudio");
            audioObj.transform.parent = LFCUtilities.LocalPlayer.transform;
            audioObj.transform.localPosition = Vector3.forward * 160f;

            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = ScreamSound;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 10f;
            audioSource.maxDistance = 200f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.Play();

            Destroy(audioObj, ScreamSound.length);
        }
    }
}