using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class CrustapikanLarvaeAI : UpsideDownEnemyAI
{
    public Transform TurnCompass;
    public AudioClip[] FootstepSounds = Array.Empty<AudioClip>();
    public AudioClip[] GroundSounds = Array.Empty<AudioClip>();
    public AudioClip[] CrustapikanLarvaeSounds = Array.Empty<AudioClip>();

    public bool isDigging = false;
    public bool isDashing = false;

    public float footstepTimer = 0f;
    public float groundstepTimer = 0f;

    public Coroutine stunCoroutine;
    public Coroutine dashCoroutine;
    public Coroutine biteCoroutine;

    public enum State
    {
        WANDERING,
        SYNCING,
        CHASING
    }

    public enum Sound
    {
        DIGIN,
        DIGOUT,
        DASH,
        BITE,
        ROAR
    }

    public override void ForceSync()
    {
        if (currentBehaviourStateIndex == (int)State.WANDERING)
        {
            StopSearch(currentSearch);
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.SYNCING);
        }
    }

    public override void ForceSend()
    {
        if (currentBehaviourStateIndex == (int)State.WANDERING || currentBehaviourStateIndex == (int)State.SYNCING)
        {
            CancelDashCoroutine();
            SetMovingTowardsTargetPlayer(syncedTarget);
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public override void Start()
    {
        base.Start();

        callDistance = 90f;
        if (CrustapikanLarvaeSounds.Length > 0)
            creatureSFX.PlayOneShot(CrustapikanLarvaeSounds[(int)Sound.DIGOUT]);
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || stunCoroutine != null) return;

        PlayFootstepSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null && state == (int)State.CHASING)
        {
            TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
        }
    }

    public void PlayFootstepSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && (currentAnimatorClipInfo[0].clip.name.Contains("walk") || currentAnimatorClipInfo[0].clip.name.Contains("chase")))
        {
            footstepTimer -= Time.deltaTime;
            if (FootstepSounds.Length > 0 && footstepTimer <= 0)
            {
                creatureSFX.PlayOneShot(FootstepSounds[UnityEngine.Random.Range(0, FootstepSounds.Length)]);
                footstepTimer = 0.5f;
            }
        }
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 0.833f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (LFCUtilities.IsServer && setToStunned && stunCoroutine == null)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            stunCoroutine = StartCoroutine(StunCoroutine());
        }
    }

    public IEnumerator StunCoroutine()
    {
        CancelDashCoroutine();
        CancelBiteCoroutine();

        agent.speed = 0f;
        DoAnimationEveryoneRpc("startStun");
        yield return this.WaitForFullAnimation("stun");

        while (stunNormalizedTimer > 0f)
            yield return null;

        while (postStunInvincibilityTimer > 0f)
            yield return null;

        switch (currentBehaviourStateIndex)
        {
            case (int)State.WANDERING:
                if (stunnedByPlayer != null)
                {
                    targetPlayer = stunnedByPlayer;
                    StopSearch(currentSearch);
                    DoAnimationEveryoneRpc("startChase");
                    SwitchToBehaviourClientRpc((int)State.CHASING);
                }
                else
                {
                    DoAnimationEveryoneRpc("startWalk");
                }
                break;
            case (int)State.SYNCING:
            case (int)State.CHASING:
                DoAnimationEveryoneRpc("startChase");
                break;
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
        }
    }

    public void DoWandering()
    {
        agent.speed = 3f;
        if (this.FoundClosestPlayerInRange(35, 10))
        {
            StopSearch(currentSearch);
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
    }

    public void DoSyncing()
    {
        if (caller == null || caller.isEnemyDead)
        {
            RemoveCaller();
            CancelDashCoroutine();
            StartSearch(transform.position);
            DoAnimationEveryoneRpc("startWalk");
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }
        float distanceWithCaller = Vector3.Distance(transform.position, caller.transform.position);
        if (distanceWithCaller >= 10f)
            dashCoroutine ??= StartCoroutine(DashCoroutine());
        else if (dashCoroutine == null)
            agent.speed = 2f;
        _ = SetDestinationToPosition(caller.transform.position);
    }

    public void DoChasing()
    {
        if (biteCoroutine != null) return;
        if (dashCoroutine == null)
        {
            agent.speed = 6f;
            float distanceWithPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (!this.TargetClosestPlayerInAnyCase() || (distanceWithPlayer > 50f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
            {
                StartSearch(transform.position);
                DoAnimationEveryoneRpc("startWalk");
                SwitchToBehaviourClientRpc((int)State.WANDERING);
                return;
            }
            if (distanceWithPlayer >= 10f)
            {
                dashCoroutine = StartCoroutine(DashCoroutine());
                return;
            }
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator DashCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startRoar");
        PlayAudioEveryoneRpc((int)Sound.ROAR);
        yield return this.WaitForFullAnimation("roar");

        DoAnimationEveryoneRpc("startDigIn");
        PlayAudioEveryoneRpc((int)Sound.DIGIN);
        yield return this.WaitForFullAnimation("digin");

        SetDiggingEveryoneRpc(true);
        agent.speed = 9f;

        Transform entity = currentBehaviourStateIndex == (int)State.CHASING ? targetPlayer.transform : caller.transform;
        float maxDistance = currentBehaviourStateIndex == (int)State.CHASING ? 50f : callDistance;
        while (entity != null)
        {
            float distanceWithEntity = Vector3.Distance(transform.position, entity.position);
            if (distanceWithEntity <= 5f || distanceWithEntity >= maxDistance || !DimensionRegistry.AreInSameDimension(entity.gameObject, gameObject)) break;

            yield return null;
        }

        agent.speed = 0f;
        SetDiggingEveryoneRpc(false);

        DoAnimationEveryoneRpc("startDigOut");
        PlayAudioEveryoneRpc((int)Sound.DIGOUT);
        yield return this.WaitForFullAnimation("digout");

        if (currentBehaviourStateIndex == (int)State.CHASING && targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position) <= 15f)
        {
            DoAnimationEveryoneRpc("startDash");
            PlayAudioEveryoneRpc((int)Sound.DASH);

            isDashing = true;
            agent.speed = 24f;
            agent.angularSpeed = 0f;
            agent.acceleration = 100f;

            Vector3 dashDirection = (targetPlayer.transform.position - transform.position).normalized;
            float dashTime = 0.75f;
            float timer = 0f;
            while (timer < dashTime && isDashing)
            {
                timer += Time.deltaTime;
                agent.velocity = dashDirection * agent.speed;
                yield return null;
            }
        }

        CancelDashCoroutine();
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetDiggingEveryoneRpc(bool enable)
    {
        isDigging = enable;
        EnableEnemyMesh(!enable);
        if (enable) _ = StartCoroutine(DiggingCoroutine());
    }

    public IEnumerator DiggingCoroutine()
    {
        while (isDigging)
        {
            PlayGroundstepSound();
            LFCGlobalManager.PlayParticle($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.groundParticle.name}", transform.position, Quaternion.identity, 0.075f);
            LFCGlobalManager.PlayParticle($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.bloodParticle.name}", transform.position, Quaternion.identity);

            yield return null;
        }
    }

    public void PlayGroundstepSound()
    {
        groundstepTimer -= Time.deltaTime;
        if (GroundSounds.Length > 0 && groundstepTimer <= 0)
        {
            creatureSFX.PlayOneShot(GroundSounds[UnityEngine.Random.Range(0, GroundSounds.Length)]);
            groundstepTimer = 0.2f;
        }
    }

    public void CancelDashCoroutine()
    {
        if (dashCoroutine != null)
        {
            SetDiggingEveryoneRpc(false);
            ResetAnimationEveryoneRpc("startDash");
            DoAnimationEveryoneRpc("startChase");

            isDashing = false;
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;

            agent.speed = 6f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.velocity = Vector3.zero;
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (isEnemyDead || currentBehaviourStateIndex != (int)State.CHASING || biteCoroutine != null) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        OnCollideWithPlayerServerRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void OnCollideWithPlayerServerRpc(int playerId)
    {
        if (isDashing)
        {
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 20, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            CancelDashCoroutine();
        }
        if (dashCoroutine == null)
            biteCoroutine ??= StartCoroutine(BiteCoroutine(playerId));
    }

    public IEnumerator BiteCoroutine(int playerId)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startBite");
        PlayAudioEveryoneRpc((int)Sound.BITE);
        yield return this.WaitForFullAnimation("bite");

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 10, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        DoAnimationEveryoneRpc("startChase");
        agent.speed = 6f;

        biteCoroutine = null;
    }

    public void CancelBiteCoroutine()
    {
        if (biteCoroutine != null)
        {
            StopCoroutine(biteCoroutine);
            biteCoroutine = null;
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (!isEnemyDead && dashCoroutine == null)
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
    }

    public override void KillEnemy(bool destroy = false)
    {
        RemoveCaller();
        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayAudioEveryoneRpc(int enemySound)
    {
        if (CrustapikanLarvaeSounds.Length > 0)
            creatureSFX.PlayOneShot(CrustapikanLarvaeSounds[enemySound]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ResetAnimationEveryoneRpc(string animationState) => creatureAnimator.ResetTrigger(animationState);

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
