using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class CrustapikanAI : UpsideDownEnemyAI
{
    public Transform TurnCompass;
    public Transform GrabPoint;
    public AudioClip[] FootstepSounds = Array.Empty<AudioClip>();
    public AudioClip[] CrustapikanSounds = Array.Empty<AudioClip>();

    public float footstepTimer = 0f;
    public float lookTimer = 0f;
    public float callTimer = 0f;
    public float grabTimer = 0f;

    public float lookCooldown = 30f;
    public float callCooldown = 60f;
    public float grabCooldown = 20f;

    public bool canLook = false;
    public bool canCall = false;
    public bool canGrab = false;

    public Coroutine stunCoroutine;
    public Coroutine lookCoroutine;
    public Coroutine callCoroutine;
    public Coroutine grabCoroutine;
    public Coroutine throwCoroutine;
    public Coroutine swingCoroutine;

    public RockProjectile rockProjectile;
    public Vector3 lastSeenPosition;
    public List<UpsideDownEnemyAI> syncedEnemies = [];
    public List<UpsideDownEnemyAI> spawnedEnemies = [];

    public enum State
    {
        WANDERING,
        SYNCING,
        CHASING,
        CALLING,
        CARRYING
    }

    public enum Sound
    {
        CALL,
        SMASH,
        SEND,
        GRAB,
        THROW,
        SWING
    }

    public override void ForceSync()
    {
        if (currentBehaviourStateIndex == (int)State.WANDERING)
        {
            canCall = false;
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.SYNCING);
        }
    }

    public override void ForceSend()
    {
        if (currentBehaviourStateIndex == (int)State.WANDERING || currentBehaviourStateIndex == (int)State.SYNCING)
        {
            SetMovingTowardsTargetPlayer(syncedTarget);
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public override void Start()
    {
        base.Start();

        callDistance = 40f;
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || stunCoroutine != null) return;

        PlayFootstepSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null && (state == (int)State.CHASING || state == (int)State.CALLING || state == (int)State.CARRYING))
        {
            TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
        }
        LFCUtilities.UpdateTimer(ref lookTimer, lookCooldown, !canLook, () => canLook = true);
        LFCUtilities.UpdateTimer(ref callTimer, callCooldown, !canCall && state != (int)State.CALLING, () => canCall = true);
        LFCUtilities.UpdateTimer(ref grabTimer, grabCooldown, !canGrab, () => canGrab = true);
    }

    public void PlayFootstepSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && currentAnimatorClipInfo[0].clip.name.Contains("move"))
        {
            footstepTimer -= Time.deltaTime;
            if (FootstepSounds.Length > 0 && footstepTimer <= 0)
            {
                creatureSFX.PlayOneShot(FootstepSounds[UnityEngine.Random.Range(0, FootstepSounds.Length)]);
                footstepTimer = 1.25f;
            }
        }
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 2.333f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (LFCUtilities.IsServer && setToStunned && stunCoroutine == null)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            stunCoroutine = StartCoroutine(StunCoroutine());
        }
    }

    public IEnumerator StunCoroutine()
    {
        CancelLookCoroutine();
        CancelGrabCoroutine();
        CancelSwingCoroutine();
        CancelCallCoroutine();
        CancelThrowCoroutine();

        agent.speed = 0f;
        DoAnimationEveryoneRpc("startStun");
        yield return this.WaitForFullAnimation("stun");

        while (stunNormalizedTimer > 0f)
            yield return null;

        while (postStunInvincibilityTimer > 0f)
            yield return null;

        DoAnimationEveryoneRpc("startMove");
        if (currentBehaviourStateIndex == (int)State.WANDERING && stunnedByPlayer != null)
        {
            targetPlayer = stunnedByPlayer;
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }

        stunCoroutine = null;
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunCoroutine != null) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)State.WANDERING: DoWandering(); break;
            case (int)State.SYNCING: DoSyncing(); break;
            case (int)State.CHASING: DoChasing(); break;
            case (int)State.CALLING: DoCalling(); break;
            case (int)State.CARRYING: DoCarrying(); break;
        }
    }

    public void DoWandering()
    {
        if (lookCoroutine != null) return;

        agent.speed = 3f;
        if (this.FoundClosestPlayerInRange(35, 10))
        {
            canCall = true;
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (canLook)
        {
            canLook = false;
            StopSearch(currentSearch);
            lookCoroutine ??= StartCoroutine(LookCoroutine());
        }
    }

    public IEnumerator LookCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startLook");

        IEnumerator waitForLook = this.WaitForFullAnimation("look");
        while (waitForLook.MoveNext())
        {
            if (this.FoundClosestPlayerInRange(50, 15, 90f))
            {
                DoAnimationEveryoneRpc("startMove");
                SwitchToBehaviourClientRpc((int)State.CHASING);
                yield break;
            }

            yield return waitForLook.Current;
        }

        StartSearch(transform.position);
        DoAnimationEveryoneRpc("startMove");
        lookCoroutine = null;
    }

    public void CancelLookCoroutine()
    {
        if (lookCoroutine != null)
        {
            StopCoroutine(lookCoroutine);
            lookCoroutine = null;
            canLook = true;
            lookTimer = 0f;
        }
    }

    public void DoSyncing()
    {
        if (caller == null || caller.isEnemyDead)
        {
            RemoveCaller();
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }
        agent.speed = Vector3.Distance(transform.position, caller.transform.position) < 10f ? 2f : 4f;
        _ = SetDestinationToPosition(caller.transform.position);
    }

    public void DoChasing()
    {
        if (swingCoroutine != null || grabCoroutine != null) return;

        agent.speed = 4f;
        float distanceWithPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        if (!this.TargetClosestPlayerInAnyCase() || (distanceWithPlayer > 50f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            return;
        }
        if (canCall && syncedEnemies.Count == 0)
        {
            canCall = false;
            canGrab = false;
            SwitchToBehaviourClientRpc((int)State.CALLING);
            return;
        }
        if (canGrab && distanceWithPlayer >= 15f && CheckLineOfSightForPosition(targetPlayer.transform.position))
        {
            canGrab = false;
            grabCoroutine ??= StartCoroutine(GrabCoroutine());
            return;
        }

        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public void DoCalling()
    {
        callCoroutine ??= StartCoroutine(CallCoroutine());
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator CallCoroutine()
    {
        agent.speed = 0f;
        PlayAudioEveryoneRpc((int)Sound.CALL);
        DoAnimationEveryoneRpc("startCall");
        yield return this.WaitForFullAnimation("call");

        foreach (Collider hitCollider in Physics.OverlapSphere(transform.position, 100f, 524288, QueryTriggerInteraction.Collide))
        {
            EnemyAI enemy = hitCollider.GetComponent<EnemyAICollisionDetect>()?.mainScript;
            if (enemy == null || enemy == this || enemy.isEnemyDead || !DimensionRegistry.AreInSameDimension(gameObject, enemy.gameObject)) continue;
            if (enemy is not UpsideDownEnemyAI upsideDownEnemy || Vector3.Distance(upsideDownEnemy.transform.position, transform.position) > upsideDownEnemy.callDistance) continue;

            upsideDownEnemy.SetCaller(this, targetPlayer);
            upsideDownEnemy.ForceSync();
        }

        // Si pas d'ennemi aux alentours, faire une invocation
        if (syncedEnemies.Count == 0)
        {
            DoAnimationEveryoneRpc("startSmash");
            yield return new WaitForSeconds(2.15f);

            PlayAudioEveryoneRpc((int)Sound.SMASH);
            LFCNetworkManager.Instance.PlayParticleEveryoneRpc($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.groundParticle.name}", transform.position + transform.forward, Quaternion.identity, 0.5f);
            yield return this.WaitForFullAnimation("smash");

            for (int i = 0; i < 2; i++)
            {
                UpsideDownEnemyAI upsideDownEnemy = SpawnEnemyForServer(StrangerThings.crustopikanLarvaeType, transform.position, 5f);
                if (upsideDownEnemy != null)
                {
                    upsideDownEnemy.SetCaller(this, targetPlayer);
                    upsideDownEnemy.ForceSync();
                }
            }
            yield return null;
        }

        agent.speed = 2f;
        DoAnimationEveryoneRpc("startMove");
        yield return new WaitUntil(() => syncedEnemies.All(e => Vector3.Distance(e.transform.position, transform.position) <= 15f || e.targetPlayer != null));

        if (syncedEnemies.Any(e => e.targetPlayer == null))
        {
            agent.speed = 0f;
            PlayAudioEveryoneRpc((int)Sound.SEND);
            DoAnimationEveryoneRpc("startSend");
            yield return this.WaitForFullAnimation("send");

            foreach (UpsideDownEnemyAI upsideDownEnemy in syncedEnemies)
                upsideDownEnemy.ForceSend();
        }

        CleanCallers();
        DoAnimationEveryoneRpc("startMove");
        SwitchToBehaviourClientRpc((int)State.CHASING);
        callCoroutine = null;
    }

    public void CancelCallCoroutine()
    {
        if (callCoroutine != null)
        {
            StopCoroutine(callCoroutine);
            callCoroutine = null;
            CleanCallers();
            canCall = true;
            callTimer = 0f;
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public void CleanCallers()
    {
        foreach (UpsideDownEnemyAI upsideDownEnemy in syncedEnemies.ToList())
            upsideDownEnemy.RemoveCaller();
        RemoveCaller();
    }

    public IEnumerator GrabCoroutine()
    {
        agent.speed = 0f;
        PlayAudioEveryoneRpc((int)Sound.GRAB);
        DoAnimationEveryoneRpc("startGrab");
        yield return new WaitForSeconds(1.7f);

        SpawnRockForServer();
        yield return this.WaitForFullAnimation("grab");

        DoAnimationEveryoneRpc("startCarry");
        SwitchToBehaviourClientRpc((int)State.CARRYING);
        grabCoroutine = null;
    }

    public void SpawnRockForServer()
    {
        GameObject gameObject = Instantiate(StrangerThings.rockProjectileObj, GrabPoint.position, GrabPoint.rotation);
        NetworkObject networkObject = gameObject.GetComponent<NetworkObject>();
        networkObject.Spawn();
        SpawnRockEveryoneRpc(networkObject);
        rockProjectile = gameObject.GetComponent<RockProjectile>();
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnRockEveryoneRpc(NetworkObjectReference obj)
    {
        if (obj.TryGet(out NetworkObject networkObject))
            networkObject.transform.SetParent(GrabPoint, true);
    }

    public void CancelGrabCoroutine()
    {
        if (grabCoroutine != null)
        {
            StopCoroutine(grabCoroutine);
            grabCoroutine = null;
            rockProjectile?.ThrowRockEveryoneRpc(thisNetworkObject, transform.position);
            rockProjectile = null;
        }
    }

    public void DoCarrying()
    {
        if (throwCoroutine != null) return;

        if (rockProjectile == null)
        {
            lastSeenPosition = Vector3.zero;
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        agent.speed = 4f;
        float distanceWithPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        lastSeenPosition = targetPlayer != null && distanceWithPlayer <= 40f
            ? targetPlayer.transform.position
            : lastSeenPosition != Vector3.zero ? lastSeenPosition : transform.position;
        if (!this.TargetClosestPlayerInAnyCase()
            || Vector3.Distance(transform.position, lastSeenPosition) <= 40f
            || (distanceWithPlayer > 60f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
        {
            throwCoroutine ??= StartCoroutine(ThrowCoroutine());
            return;
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator ThrowCoroutine()
    {
        agent.speed = 0f;
        PlayAudioEveryoneRpc((int)Sound.THROW);
        DoAnimationEveryoneRpc("startThrow");
        yield return new WaitForSeconds(0.8f);

        rockProjectile?.ThrowRockEveryoneRpc(thisNetworkObject, lastSeenPosition);
        rockProjectile = null;
        yield return this.WaitForFullAnimation("throw");

        DoAnimationEveryoneRpc("startMove");
        SwitchToBehaviourClientRpc((int)State.CHASING);

        lastSeenPosition = Vector3.zero;
        throwCoroutine = null;
    }

    public void CancelThrowCoroutine()
    {
        if (throwCoroutine != null)
        {
            StopCoroutine(throwCoroutine);
            throwCoroutine = null;
            canGrab = true;
            grabTimer = 0f;
            rockProjectile?.ThrowRockEveryoneRpc(thisNetworkObject, transform.position);
            rockProjectile = null;
        }
    }

    public UpsideDownEnemyAI SpawnEnemyForServer(EnemyType enemyType, Vector3 position, float radius)
    {
        _ = spawnedEnemies.RemoveAll(e => e == null || e.isEnemyDead);
        if (spawnedEnemies.Count < 3 && this.TryGetSafeRandomNavMeshPosition(position, radius, out Vector3 spawnPosition))
        {
            GameObject gameObject = Instantiate(enemyType.enemyPrefab, spawnPosition, Quaternion.identity);
            gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);

            UpsideDownEnemyAI upsideDownEnemy = gameObject.GetComponentInChildren<UpsideDownEnemyAI>();
            spawnedEnemies.Add(upsideDownEnemy);
            return upsideDownEnemy;
        }
        return null;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (isEnemyDead || currentBehaviourStateIndex != (int)State.CHASING || swingCoroutine != null) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        SwingServerRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SwingServerRpc(int playerId) => swingCoroutine ??= StartCoroutine(SwingCoroutine(playerId));

    public IEnumerator SwingCoroutine(int playerId)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startSwing");
        PlayAudioEveryoneRpc((int)Sound.SWING);
        yield return new WaitForSeconds(2.08f);

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 100, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        yield return this.WaitForFullAnimation("swing");

        DoAnimationEveryoneRpc("startMove");
        if (StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>().isPlayerDead)
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }

        swingCoroutine = null;
    }

    public void CancelSwingCoroutine()
    {
        if (swingCoroutine != null)
        {
            StopCoroutine(swingCoroutine);
            swingCoroutine = null;
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1) { }

    public override void KillEnemy(bool destroy = false)
    {
        RemoveCaller();
        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayAudioEveryoneRpc(int enemySound)
    {
        if (CrustapikanSounds.Length > 0)
            creatureSFX.PlayOneShot(CrustapikanSounds[enemySound]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
