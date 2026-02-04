using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class DemogorgonKidnapperAI : DemogorgonAI
{
    public Transform GrabPoint;
    public Transform cameraPivot;
    public Camera camera;
    public Camera playerCamera;

    public Coroutine setCoroutine;
    public Coroutine dropCoroutine;

    public UpsideDownPortal openedPortal;
    public PlayerControllerB carriedPlayer;
    public DeadBodyInfo fakeBody;

    protected override void OnDemogorgonStart()
        => playerCamera = LFCUtilities.LocalPlayer?.gameplayCamera;

    protected override void UpdateNonChasingTargetPlayer(PlayerControllerB player)
    {
        if (camera.enabled && camera == player.gameplayCamera && LFCUtilities.ShouldBeLocalPlayer(player))
        {
            Vector2 lookInput = player.playerActions.Movement.Look.ReadValue<Vector2>() * IngamePlayerSettings.Instance.settings.lookSensitivity * 0.008f;
            cameraPivot.Rotate(new Vector3(0f, lookInput.x, 0f));

            float verticalAngle = cameraPivot.localEulerAngles.x - lookInput.y;
            verticalAngle = (verticalAngle > 180f) ? (verticalAngle - 360f) : verticalAngle;
            verticalAngle = Mathf.Clamp(verticalAngle, -45f, 45f);
            cameraPivot.localEulerAngles = new Vector3(verticalAngle, cameraPivot.localEulerAngles.y, 0f);
        }
    }

    public override void CancelActionsForServer()
    {
        base.CancelActionsForServer();

        if (LFCUtilities.IsServer)
        {
            CancelSetCoroutine();
            CancelDropCoroutine();
        }
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
        if (!DimensionRegistry.IsInUpsideDown(gameObject))
        {
            StopSearch(currentSearch);
            SwitchToBehaviourClientRpc((int)State.PORTALING);
            return;
        }
        PlayerControllerB player = CheckForNormalWorldPlayer();
        if (player != null && !DimensionRegistry.IsInUpsideDown(player.gameObject))
        {
            targetPlayer = player;
            StopSearch(currentSearch);
            setCoroutine ??= StartCoroutine(SetCoroutine());
        }
    }

    public PlayerControllerB CheckForNormalWorldPlayer()
    {
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (PlayerIsTargetable(player) && Vector3.Distance(player.transform.position, transform.position) < 15f)
                return player;
        }
        return null;
    }

    public IEnumerator SetCoroutine()
    {
        agent.speed = 0f;

        DoAnimationEveryoneRpc("startSetIn");
        PlayAudioEveryoneRpc((int)Sound.SET);
        yield return this.WaitForFullAnimation("setin");

        DoAnimationEveryoneRpc("startSet");
        yield return this.WaitForFullAnimation("set");

        openedPortal = MapObjectsManager.SpawnUpsideDownPortalForServer(transform.position, isOutside, isFake: true);
        DoAnimationEveryoneRpc("startSetOut");
        yield return this.WaitForFullAnimation("setout");
        yield return DigCoroutine(DimensionRegistry.IsInUpsideDown(targetPlayer.gameObject), openedPortal);

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
        }
    }

    public override void DoPortaling()
    {
        if (dropCoroutine != null || portalingCoroutine != null) return;

        agent.speed = 7f;
        if (!IsFleeing() && carriedPlayer == null && this.FoundClosestPlayerInRange(25, 10))
        {
            closestPortal = null;
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        closestPortal ??= openedPortal ?? MapObjectsManager.GetClosestPortal(transform.position);
        if (closestPortal == null)
        {
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
        if (Vector3.Distance(transform.position, closestPortal.transform.position) <= 1f)
        {
            if (carriedPlayer != null) dropCoroutine ??= StartCoroutine(DropCoroutine(carriedPlayer));
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
        yield return DigCoroutine(isInUpsideDown: true, closestPortal);

        targetPlayer = player;
        DoAnimationEveryoneRpc("startMove");
        SwitchToBehaviourClientRpc((int)State.CHASING);

        if (openedPortal != null)
        {
            Destroy(openedPortal.gameObject);
            openedPortal = null;
        }
        dropCoroutine = null;
    }

    public void CancelDropCoroutine()
    {
        if (dropCoroutine != null)
        {
            StopCoroutine(dropCoroutine);
            dropCoroutine = null;
        }
        if (carriedPlayer != null)
            DropPlayerEveryoneRpc((int)carriedPlayer.playerClientId, isInUpsideDown: false);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DropPlayerEveryoneRpc(int playerId, bool isInUpsideDown = true)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
        carriedPlayer = null;

        if (!player.isPlayerDead)
        {
            LFCVisibilityRegistry.Restore(player.gameObject, $"{StrangerThings.modName}Demogorgon");
            player.inSpecialInteractAnimation = false;
            player.inAnimationWithEnemy = null;
            player.ResetZAndXRotation();

            if (LFCUtilities.ShouldBeLocalPlayer(player))
            {
                if (camera != null) camera.enabled = false;
                player.gameplayCamera = playerCamera;
            }

            if (LFCUtilities.IsServer)
            {
                if (isInUpsideDown) CorruptPortalForServer(player);
                LFCNetworkManager.Instance.TeleportPlayerEveryoneRpc((int)player.playerClientId, transform.position, isInElevator: false, isInHangarShipRoom: false, !isOutside);
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

    public void CorruptPortalForServer(PlayerControllerB player)
    {
        UpsideDownPortal[] upsideDownPortals = MapObjectsManager.GetUpsideDownPortals();
        if (upsideDownPortals.Length != 0)
        {
            UpsideDownPortal upsideDownPortal = upsideDownPortals[Random.Range(0, upsideDownPortals.Length)];
            upsideDownPortal.CorruptPortalForServer(player);
        }
    }

    public IEnumerator PortalingCoroutine(bool isInUpsideDown)
    {
        agent.speed = 0f;
        yield return DigCoroutine(isInUpsideDown, closestPortal);

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

        if (openedPortal != null)
        {
            Destroy(openedPortal.gameObject);
            openedPortal = null;
        }
        portalingCoroutine = null;
    }

    public IEnumerator DigCoroutine(bool isInUpsideDown, UpsideDownPortal targetedPortal)
    {
        DoAnimationEveryoneRpc("startDig");
        PlayAudioEveryoneRpc((int)Sound.DIG);
        yield return this.WaitForFullAnimation("dig");

        DoAnimationEveryoneRpc("startDigIn");
        yield return this.WaitForFullAnimation("digin");

        LFCNetworkManager.Instance.TeleportEnemyEveryoneRpc(thisNetworkObject, targetedPortal.transform.position, targetedPortal.isOutside);
        if (DimensionRegistry.IsInUpsideDown(gameObject) != isInUpsideDown)
        {
            StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(thisNetworkObject, isInUpsideDown);
            if (isInUpsideDown) RestoreEnemyHealthEveryoneRpc();
        }

        DoAnimationEveryoneRpc("startDigOut");
        yield return this.WaitForFullAnimation("digout");

        closestPortal = null;
    }

    public override void StopDash(PlayerControllerB player = null)
    {
        agent.speed = 0f;
        isDashing = false;
        DoAnimationEveryoneRpc(player == null || DimensionRegistry.IsInUpsideDown(gameObject) ? "startRecover" : "startGrab");
        stopDashCoroutine ??= StartCoroutine(StopDashCoroutine(player));
    }

    public override IEnumerator StopDashCoroutine(PlayerControllerB player)
    {
        if (player == null || DimensionRegistry.IsInUpsideDown(gameObject))
        {
            if (player != null)
                LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            PlayAudioEveryoneRpc((int)Sound.ROAR);
            yield return this.WaitForFullAnimation("recover");
            DoAnimationEveryoneRpc("startMove");
        }
        else
        {
            GrabPlayerEveryoneRpc((int)player.playerClientId);
            yield return this.WaitForFullAnimation("grab");
            DoAnimationEveryoneRpc("startCarry");
        }

        if (carriedPlayer != null)
            SwitchToBehaviourClientRpc((int)State.PORTALING);
        CancelDashCoroutine();
        stopDashCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void GrabPlayerEveryoneRpc(int playerId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
        carriedPlayer = player;

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
        LFCVisibilityRegistry.Hide(player.gameObject, $"{StrangerThings.modName}Demogorgon");

        GameObject fakeBodyObj = Instantiate(StartOfRound.Instance.playerRagdolls[0], GrabPoint.position, GrabPoint.rotation);
        Vector3 originalScale = fakeBodyObj.transform.localScale;
        fakeBodyObj.transform.SetParent(GrabPoint, true);
        fakeBody = fakeBodyObj.GetComponent<DeadBodyInfo>();
        fakeBody.playerObjectId = playerId;
        fakeBody.attachedTo = GrabPoint;
        fakeBody.attachedLimb = fakeBody.bodyParts[5];
        fakeBody.matchPositionExactly = false;
        fakeBody.seenByLocalPlayer = true;

        Vector3 correction = new Vector3(1f / GrabPoint.lossyScale.x, 1f / GrabPoint.lossyScale.y, 1f / GrabPoint.lossyScale.z);
        fakeBodyObj.transform.localScale = new Vector3(originalScale.x * correction.x, originalScale.y * correction.y, originalScale.z * correction.z);

        if (fakeBody.gameObject.TryGetComponentInChildren(out ScanNodeProperties scanNode) && scanNode.TryGetComponent(out Collider collider))
            collider.enabled = false;

        if (LFCUtilities.ShouldBeLocalPlayer(player) && camera != null)
        {
            if (playerCamera == null) playerCamera = player.gameplayCamera;
            camera.enabled = true;
            player.gameplayCamera = camera;
        }
    }
}