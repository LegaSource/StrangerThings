using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts.Projectiles;
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
    public CrustapikanSoundSerializableEntry[] CrustapikanSoundsEntry;
    public Dictionary<Sound, AudioClip[]> CrustapikanSounds;

    private float moveTimer = 0f;
    private float lookTimer = 10f;
    private float callTimer = 30f;
    private float grabTimer = 0f;

    public float lookCooldown = 20f;
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

    protected RockProjectile rockProjectile;
    protected Vector3 lastSeenPosition;

    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly float AoERadius = 100f;
    private readonly int AoEMask = 524288;

    public enum State { WANDERING, CHASING, CARRYING }
    public enum Sound { MOVE, SMASH, ROAR, GRAB, THROW, SWING }
    [Serializable] public class CrustapikanSoundSerializableEntry : LFCUtilities.SerializableEntry<Sound, AudioClip[]> { }

    public override void CancelActionsForServer()
    {
        if (LFCUtilities.IsServer)
        {
            CancelLookCoroutine();
            CancelGrabCoroutine();
            CancelSwingCoroutine();
            CancelCallCoroutine();
            CancelThrowCoroutine();
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

        CrustapikanSounds = CrustapikanSoundsEntry.ToDictionary();
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || stunCoroutine != null) return;

        PlayMoveSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null && (state == (int)State.CHASING || state == (int)State.CARRYING))
        {
            TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
        }
        LFCUtilities.UpdateTimer(ref lookTimer, lookCooldown, !canLook, () => canLook = true);
        LFCUtilities.UpdateTimer(ref callTimer, callCooldown, !canCall, () => canCall = true);
        LFCUtilities.UpdateTimer(ref grabTimer, grabCooldown, !canGrab, () => canGrab = true);
    }

    public void PlayMoveSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && currentAnimatorClipInfo[0].clip.name.Contains("move"))
        {
            moveTimer -= Time.deltaTime;
            if (CrustapikanSounds.TryGetValue(Sound.MOVE, out AudioClip[] moveSounds) && moveSounds.Length > 0 && moveTimer <= 0)
            {
                creatureSFX.PlayOneShot(moveSounds[UnityEngine.Random.Range(0, moveSounds.Length)]);
                moveTimer = 1.25f;
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
        CancelActionsForServer();

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

        switch ((State)currentBehaviourStateIndex)
        {
            case State.WANDERING: DoWandering(); break;
            case State.CHASING: DoChasing(); break;
            case State.CARRYING: DoCarrying(); break;
        }
    }

    public void DoWandering()
    {
        if (lookCoroutine != null) return;

        agent.speed = 3f;
        if (this.FoundClosestPlayerInRange(35, 10, cannotBeInShip: true))
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
            if (this.FoundClosestPlayerInRange(50, 15, 90f, cannotBeInShip: true))
            {
                DoAnimationEveryoneRpc("startMove");
                SwitchToBehaviourClientRpc((int)State.CHASING);
                lookCoroutine = null;
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

    public void DoChasing()
    {
        if (swingCoroutine != null || callCoroutine != null || grabCoroutine != null) return;

        agent.speed = 4f;
        if (!this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer, cannotBeInShip: true) || (!isSynced && distanceWithPlayer > 50f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            return;
        }
        if (canCall)
        {
            canCall = false;
            callCoroutine ??= StartCoroutine(CallCoroutine());
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

    public IEnumerator CallCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startSmash");
        yield return new WaitForSeconds(2.15f);

        PlaySFXEveryoneRpc((int)Sound.SMASH);
        PlayParticleEveryoneRpc($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.Constants.GROUND_PARTICLES}", transform.position + transform.forward, Quaternion.identity, scaleFactor: 0.5f);
        yield return this.WaitForFullAnimation("smash");

        HashSet<UpsideDownEnemyAI> syncedEnemies = [];
        int count = Physics.OverlapSphereNonAlloc(transform.position, AoERadius, overlapBuffer, AoEMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            EnemyAI enemy = overlapBuffer[i].GetComponent<EnemyAICollisionDetect>()?.mainScript;
            if (enemy == null || enemy == this || enemy.isEnemyDead || !DimensionRegistry.AreInSameDimension(gameObject, enemy.gameObject)) continue;
            if (enemy is not UpsideDownEnemyAI upsideDownEnemy || Vector3.Distance(upsideDownEnemy.transform.position, transform.position) > upsideDownEnemy.syncDistance) continue;

            upsideDownEnemy.SetSyncedTarget(targetPlayer);
            _ = syncedEnemies.Add(upsideDownEnemy);
        }

        UpsideDownEnemyAI crustopikanLarvaeEnemy = SpawnEnemyForServer(StrangerThings.CrustopikanLarvaeType, transform.position, 5f);
        if (crustopikanLarvaeEnemy != null)
        {
            crustopikanLarvaeEnemy.SetSyncedTarget(targetPlayer);
            _ = syncedEnemies.Add(crustopikanLarvaeEnemy);
        }

        if (syncedEnemies.Any(e => e.targetPlayer == null))
        {
            PlaySFXEveryoneRpc((int)Sound.ROAR);
            DoAnimationEveryoneRpc("startRoar");
            yield return this.WaitForFullAnimation("roar");

            foreach (UpsideDownEnemyAI upsideDownEnemy in syncedEnemies)
                upsideDownEnemy.ForceSend();
        }

        DoAnimationEveryoneRpc("startMove");
        SwitchToBehaviourClientRpc((int)State.CHASING);
        callCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayParticleEveryoneRpc(string tag, Vector3 position, Quaternion rotation, float scaleFactor)
        => LFCGlobalManager.PlayParticle(tag, position, rotation, scaleFactor: scaleFactor, active: DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));

    public void CancelCallCoroutine()
    {
        if (callCoroutine != null)
        {
            StopCoroutine(callCoroutine);
            callCoroutine = null;
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public IEnumerator GrabCoroutine()
    {
        agent.speed = 0f;
        PlaySFXEveryoneRpc((int)Sound.GRAB);
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
        GameObject gameObject = Instantiate(StrangerThings.RockProjectileObj, GrabPoint.position, GrabPoint.rotation);
        NetworkObject networkObject = gameObject.GetComponent<NetworkObject>();
        networkObject.Spawn();
        SpawnRockEveryoneRpc(networkObject);
        rockProjectile = gameObject.GetComponent<RockProjectile>();
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnRockEveryoneRpc(NetworkObjectReference obj)
    {
        if (obj.TryGet(out NetworkObject networkObject))
        {
            networkObject.transform.SetParent(GrabPoint, true);
            DimensionRegistry.SetInUpsideDown(networkObject.gameObject, true);
        }
    }

    public void CancelGrabCoroutine()
    {
        if (grabCoroutine != null)
        {
            StopCoroutine(grabCoroutine);
            grabCoroutine = null;
            rockProjectile?.ThrowFromPositionEveryoneRpc(thisNetworkObject, GrabPoint.transform.position, transform.position - GrabPoint.transform.position);
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
        bool hasTarget = this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer);
        lastSeenPosition = hasTarget && distanceWithPlayer <= 60f
            ? targetPlayer.transform.position
            : lastSeenPosition != Vector3.zero ? lastSeenPosition : transform.position;
        if (!hasTarget
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
        PlaySFXEveryoneRpc((int)Sound.THROW);
        DoAnimationEveryoneRpc("startThrow");
        yield return new WaitForSeconds(0.8f);

        rockProjectile?.ThrowFromPositionEveryoneRpc(thisNetworkObject, GrabPoint.transform.position, lastSeenPosition + (Vector3.up * 1.5f) - GrabPoint.transform.position);
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
            rockProjectile?.ThrowFromPositionEveryoneRpc(thisNetworkObject, GrabPoint.transform.position, transform.position - GrabPoint.transform.position);
            rockProjectile = null;
        }
    }

    public UpsideDownEnemyAI SpawnEnemyForServer(EnemyType enemyType, Vector3 position, float radius)
    {
        if (this.TryGetSafeRandomNavMeshPosition(position, radius, out Vector3 spawnPosition) && UnityEngine.Random.Range(minInclusive: 0, maxExclusive: 100) < 75)
        {
            GameObject gameObject = Instantiate(enemyType.enemyPrefab, spawnPosition, Quaternion.identity);
            NetworkObject networkObject = gameObject.GetComponentInChildren<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);
            return gameObject.GetComponentInChildren<UpsideDownEnemyAI>();
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
        PlaySFXEveryoneRpc((int)Sound.SWING);
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
        CancelActionsForServer();
        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlaySFXEveryoneRpc(int soundId)
    {
        if (CrustapikanSounds.TryGetValue((Sound)soundId, out AudioClip[] enemySounds) && enemySounds.Length > 0)
            creatureSFX.PlayOneShot(enemySounds[UnityEngine.Random.Range(0, enemySounds.Length)]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
