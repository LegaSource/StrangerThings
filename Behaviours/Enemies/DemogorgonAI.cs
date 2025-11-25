using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class DemogorgonAI : EnemyAI
{
    public Transform TurnCompass;
    public Transform GrabPoint;
    public Transform cameraPivot;
    public Camera camera;
    public Camera playerCamera;

    public AudioClip[] FootstepSounds = Array.Empty<AudioClip>();
    public AudioClip[] GrowlSounds = Array.Empty<AudioClip>();
    public AudioClip SetSound;
    public AudioClip DigSound;
    public AudioClip ScreamSound;
    public AudioClip ChargeSound;
    public AudioClip DashSound;
    public AudioClip SwingSound;
    public AudioClip RoarSound;
    public AudioClip DieSound;

    public UpsideDownPortal closestPortal;
    public DeadBodyInfo fakeBody;

    public float footstepTimer = 0f;
    public float growlTimer = 0f;
    public float setTimer = 0f;
    public float dashTimer = 0f;
    public float huntTimer = 0f;

    public float setCooldown = 30f;
    public float dashCooldown = 10f;
    public float huntDuration = 30f;

    public bool canSet = false;
    public bool canDash = false;
    public bool isHunting = false;
    public bool isDashing = false;
    public bool isCarrying = false;

    public Coroutine setCoroutine;
    public Coroutine portalingCoroutine;
    public Coroutine dashCoroutine;
    public Coroutine dropCoroutine;
    public Coroutine swingCoroutine;
    public Coroutine killCoroutine;

    public enum State
    {
        WANDERING,
        PORTALING,
        CHASING
    }

    public override void Start()
    {
        base.Start();

        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
        playerCamera = player.gameplayCamera;

        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);

        if (LFCUtilities.IsServer && DimensionRegistry.upsideDownPortals.Count < 4)
            SpawnPortals();
    }

    public void SpawnPortals()
    {
        const float minDistance = 50f;
        List<Vector3> selectedPositions = [];
        StartOfRound.Instance.allPlayerScripts.Where(p => !p.isPlayerDead).ToList().ForEach(p => selectedPositions.Add(p.transform.position));

        LFCUtilities.Shuffle(RoundManager.Instance.outsideAINodes);
        LFCUtilities.Shuffle(RoundManager.Instance.insideAINodes);

        for (int i = 0; i < 10; i++)
        {
            float maxDistance = float.MinValue;
            Vector3 bestPosition = Vector3.zero;
            GameObject lastNodeSaved = null;

            // Déterminer si ce portail est à l'extérieur ou à l'intérieur
            bool isOutside = new System.Random().Next(0, 2) == 1;
            List<GameObject> nodes = (isOutside ? RoundManager.Instance.outsideAINodes : RoundManager.Instance.insideAINodes).ToList();
            float radius = isOutside ? 10f : 2f;

            foreach (GameObject node in nodes)
            {
                Vector3 candidatePosition = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(node.transform.position, radius, default, new System.Random()) + Vector3.up;
                if (!Physics.Raycast(candidatePosition, Vector3.down, out RaycastHit hit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault)) continue;

                Vector3 validPosition = hit.point;

                // Calculer la distance minimale avec les positions sélectionnées
                float minDistanceToSelected = selectedPositions.Count > 0
                    ? selectedPositions.Min(p => Vector3.Distance(p, validPosition))
                    : float.MaxValue;

                // Garder la position la plus éloignée des autres sélectionnées
                if (minDistanceToSelected > minDistance || minDistanceToSelected > maxDistance)
                {
                    maxDistance = minDistanceToSelected;
                    bestPosition = validPosition;
                    lastNodeSaved = node;

                    if (minDistanceToSelected > minDistance) break;
                }
            }

            if (bestPosition != Vector3.zero)
            {
                selectedPositions.Add(bestPosition);
                _ = nodes.Remove(lastNodeSaved);

                DimensionRegistry.SpawnUpsideDownPortalForServer(bestPosition, isOutside);
            }
        }
    }

    public override void Update()
    {
        if (killCoroutine != null) return;
        base.Update();

        creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
        if (stunNormalizedTimer > 0f)
        {
            agent.speed = 0f;
            if (stunnedByPlayer != null)
            {
                targetPlayer = stunnedByPlayer;
                StopSearch(currentSearch);
                SwitchToBehaviourClientRpc((int)State.CHASING);
            }
            return;
        }
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
        if (!canSet)
        {
            setTimer += Time.deltaTime;
            if (setTimer >= setCooldown)
            {
                canSet = true;
                setTimer = 0f;
                setCooldown = 60f;
            }
        }
        if (!canDash)
        {
            dashTimer += Time.deltaTime;
            if (dashTimer >= dashCooldown)
            {
                canDash = true;
                dashTimer = 0f;
            }
        }
        if (isHunting)
        {
            huntTimer += Time.deltaTime;
            if (huntTimer >= huntDuration)
            {
                isHunting = false;
                huntTimer = 0f;
            }
        }
    }

    public void PlayFootstepSound()
    {
        if (agent.velocity.magnitude < 0.1f || isDashing) return;

        footstepTimer -= Time.deltaTime;
        if (FootstepSounds.Length > 0 && footstepTimer <= 0)
        {
            creatureSFX.PlayOneShot(FootstepSounds[UnityEngine.Random.Range(0, FootstepSounds.Length)]);
            footstepTimer = 0.45f;
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

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)State.WANDERING:
                DoWandering();
                break;
            case (int)State.PORTALING:
                DoPortaling();
                break;
            case (int)State.CHASING:
                DoChasing();
                break;
        }
    }

    public void DoWandering()
    {
        if (setCoroutine != null) return;

        agent.speed = 3f;
        if (FoundClosestPlayerInRange(25, 10))
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

    private bool FoundClosestPlayerInRange(int range, int senseRange)
    {
        PlayerControllerB player = CheckLineOfSightForPlayer(60f, range, senseRange);
        return player != null && PlayerIsTargetable(player) && (bool)(targetPlayer = player);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetEveryoneRpc(int playerId) => setCoroutine ??= StartCoroutine(SetCoroutine(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>()));

    public IEnumerator SetCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        creatureAnimator.SetTrigger("startSetIn");
        creatureSFX.PlayOneShot(SetSound);
        yield return WaitForFullAnimation("setin");

        creatureAnimator.SetTrigger("startSet");
        yield return WaitForFullAnimation("set");

        DimensionRegistry.SpawnUpsideDownPortalForServer(transform.position, isOutside);

        creatureAnimator.SetTrigger("startSetOut");
        yield return WaitForFullAnimation("setout");

        closestPortal = DimensionRegistry.GetClosestPortal(player.transform.position);
        yield return DigCoroutine(closestPortal.transform.position, closestPortal.isOutside, false);

        targetPlayer = player;
        creatureAnimator.SetTrigger("startChase");
        SwitchToBehaviourStateOnLocalClient((int)State.CHASING);

        setCoroutine = null;
    }

    public void DoPortaling()
    {
        if (dropCoroutine != null || portalingCoroutine != null) return;

        agent.speed = 6f;
        if (closestPortal == null) closestPortal = DimensionRegistry.GetClosestPortal(transform.position);
        if (Vector3.Distance(transform.position, closestPortal.transform.position) <= 1f)
        {
            if (isCarrying) DropEveryoneRpc((int)targetPlayer.playerClientId);
            else PortalingEveryoneRpc(closestPortal.transform.position, closestPortal.isOutside, !DimensionRegistry.IsInUpsideDown(gameObject));
            return;
        }
        _ = SetDestinationToPosition(closestPortal.transform.position);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DropEveryoneRpc(int playerId) => dropCoroutine ??= StartCoroutine(DropCoroutine(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>()));

    private IEnumerator DropCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        creatureAnimator.SetTrigger("startDrop");

        yield return WaitForFullAnimation("drop");

        DropPlayer(player, closestPortal.transform.position);
        yield return DigCoroutine(closestPortal.transform.position, closestPortal.isOutside, true);

        targetPlayer = player;
        creatureAnimator.SetTrigger("startChase");
        SwitchToBehaviourStateOnLocalClient((int)State.CHASING);

        dropCoroutine = null;
    }

    public void DropPlayer(PlayerControllerB player, Vector3 position)
    {
        isCarrying = false;

        if (!player.isPlayerDead)
        {
            if (LFCUtilities.ShouldBeLocalPlayer(player))
            {
                camera.enabled = false;
                player.gameplayCamera = playerCamera;
            }
            player.DisablePlayerModel(player.gameObject, enable: true, disableLocalArms: true);
            player.inSpecialInteractAnimation = false;
            player.inAnimationWithEnemy = null;
            player.ResetZAndXRotation();
            DimensionRegistry.SetUpsideDown(player.gameObject, true);

            if (LFCUtilities.IsServer)
                LFCNetworkManager.Instance.TeleportPlayerEveryoneRpc((int)player.playerClientId, position, false, false, !isOutside);
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

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PortalingEveryoneRpc(Vector3 position, bool isOutside, bool isInUpsideDown) => portalingCoroutine ??= StartCoroutine(PortalingCoroutine(position, isOutside, isInUpsideDown));

    private IEnumerator PortalingCoroutine(Vector3 position, bool isOutside, bool isInUpsideDown)
    {
        agent.speed = 0f;
        yield return DigCoroutine(position, isOutside, isInUpsideDown);

        if (LFCUtilities.IsServer)
        {
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
        }

        portalingCoroutine = null;
    }

    private IEnumerator DigCoroutine(Vector3 position, bool isOutside, bool isInUpsideDown)
    {
        creatureAnimator.SetTrigger("startDig");
        creatureSFX.PlayOneShot(DigSound);
        yield return WaitForFullAnimation("dig");

        creatureAnimator.SetTrigger("startDigIn");
        yield return WaitForFullAnimation("digin");

        LFCNetworkManager.Instance.TeleportEnemyEveryoneRpc(thisNetworkObject, position, isOutside);
        if (DimensionRegistry.IsInUpsideDown(gameObject) != isInUpsideDown)
            DimensionRegistry.SetUpsideDown(gameObject, isInUpsideDown);

        creatureAnimator.SetTrigger("startDigOut");

        GameObject audioObj = new GameObject("ScreamAudio");
        audioObj.transform.parent = GameNetworkManager.Instance.localPlayerController.transform;
        audioObj.transform.localPosition = Vector3.forward * 50f;
        AudioSource audioSource = audioObj.AddComponent<AudioSource>();
        audioSource.clip = ScreamSound;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 10f;
        audioSource.maxDistance = 200f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.Play();
        Destroy(audioObj, ScreamSound.length);

        yield return WaitForFullAnimation("digout");

        closestPortal = null;
    }

    public void DoChasing()
    {
        if (dashCoroutine != null || swingCoroutine != null || killCoroutine != null) return;

        agent.speed = 6f;
        float distanceWithPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        if (this.TargetOutsideChasedPlayer()) return;
        if (targetPlayer != null && !DimensionRegistry.AreInSameDimension(gameObject, targetPlayer.gameObject))
        {
            SwitchToBehaviourClientRpc((int)State.PORTALING);
            return;
        }
        if (!TargetClosestPlayerInAnyCase() || (!isHunting && distanceWithPlayer > 30 && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
        {
            StartSearch(transform.position);
            DoAnimationEveryoneRpc("startWalk");
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            return;
        }
        if (canDash && distanceWithPlayer <= 12f && distanceWithPlayer >= 4 && CheckLineOfSightForPosition(targetPlayer.transform.position))
        {
            canDash = false;
            DashEveryoneRpc((int)targetPlayer.playerClientId);
            return;
        }
        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    public bool TargetClosestPlayerInAnyCase()
    {
        mostOptimalDistance = 2000f;
        targetPlayer = null;
        for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            if (!PlayerIsTargetable(player)) continue;

            tempDist = Vector3.Distance(transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
            if (tempDist < mostOptimalDistance)
            {
                mostOptimalDistance = tempDist;
                targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
            }
        }
        return targetPlayer != null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DashEveryoneRpc(int playerId) => dashCoroutine ??= StartCoroutine(DashCoroutine(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>()));

    public IEnumerator DashCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        creatureAnimator.SetTrigger("startCharge");
        creatureSFX.PlayOneShot(ChargeSound);
        yield return WaitForFullAnimation("charge");

        creatureAnimator.SetTrigger("startDash");
        creatureSFX.PlayOneShot(DashSound);

        isDashing = true;
        agent.speed = 24f;
        agent.angularSpeed = 0f;
        agent.acceleration = 100f;

        Vector3 dashDirection = (player.transform.position - transform.position).normalized;
        float dashTime = 0.6f;
        float timer = 0f;
        while (timer < dashTime && isDashing)
        {
            timer += Time.deltaTime;
            agent.velocity = dashDirection * agent.speed;
            yield return null;
        }

        StopDash();
    }

    private void StopDash(PlayerControllerB player = null)
    {
        if (isDashing)
        {
            agent.speed = 0f;
            isDashing = false;
            creatureAnimator.ResetTrigger("startDash");
            creatureAnimator.SetTrigger(player == null || DimensionRegistry.IsInUpsideDown(gameObject) ? "startRecover" : "startGrab");
            _ = StartCoroutine(StartMovingAfterDash(player));
        }
    }

    private IEnumerator StartMovingAfterDash(PlayerControllerB player)
    {
        if (player == null || DimensionRegistry.IsInUpsideDown(gameObject))
        {
            creatureSFX.PlayOneShot(RoarSound);
            yield return WaitForFullAnimation("recover");
            creatureAnimator.SetTrigger("startChase");
        }
        else
        {
            GrabPlayer(player);
            yield return WaitForFullAnimation("grab");
            creatureAnimator.SetTrigger("startCarry");

            if (LFCUtilities.ShouldBeLocalPlayer(player))
            {
                camera.enabled = true;
                player.gameplayCamera = camera;
            }
        }

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        agent.speed = 6f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.velocity = Vector3.zero;

        if (isCarrying) SwitchToBehaviourStateOnLocalClient((int)State.PORTALING);
    }

    public void GrabPlayer(PlayerControllerB player)
    {
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

        ApplyDoorHitDamage(networkObject.gameObject, rb.velocity);
    }

    private IEnumerator ReleaseDoor(Rigidbody rb, Vector3 force)
    {
        yield return new WaitForFixedUpdate();
        rb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForFixedUpdate();
        rb.useGravity = true;
    }

    private void ApplyDoorHitDamage(GameObject doorPiece, Vector3 velocity)
    {
        if (velocity.magnitude < 3f) return;

        Collider collider = doorPiece.GetComponentInChildren<Collider>();
        Collider[] hits = Physics.OverlapBox(collider.bounds.center, collider.bounds.extents * 1.2f, doorPiece.transform.rotation);
        foreach (Collider hit in hits)
        {
            PlayerControllerB player = hit.GetComponentInParent<PlayerControllerB>();
            if (player == null) continue;

            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, (int)Mathf.Clamp(velocity.magnitude * 4f, 1f, 15f));
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (currentBehaviourStateIndex != (int)State.CHASING || swingCoroutine != null || killCoroutine != null) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        if (isDashing)
        {
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80);
            StopDashEveryoneRpc((int)player.playerClientId);
        }
        if (dashCoroutine == null) SwingEveryoneRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void StopDashEveryoneRpc(int playerId) => StopDash(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>());

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SwingEveryoneRpc(int playerId) => swingCoroutine ??= StartCoroutine(SwingCoroutine(StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>()));

    public IEnumerator SwingCoroutine(PlayerControllerB player)
    {
        agent.speed = 0f;
        creatureAnimator.SetTrigger("startSwing");
        creatureSFX.PlayOneShot(SwingSound);
        yield return WaitForFullAnimation("swing");

        player.DamagePlayer(20, hasDamageSFX: true, callRPC: true, CauseOfDeath.Crushing);
        creatureAnimator.SetTrigger("startRoar");
        creatureSFX.PlayOneShot(RoarSound);
        yield return WaitForFullAnimation("roar");

        creatureAnimator.SetTrigger("startChase");
        agent.speed = 6f;

        swingCoroutine = null;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (isEnemyDead || !DimensionRegistry.IsInUpsideDown(gameObject)) return;
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

        enemyHP -= force;
        if (enemyHP <= 0 && IsOwner) KillEnemyOnOwnerClient();
    }

    public override void KillEnemy(bool destroy = false) => killCoroutine = StartCoroutine(KillEnemyCoroutine(destroy));

    public IEnumerator KillEnemyCoroutine(bool destroy)
    {
        creatureAnimator.SetTrigger("startKill");
        creatureSFX.PlayOneShot(DieSound);

        yield return WaitForFullAnimation("kill");
        yield return new WaitForSeconds(1f);

        LFCGlobalManager.PlayParticle($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.darkExplosionParticle.name}", transform.position, Quaternion.Euler(-90, 0, 0));
        LFCGlobalManager.PlayAudio($"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.darkExplosionAudio.name}", transform.position);
        base.KillEnemy(destroy);
    }

    private IEnumerator WaitForFullAnimation(string clipName, float maxDuration = 10, int layer = 0)
    {
        float timer = 0f;

        // Attendre que le clip démarre
        while (true)
        {
            AnimatorClipInfo[] clip = creatureAnimator.GetCurrentAnimatorClipInfo(layer);
            if (clip.Length > 0 && clip[0].clip.name.Contains(clipName)) break;

            timer += Time.deltaTime;
            if (timer > maxDuration) yield break;
            yield return null;
        }

        // Attendre fin du clip
        while (creatureAnimator.GetCurrentAnimatorStateInfo(layer).normalizedTime < 1f)
        {
            timer += Time.deltaTime;
            if (timer > maxDuration) yield break;
            yield return null;
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
