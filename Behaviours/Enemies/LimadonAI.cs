using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;
public class LimadonAI : UpsideDownEnemyAI
{
    public Transform TurnCompass;
    public LimadonSoundSerializableEntry[] LimadonSoundsEntry;
    public Dictionary<Sound, AudioClip[]> LimadonSounds;

    private float crawlTimer = 0f;
    private float jumpTimer = 15f;
    private float chargeTimer = 0f;
    public float jumpDuration = 1.167f;

    public float jumpCooldown = 20f;
    public float chargeCooldown = 10f;

    public bool canJump = false;
    public bool canCharge = false;
    public bool isSplashing = false;

    public Coroutine spawnCoroutine;
    public Coroutine stunCoroutine;
    public Coroutine jumpCoroutine;
    public Coroutine chargeCoroutine;
    public Coroutine swingCoroutine;

    public enum State { WANDERING, CHASING }
    public enum Sound { CRAWL, SPAWN, SWING, ROAR, JUMP, LAND, CHARGE }
    [Serializable] public class LimadonSoundSerializableEntry : LFCUtilities.SerializableEntry<Sound, AudioClip[]> { }

    public override void CancelActionsForServer()
    {
        if (LFCUtilities.IsServer)
        {
            CancelSpawnCoroutine();
            CancelJumpCoroutine();
            CancelChargeCoroutine();
            CancelSwingCoroutine();
        }
    }

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

        LimadonSounds = LimadonSoundsEntry.ToDictionary();
        spawnCoroutine ??= StartCoroutine(SpawnCoroutine());
    }

    public IEnumerator SpawnCoroutine()
    {
        agent.speed = 0f;
        if (LimadonSounds.TryGetValue(Sound.SPAWN, out AudioClip[] spawnSounds) && spawnSounds.Length > 0)
            creatureSFX.PlayOneShot(spawnSounds[UnityEngine.Random.Range(0, spawnSounds.Length)]);
        yield return this.WaitForFullAnimation("spawn");

        agent.speed = 2f;
        StartSearch(transform.position);
        currentBehaviourStateIndex = (int)State.WANDERING;
        CancelSpawnCoroutine();
    }

    public void CancelSpawnCoroutine()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void InitializeDuplicateEveryoneRpc(int playerId)
    {
        enemyHP = 2;
        if (currentBehaviourStateIndex != (int)State.CHASING)
        {
            StopSearch(currentSearch);
            if (playerId != -1) SetMovingTowardsTargetPlayer(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>());
            currentBehaviourStateIndex = (int)State.CHASING;
        }
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || stunCoroutine != null) return;

        PlayCrawlSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null && state == (int)State.CHASING)
        {
            TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
        }
        LFCUtilities.UpdateTimer(ref jumpTimer, jumpCooldown, !canJump, () => canJump = true);
        LFCUtilities.UpdateTimer(ref chargeTimer, chargeCooldown, !canCharge, () => canCharge = true);
    }

    public void PlayCrawlSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && currentAnimatorClipInfo[0].clip.name.Contains("crawl"))
        {
            crawlTimer -= Time.deltaTime;
            if (LimadonSounds.TryGetValue(Sound.CRAWL, out AudioClip[] crawlSounds) && crawlSounds.Length > 0 && crawlTimer <= 0)
            {
                creatureSFX.PlayOneShot(crawlSounds[UnityEngine.Random.Range(0, crawlSounds.Length)]);
                crawlTimer = 1.25f;
            }
        }
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 0.833f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (LFCUtilities.IsServer && setToStunned && !isSplashing && stunCoroutine == null)
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

        while (stunNormalizedTimer > 0f)
            yield return null;

        while (postStunInvincibilityTimer > 0f)
            yield return null;

        switch (currentBehaviourStateIndex)
        {
            case (int)State.WANDERING:
                DoAnimationEveryoneRpc("startCrawl");
                if (stunnedByPlayer != null)
                {
                    targetPlayer = stunnedByPlayer;
                    StopSearch(currentSearch);
                    SwitchToBehaviourClientRpc((int)State.CHASING);
                }
                break;
            case (int)State.CHASING:
                DoAnimationEveryoneRpc("startCrawl");
                break;
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
            case State.CHASING: DoChasing(); break;
        }
    }

    public void DoWandering()
    {
        if (spawnCoroutine != null) return;

        agent.speed = 2f;
        if (this.FoundClosestPlayerInRange(25, 10))
        {
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
    }

    public void DoChasing()
    {
        if (swingCoroutine != null || chargeCoroutine != null) return;
        if (jumpCoroutine == null)
        {
            agent.speed = 5f;
            if (!this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer) || (!isSynced && distanceWithPlayer > 35f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
            {
                StartSearch(transform.position);
                SwitchToBehaviourClientRpc((int)State.WANDERING);
                return;
            }
            if (canJump && distanceWithPlayer >= 5f && distanceWithPlayer < 15f)
            {
                canJump = false;
                jumpCoroutine = StartCoroutine(JumpCoroutine());
                return;
            }
            if (canCharge && distanceWithPlayer < 5f)
            {
                canCharge = false;
                chargeCoroutine = StartCoroutine(ChargeCoroutine());
                return;
            }
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator JumpCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startRoar");
        PlaySFXEveryoneRpc((int)Sound.ROAR);
        yield return this.WaitForFullAnimation("roar");

        DoAnimationEveryoneRpc("startJump");
        PlaySFXEveryoneRpc((int)Sound.JUMP);

        Vector3 landingPoint = Vector3.Distance(targetPlayer.transform.position, transform.position) > 25f ? transform.position : targetPlayer.transform.position;
        agent.angularSpeed = 0f;
        agent.acceleration = 100f;
        _ = SetDestinationToPosition(landingPoint);
        _ = agent.SetDestination(landingPoint);
        agent.speed = agent.remainingDistance / jumpDuration;

        yield return this.WaitForFullAnimation("jump");
        agent.speed = 0f;

        isSplashing = true;
        PlaySFXEveryoneRpc((int)Sound.LAND);
        StrangerThingsNetworkManager.Instance.PlayPoisonExplosionEveryoneRpc(transform.position);

        if (enemyHP > 4 && this.TryGetSafeRandomNavMeshPosition(transform.position, 3f, out Vector3 spawnPosition))
        {
            _ = StartCoroutine(SpawnEnemyCoroutine(spawnPosition));
            DeductHealthEveryoneRpc(enemyHP - 4);
        }

        isSplashing = false;
        DoAnimationEveryoneRpc("startDigOut");
        PlaySFXEveryoneRpc((int)Sound.SPAWN);
        yield return this.WaitForFullAnimation("spawn");

        CancelJumpCoroutine();
    }

    public IEnumerator SpawnEnemyCoroutine(Vector3 position)
    {
        GameObject gameObject = Instantiate(StrangerThings.LimadonType.enemyPrefab, position, Quaternion.identity);
        NetworkObject networkObject = gameObject.GetComponentInChildren<NetworkObject>();
        networkObject.Spawn(destroyWithScene: true);

        LimadonAI limadon = networkObject.GetComponentInChildren<LimadonAI>();
        yield return limadon.WaitForFullAnimation("spawn");
        limadon.InitializeDuplicateEveryoneRpc(targetPlayer != null ? (int)targetPlayer.playerClientId : -1);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DeductHealthEveryoneRpc(int newHP) => enemyHP -= newHP;

    public void CancelJumpCoroutine()
    {
        if (jumpCoroutine != null)
        {
            DoAnimationEveryoneRpc("startCrawl");
            StopCoroutine(jumpCoroutine);
            jumpCoroutine = null;

            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
        }
    }

    public IEnumerator ChargeCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startCharge");
        PlaySFXEveryoneRpc((int)Sound.CHARGE);
        yield return this.WaitForFullAnimation("charge");

        CancelChargeCoroutine();
    }

    public void CancelChargeCoroutine()
    {
        if (chargeCoroutine != null)
        {
            StrangerThingsNetworkManager.Instance.PlayPoisonExplosionEveryoneRpc(transform.position);
            DoAnimationEveryoneRpc("startCrawl");
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (isEnemyDead || currentBehaviourStateIndex != (int)State.CHASING || swingCoroutine != null) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        OnCollideWithPlayerServerRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void OnCollideWithPlayerServerRpc(int playerId)
    {
        if (chargeCoroutine != null)
        {
            CancelChargeCoroutine();
            return;
        }
        if (isSplashing)
        {
            isSplashing = false;
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 20, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        }
        if (jumpCoroutine == null)
            swingCoroutine ??= StartCoroutine(SwingCoroutine(playerId));
    }

    public IEnumerator SwingCoroutine(int playerId)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startSwing");
        PlaySFXEveryoneRpc((int)Sound.SWING);
        yield return this.WaitForFullAnimation("swing");

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 10, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        DoAnimationEveryoneRpc("startCrawl");
        agent.speed = 6f;

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
        if (!isEnemyDead)
        {
            CancelChargeCoroutine();
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        CancelActionsForServer();
        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlaySFXEveryoneRpc(int enemySound)
    {
        if (LimadonSounds.TryGetValue((Sound)enemySound, out AudioClip[] enemySounds) && enemySounds.Length > 0)
            creatureSFX.PlayOneShot(enemySounds[UnityEngine.Random.Range(0, enemySounds.Length)]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
