using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Behaviours.Scripts.Projectiles;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public abstract class DemogorgonAI : UpsideDownEnemyAI
{
    public Transform TurnCompass;
    public DemogorgonSoundSerializableEntry[] DemogorgonSoundsEntry;
    public Dictionary<Sound, AudioClip[]> DemogorgonSounds;

    private float moveTimer = 0f;
    private float growlTimer = 0f;
    private float dashTimer = 0f;
    public float dashCooldown = 15f;

    public bool canDash = false;
    public bool isDashing = false;

    public Coroutine stunCoroutine;
    public Coroutine portalingCoroutine;
    public Coroutine dashCoroutine;
    public Coroutine stopDashCoroutine;
    public Coroutine swingCoroutine;

    protected UpsideDownPortal closestPortal;

    public enum State { WANDERING, PORTALING, CHASING }
    public enum Sound { MOVE, GROWL, SET, DIG, CHARGE, DASH, SWING, ROAR }
    [Serializable] public class DemogorgonSoundSerializableEntry : LFCUtilities.SerializableEntry<Sound, AudioClip[]> { }

    public override void ForceSend()
    {
        if (currentBehaviourStateIndex == (int)State.WANDERING)
        {
            StopSearch(currentSearch);
            SetMovingTowardsTargetPlayer(syncedTarget);
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public override void Start()
    {
        base.Start();

        DemogorgonSounds = DemogorgonSoundsEntry.ToDictionary();
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);
        MapObjectsManager.SpawnPortalsForServer();
        OnDemogorgonStart();
    }

    protected virtual void OnDemogorgonStart() { }

    public override void Update()
    {
        base.Update();
        if (stunCoroutine != null) return;

        PlayMoveSound();
        PlayGrowlSound();
        if (targetPlayer != null)
        {
            if (currentBehaviourStateIndex == (int)State.CHASING)
            {
                TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
            }
            else
            {
                UpdateNonChasingTargetPlayer(targetPlayer);
            }
        }
        UpdateCooldowns();
    }

    protected virtual void UpdateNonChasingTargetPlayer(PlayerControllerB player) { }

    protected virtual void UpdateCooldowns()
        => LFCUtilities.UpdateTimer(ref dashTimer, dashCooldown, !canDash, () => canDash = true);

    public override void CancelActionsForServer()
    {
        if (LFCUtilities.IsServer)
        {
            CancelPortalingCoroutine();
            CancelDashCoroutine();
            CancelSwingCoroutine();
        }
    }

    public void PlayMoveSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && (currentAnimatorClipInfo[0].clip.name.Contains("move") || currentAnimatorClipInfo[0].clip.name.Contains("carry")))
        {
            moveTimer -= Time.deltaTime;
            if (DemogorgonSounds.TryGetValue(Sound.MOVE, out AudioClip[] moveSounds) && moveSounds.Length > 0 && moveTimer <= 0)
            {
                creatureSFX.PlayOneShot(moveSounds[UnityEngine.Random.Range(0, moveSounds.Length)]);
                moveTimer = 0.45f;
            }
        }
    }

    public void PlayGrowlSound()
    {
        growlTimer -= Time.deltaTime;
        if (DemogorgonSounds.TryGetValue(Sound.GROWL, out AudioClip[] growlSounds) && growlSounds.Length > 0 && growlTimer <= 0)
        {
            creatureSFX.PlayOneShot(growlSounds[UnityEngine.Random.Range(0, growlSounds.Length)]);
            growlTimer = 4f;
        }
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 3.958f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (LFCUtilities.IsServer && setToStunned && stunCoroutine == null && stopDashCoroutine == null)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            stunCoroutine = StartCoroutine(StunCoroutine());
        }
    }

    public IEnumerator StunCoroutine()
    {
        CancelActionsForServer();

        agent.speed = 0f;
        DoAnimationEveryoneRpc("startStun");
        yield return this.WaitForFullAnimation("stun");

        while (stunNormalizedTimer > 0f) yield return null;
        while (postStunInvincibilityTimer > 0f) yield return null;

        DoAnimationEveryoneRpc("startMove");
        if (currentBehaviourStateIndex == (int)State.PORTALING && (stunnedByPlayer != null || targetPlayer != null))
        {
            if (!targetPlayer) targetPlayer = stunnedByPlayer;
            closestPortal = null;
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }

        stunCoroutine = null;
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunCoroutine != null) return;

        switch ((State)currentBehaviourStateIndex)
        {
            case State.WANDERING: DoWandering(); break;
            case State.PORTALING: DoPortaling(); break;
            case State.CHASING: DoChasing(); break;
        }
    }

    public abstract void DoWandering();
    public abstract void DoPortaling();

    protected virtual bool IgnoreLoseTargetConditions => false;

    protected bool ShouldLoseTarget(float distanceWithPlayer)
        => targetPlayer == null
            || (!isSynced
                && !IgnoreLoseTargetConditions
                && distanceWithPlayer > 30f
                && !CheckLineOfSightForPosition(targetPlayer.transform.position));

    public virtual void DoChasing()
    {
        if (swingCoroutine != null) return;
        if (dashCoroutine == null)
        {
            agent.speed = 7f;
            if (this.TargetOutsideChasedPlayer()) return;
            if (IsFleeing() || (targetPlayer != null && !DimensionRegistry.AreInSameDimension(gameObject, targetPlayer.gameObject)))
            {
                SwitchToBehaviourClientRpc((int)State.PORTALING);
                return;
            }
            if (!this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer) || ShouldLoseTarget(distanceWithPlayer))
            {
                StartSearch(transform.position);
                SwitchToBehaviourClientRpc((int)State.WANDERING);
                return;
            }
            if (canDash && distanceWithPlayer <= 15f && distanceWithPlayer >= 3f && targetPlayer != null && CheckLineOfSightForPosition(targetPlayer.transform.position))
            {
                canDash = false;
                dashCoroutine = StartCoroutine(DashCoroutine());
                return;
            }
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator DashCoroutine()
    {
        bool firstPass = true;
        int nbDashes = UnityEngine.Random.Range(minInclusive: 1, maxExclusive: 4);

        for (int i = 0; i < nbDashes; i++)
        {
            if (firstPass || isDashing)
            {
                firstPass = false;
                isDashing = false;

                agent.speed = 0f;
                DoAnimationEveryoneRpc("startCharge");
                PlaySFXEveryoneRpc((int)Sound.CHARGE);
                yield return this.WaitForFullAnimation("charge");

                DoAnimationEveryoneRpc("startDash");
                PlaySFXEveryoneRpc((int)Sound.DASH);

                isDashing = true;
                agent.speed = 24f;
                agent.angularSpeed = 0f;
                agent.acceleration = 100f;

                if (targetPlayer != null)
                {
                    Vector3 dashDirection = (targetPlayer.transform.position - transform.position).normalized;
                    float dashTime = 0.6f;
                    float timer = 0f;

                    while (timer < dashTime && isDashing)
                    {
                        timer += Time.deltaTime;
                        agent.velocity = dashDirection * agent.speed;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(0.33f);
            }
        }

        if (isDashing) StopDash();
    }

    public virtual void StopDash(PlayerControllerB player = null)
    {
        agent.speed = 0f;
        isDashing = false;
        DoAnimationEveryoneRpc("startRecover");
        stopDashCoroutine ??= StartCoroutine(StopDashCoroutine(player));
    }

    public virtual IEnumerator StopDashCoroutine(PlayerControllerB player)
    {
        if (player != null)
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);

        PlaySFXEveryoneRpc((int)Sound.ROAR);
        yield return this.WaitForFullAnimation("recover");
        DoAnimationEveryoneRpc("startMove");

        CancelDashCoroutine();
        stopDashCoroutine = null;
    }

    public void CancelDashCoroutine()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
            isDashing = false;

            agent.speed = 7f;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.velocity = Vector3.zero;
        }
    }

    public void CancelPortalingCoroutine()
    {
        if (portalingCoroutine != null)
        {
            StopCoroutine(portalingCoroutine);
            portalingCoroutine = null;
            closestPortal = null;
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (currentBehaviourStateIndex != (int)State.CHASING || IsFleeing()) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        OnCollideWithPlayerServerRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void OnCollideWithPlayerServerRpc(int playerId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
        if (isDashing) StopDash(player);
        if (dashCoroutine == null) swingCoroutine ??= StartCoroutine(SwingCoroutine(player));
    }

    public IEnumerator SwingCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startSwing");
        PlaySFXEveryoneRpc((int)Sound.SWING);
        yield return this.WaitForFullAnimation("swing");

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 20, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        DoAnimationEveryoneRpc("startMove");
        yield return new WaitForSeconds(0.5f);
        agent.speed = 7f;

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

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (!isEnemyDead && !IsFleeing())
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
    }

    public override void KillEnemy(bool destroy = false)
    {
        if (DimensionRegistry.IsInUpsideDown(gameObject))
        {
            CancelActionsForServer();
            base.KillEnemy(destroy);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void BreakDoorEveryoneRpc(NetworkObjectReference obj, Vector3 direction)
    {
        if (obj.TryGet(out NetworkObject networkObject))
        {
            GameObject doorObject = networkObject.gameObject;
            doorObject.transform.position += direction * 0.5f;

            DoorProjectile doorProjectile = doorObject.GetComponent<DoorProjectile>() ?? doorObject.AddComponent<DoorProjectile>();
            doorProjectile.Initialize();
            doorProjectile.Throw(direction, 20f, isLastThrow: true);

            if (networkObject.gameObject.TryGetComponentInChildren(out AnimatedObjectTrigger objectTrigger))
                objectTrigger.PlayAudio(objectTrigger.boolValue, true);
        }
    }

    public bool IsFleeing() => !DimensionRegistry.IsInUpsideDown(gameObject) && enemyHP <= 5;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void RestoreEnemyHealthEveryoneRpc() => enemyHP = 10;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlaySFXEveryoneRpc(int enemySound)
    {
        if (DemogorgonSounds.TryGetValue((Sound)enemySound, out AudioClip[] enemySounds) && enemySounds.Length > 0)
            creatureSFX.PlayOneShot(enemySounds[UnityEngine.Random.Range(0, enemySounds.Length)]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}