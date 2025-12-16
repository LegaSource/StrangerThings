using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class DemogorgonAI : UpsideDownEnemyAI
{
    public Transform TurnCompass;
    public Transform GrabPoint;
    public Transform cameraPivot;
    public Camera camera;
    public Camera playerCamera;

    public AudioClip[] FootstepSounds = Array.Empty<AudioClip>();
    public AudioClip[] GrowlSounds = Array.Empty<AudioClip>();
    public AudioClip[] DemogorgonSounds = Array.Empty<AudioClip>();
    public AudioClip ScreamSound;
    public AudioClip DieSound;

    public float footstepTimer = 0f;
    public float growlTimer = 0f;
    public float setTimer = 30f;
    public float dashTimer = 0f;
    public float huntTimer = 0f;

    public float setCooldown = 60f;
    public float dashCooldown = 10f;
    public float huntDuration = 30f;

    public bool canSet = false;
    public bool canDash = false;
    public bool isHunting = false;
    public bool isDashing = false;
    public bool isCarrying = false;

    public Coroutine stunCoroutine;
    public Coroutine setCoroutine;
    public Coroutine dropCoroutine;
    public Coroutine portalingCoroutine;
    public Coroutine dashCoroutine;
    public Coroutine stopDashCoroutine;
    public Coroutine swingCoroutine;
    public Coroutine killCoroutine;

    public UpsideDownPortal closestPortal;
    public DeadBodyInfo fakeBody;

    public enum State
    {
        WANDERING,
        PORTALING,
        SYNCING,
        CHASING
    }

    public enum Sound
    {
        SET,
        DIG,
        CHARGE,
        DASH,
        SWING,
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
            SetMovingTowardsTargetPlayer(syncedTarget);
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }
    }

    public override void Start()
    {
        base.Start();

        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        playerCamera = player.gameplayCamera;

        callDistance = 60f;
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);
        DimensionRegistry.SpawnPortalsForServer();
    }

    public override void Update()
    {
        base.Update();
        if (killCoroutine != null || stunCoroutine != null) return;

        PlayFootstepSound();
        PlayGrowlSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null)
        {
            if (state == (int)State.CHASING)
            {
                TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
            }
            else if (camera.enabled && camera == targetPlayer.gameplayCamera && LFCUtilities.ShouldBeLocalPlayer(targetPlayer))
            {
                Vector2 lookInput = targetPlayer.playerActions.Movement.Look.ReadValue<Vector2>() * IngamePlayerSettings.Instance.settings.lookSensitivity * 0.008f;
                cameraPivot.Rotate(new Vector3(0f, lookInput.x, 0f));

                // Rotation verticale avec clamping
                float verticalAngle = cameraPivot.localEulerAngles.x - lookInput.y;
                verticalAngle = (verticalAngle > 180f) ? (verticalAngle - 360f) : verticalAngle;
                verticalAngle = Mathf.Clamp(verticalAngle, -45f, 45f);
                cameraPivot.localEulerAngles = new Vector3(verticalAngle, cameraPivot.localEulerAngles.y, 0f);
            }
        }
        LFCUtilities.UpdateTimer(ref setTimer, setCooldown, !canSet, () => canSet = true);
        LFCUtilities.UpdateTimer(ref dashTimer, dashCooldown, !canDash, () => canDash = true);
        LFCUtilities.UpdateTimer(ref huntTimer, huntDuration, isHunting, () => isHunting = false);
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
                footstepTimer = 0.45f;
            }
        }
    }

    public void PlayGrowlSound()
    {
        growlTimer -= Time.deltaTime;
        if (GrowlSounds.Length > 0 && growlTimer <= 0)
        {
            creatureSFX.PlayOneShot(GrowlSounds[UnityEngine.Random.Range(0, GrowlSounds.Length)]);
            growlTimer = 4f;
        }
    }

    public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 3.958f, PlayerControllerB setStunnedByPlayer = null)
    {
        if (LFCUtilities.IsServer && setToStunned && stunCoroutine == null && stopDashCoroutine == null && killCoroutine == null)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            stunCoroutine = StartCoroutine(StunCoroutine());
        }
    }

    public IEnumerator StunCoroutine()
    {
        CancelSetCoroutine();
        CancelDropCoroutine();
        CancelPortalingCoroutine();
        CancelDashCoroutine();
        CancelSwingCoroutine();

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
            case (int)State.PORTALING:
                if (stunnedByPlayer != null || targetPlayer != null)
                {
                    targetPlayer ??= stunnedByPlayer;
                    closestPortal = null;
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
            case (int)State.PORTALING: DoPortaling(); break;
            case (int)State.SYNCING: DoSyncing(); break;
            case (int)State.CHASING: DoChasing(); break;
        }
    }

    public void DoWandering()
    {
        if (setCoroutine != null) return;

        agent.speed = 3f;
        if (this.FoundClosestPlayerInRange(25, 10))
        {
            StopSearch(currentSearch);
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (DimensionRegistry.IsInUpsideDown(gameObject))
        {
            StopSearch(currentSearch);
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.PORTALING);
        }
    }

    public IEnumerator SetCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        canSet = false;

        DoAnimationEveryoneRpc("startSetIn");
        PlayAudioEveryoneRpc((int)Sound.SET);
        yield return this.WaitForFullAnimation("setin");

        DoAnimationEveryoneRpc("startSet");
        yield return this.WaitForFullAnimation("set");

        DimensionRegistry.SpawnUpsideDownPortalForServer(transform.position, isOutside);

        DoAnimationEveryoneRpc("startSetOut");
        yield return this.WaitForFullAnimation("setout");

        closestPortal = DimensionRegistry.GetClosestPortal(player.transform.position);
        yield return DigCoroutine(false);

        targetPlayer = player;
        DoAnimationEveryoneRpc("startChase");
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

    public void DoPortaling()
    {
        if (dropCoroutine != null || portalingCoroutine != null) return;

        agent.speed = 6f;
        if (!isCarrying && this.FoundClosestPlayerInRange(25, 10))
        {
            closestPortal = null;
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        closestPortal ??= DimensionRegistry.GetClosestPortal(transform.position);
        if (closestPortal == null)
        {
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (Vector3.Distance(transform.position, closestPortal.transform.position) <= 1f)
        {
            if (isCarrying) dropCoroutine ??= StartCoroutine(DropCoroutine(targetPlayer));
            else portalingCoroutine ??= StartCoroutine(PortalingCoroutine(!DimensionRegistry.IsInUpsideDown(gameObject)));
            return;
        }
        _ = SetDestinationToPosition(closestPortal.transform.position);
    }

    public IEnumerator DropCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startDrop");
        yield return this.WaitForFullAnimation("drop");

        DropPlayerEveryoneRpc((int)player.playerClientId);
        yield return DigCoroutine(true);

        targetPlayer = player;
        DoAnimationEveryoneRpc("startChase");
        SwitchToBehaviourClientRpc((int)State.CHASING);

        dropCoroutine = null;
    }

    public void CancelDropCoroutine()
    {
        if (dropCoroutine != null)
        {
            StopCoroutine(dropCoroutine);
            dropCoroutine = null;
        }
        if (isCarrying)
            DropPlayerEveryoneRpc((int)targetPlayer.playerClientId, isInUpsideDown: false);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DropPlayerEveryoneRpc(int playerId, bool isInUpsideDown = true)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
        isCarrying = false;

        if (!player.isPlayerDead)
        {
            player.DisablePlayerModel(player.gameObject, enable: true, disableLocalArms: true);
            player.inSpecialInteractAnimation = false;
            player.inAnimationWithEnemy = null;
            player.ResetZAndXRotation();

            if (LFCUtilities.IsServer)
            {
                if (isInUpsideDown)
                    StrangerThingsNetworkManager.Instance.SetPlayerInUpsideDownEveryoneRpc((int)player.playerClientId);
                LFCNetworkManager.Instance.TeleportPlayerEveryoneRpc((int)player.playerClientId, transform.position, false, false, !isOutside);
            }
            if (LFCUtilities.ShouldBeLocalPlayer(player))
            {
                camera.enabled = false;
                player.gameplayCamera = playerCamera;
            }
        }

        if (fakeBody != null)
        {
            fakeBody.attachedTo = null;
            fakeBody.attachedLimb = null;
            fakeBody.matchPositionExactly = false;

            Destroy(fakeBody.gameObject, 0.1f);
            fakeBody = null;
        }
    }

    public IEnumerator PortalingCoroutine(bool isInUpsideDown)
    {
        agent.speed = 0f;
        yield return DigCoroutine(isInUpsideDown);

        if (targetPlayer == null)
        {
            StartSearch(transform.position);
            DoAnimationEveryoneRpc("startWalk");
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }
        else
        {
            DoAnimationEveryoneRpc("startChase");
            SwitchToBehaviourClientRpc((int)State.CHASING);
        }

        portalingCoroutine = null;
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

    public IEnumerator DigCoroutine(bool isInUpsideDown)
    {
        DoAnimationEveryoneRpc("startDig");
        PlayAudioEveryoneRpc((int)Sound.DIG);
        yield return this.WaitForFullAnimation("dig");

        DoAnimationEveryoneRpc("startDigIn");
        yield return this.WaitForFullAnimation("digin");

        LFCNetworkManager.Instance.TeleportEnemyEveryoneRpc(thisNetworkObject, closestPortal.transform.position, closestPortal.isOutside);
        if (DimensionRegistry.IsInUpsideDown(gameObject) != isInUpsideDown)
            StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(thisNetworkObject);

        DoAnimationEveryoneRpc("startDigOut");
        PlayScreamAudioEveryoneRpc();
        yield return this.WaitForFullAnimation("digout");

        closestPortal = null;
    }

    public void DoSyncing()
    {
        if (caller == null || caller.isEnemyDead)
        {
            RemoveCaller();
            StartSearch(transform.position);
            DoAnimationEveryoneRpc("startWalk");
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }
        agent.speed = Vector3.Distance(transform.position, caller.transform.position) < 10f ? 3f : 6f;
        _ = SetDestinationToPosition(caller.transform.position);
    }

    public void DoChasing()
    {
        if (swingCoroutine != null || killCoroutine != null) return;

        if (dashCoroutine == null)
        {
            agent.speed = 6f;
            if (this.TargetOutsideChasedPlayer()) return;
            if (targetPlayer != null && !DimensionRegistry.AreInSameDimension(gameObject, targetPlayer.gameObject))
            {
                SwitchToBehaviourClientRpc((int)State.PORTALING);
                return;
            }
            float distanceWithPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (!this.TargetClosestPlayerInAnyCase() || (!isHunting && distanceWithPlayer > 30f && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
            {
                StartSearch(transform.position);
                DoAnimationEveryoneRpc("startWalk");
                SwitchToBehaviourClientRpc((int)State.WANDERING);
                return;
            }
            if (canDash && distanceWithPlayer <= 12f && distanceWithPlayer >= 4f && CheckLineOfSightForPosition(targetPlayer.transform.position))
            {
                canDash = false;
                dashCoroutine ??= StartCoroutine(DashCoroutine());
                return;
            }
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public IEnumerator DashCoroutine()
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startCharge");
        PlayAudioEveryoneRpc((int)Sound.CHARGE);
        yield return this.WaitForFullAnimation("charge");

        DoAnimationEveryoneRpc("startDash");
        PlayAudioEveryoneRpc((int)Sound.DASH);

        isDashing = true;
        agent.speed = 24f;
        agent.angularSpeed = 0f;
        agent.acceleration = 100f;

        Vector3 dashDirection = (targetPlayer.transform.position - transform.position).normalized;
        float dashTime = 0.6f;
        float timer = 0f;
        while (timer < dashTime && isDashing)
        {
            timer += Time.deltaTime;
            agent.velocity = dashDirection * agent.speed;
            yield return null;
        }

        yield return new WaitForSeconds(0.35f);
        if (isDashing) StopDash();
    }

    public void StopDash(PlayerControllerB player = null)
    {
        agent.speed = 0f;
        isDashing = false;
        ResetAnimationEveryoneRpc("startDash");
        DoAnimationEveryoneRpc(player == null || DimensionRegistry.IsInUpsideDown(gameObject) ? "startRecover" : "startGrab");
        stopDashCoroutine ??= StartCoroutine(StopDashCoroutine(player));
    }

    public IEnumerator StopDashCoroutine(PlayerControllerB player)
    {
        if (player == null || DimensionRegistry.IsInUpsideDown(gameObject))
        {
            if (player != null) LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            PlayAudioEveryoneRpc((int)Sound.ROAR);
            yield return this.WaitForFullAnimation("recover");
            DoAnimationEveryoneRpc("startChase");
        }
        else
        {
            GrabPlayerEveryoneRpc((int)player.playerClientId);
            yield return this.WaitForFullAnimation("grab");
            DoAnimationEveryoneRpc("startCarry");
        }

        CancelDashCoroutine();
        if (isCarrying) SwitchToBehaviourClientRpc((int)State.PORTALING);
        stopDashCoroutine = null;
    }

    public void CancelDashCoroutine()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
            isDashing = false;
            ResetDashMovement();
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void GrabPlayerEveryoneRpc(int playerId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
        isCarrying = true;

        if (player.inSpecialInteractAnimation && player.currentTriggerInAnimationWith != null)
            player.currentTriggerInAnimationWith.CancelAnimationExternally();

        player.isCrouching = false;
        player.playerBodyAnimator.SetBool("crouching", value: false);
        player.inSpecialInteractAnimation = true;
        player.inAnimationWithEnemy = this;
        player.isInElevator = false;
        player.isInHangarShipRoom = false;
        player.ResetZAndXRotation();
        player.DropAllHeldItems();
        player.DisablePlayerModel(player.gameObject);

        GameObject fakeBodyObj = Instantiate(StartOfRound.Instance.playerRagdolls[0], GrabPoint.position, GrabPoint.rotation);
        Vector3 originalScale = fakeBodyObj.transform.localScale;

        fakeBodyObj.transform.SetParent(GrabPoint, true);
        fakeBody = fakeBodyObj.GetComponent<DeadBodyInfo>();
        fakeBody.attachedTo = GrabPoint;
        fakeBody.attachedLimb = fakeBody.bodyParts[5];
        fakeBody.matchPositionExactly = false;
        fakeBody.seenByLocalPlayer = true;

        // Correction taille du corps
        Vector3 correction = new Vector3(1f / GrabPoint.lossyScale.x, 1f / GrabPoint.lossyScale.y, 1f / GrabPoint.lossyScale.z);
        fakeBodyObj.transform.localScale = new Vector3(originalScale.x * correction.x, originalScale.y * correction.y, originalScale.z * correction.z);

        ScanNodeProperties scanNode = fakeBody.gameObject.GetComponentInChildren<ScanNodeProperties>();
        if (scanNode != null && scanNode.TryGetComponent(out Collider collider)) collider.enabled = false;

        if (LFCUtilities.ShouldBeLocalPlayer(player))
        {
            camera.enabled = true;
            player.gameplayCamera = camera;
        }
    }

    public void ResetDashMovement()
    {
        agent.speed = 6f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.velocity = Vector3.zero;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void BreakDoorEveryoneRpc(NetworkObjectReference obj, Vector3 direction)
    {
        if (!obj.TryGet(out NetworkObject networkObject)) return;

        GameObject door = networkObject.gameObject;
        door.transform.position += direction * 0.5f;

        _ = door.AddComponent<DoorProjectile>();
        Rigidbody rb = door.AddComponent<Rigidbody>();
        rb.useGravity = false;

        _ = StartCoroutine(ReleaseDoor(rb, direction));

        AnimatedObjectTrigger objectTrigger = networkObject.gameObject.GetComponentInChildren<AnimatedObjectTrigger>();
        objectTrigger?.PlayAudio(objectTrigger.boolValue, true);
    }

    public IEnumerator ReleaseDoor(Rigidbody rb, Vector3 force)
    {
        yield return new WaitForFixedUpdate();
        rb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForFixedUpdate();
        rb.useGravity = true;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (currentBehaviourStateIndex != (int)State.CHASING || killCoroutine != null) return;
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
        PlayAudioEveryoneRpc((int)Sound.SWING);
        yield return this.WaitForFullAnimation("swing");

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 20, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        DoAnimationEveryoneRpc("startRoar");
        PlayAudioEveryoneRpc((int)Sound.ROAR);
        yield return this.WaitForFullAnimation("roar");

        DoAnimationEveryoneRpc("startChase");
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
        if (!isEnemyDead && DimensionRegistry.IsInUpsideDown(gameObject))
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
    }

    public override void KillEnemy(bool destroy = false)
    {
        RemoveCaller();
        killCoroutine = StartCoroutine(KillEnemyCoroutine(destroy));
    }

    public IEnumerator KillEnemyCoroutine(bool destroy)
    {
        creatureAnimator.SetTrigger("startKill");
        creatureSFX.PlayOneShot(DieSound);

        yield return this.WaitForFullAnimation("kill");
        yield return new WaitForSeconds(1f);

        LFCGlobalManager.PlayParticle($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.darkExplosionParticle.name}", transform.position, Quaternion.Euler(-90, 0, 0));
        LFCGlobalManager.PlayAudio($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.darkExplosionAudio.name}", transform.position);
        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayScreamAudioEveryoneRpc()
    {
        GameObject audioObj = new GameObject("ScreamAudio");
        audioObj.transform.parent = GameNetworkManager.Instance.localPlayerController.transform;
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

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayAudioEveryoneRpc(int enemySound)
    {
        if (DemogorgonSounds.Length > 0)
            creatureSFX.PlayOneShot(DemogorgonSounds[enemySound]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ResetAnimationEveryoneRpc(string animationState) => creatureAnimator.ResetTrigger(animationState);

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}