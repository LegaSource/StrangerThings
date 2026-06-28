using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class DemogorgonKidnapperAI : DemogorgonAI
{
    public Transform GrabPoint;
    public Transform CameraPivot;
    public Camera Camera;
    public Camera PlayerCamera;

    public Coroutine setCoroutine;
    public Coroutine dropCoroutine;

    protected UpsideDownPortal openedPortal;
    protected PlayerControllerB carriedPlayer;
    protected DeadBodyInfo fakeBody;

    protected override void OnDemogorgonStart() => PlayerCamera = LFCUtilities.LocalPlayer?.gameplayCamera;

    protected override void UpdateNonChasingTargetPlayer(PlayerControllerB player)
    {
        if (Camera.enabled && Camera == player.gameplayCamera && LFCUtilities.ShouldBeLocalPlayer(player))
        {
            Vector2 lookInput = player.playerActions.Movement.Look.ReadValue<Vector2>() * IngamePlayerSettings.Instance.settings.lookSensitivity * 0.008f;
            CameraPivot.Rotate(new Vector3(0f, lookInput.x, 0f));

            float verticalAngle = CameraPivot.localEulerAngles.x - lookInput.y;
            verticalAngle = (verticalAngle > 180f) ? (verticalAngle - 360f) : verticalAngle;
            verticalAngle = Mathf.Clamp(verticalAngle, -45f, 45f);
            CameraPivot.localEulerAngles = new Vector3(verticalAngle, CameraPivot.localEulerAngles.y, 0f);
        }
        if (player == carriedPlayer)
            player.transform.position = transform.position;
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
        PlaySFXEveryoneRpc((int)Sound.SET);
        yield return this.WaitForFullAnimation("setin");

        DoAnimationEveryoneRpc("startSet");
        yield return this.WaitForFullAnimation("set");

        openedPortal = MapObjectsManager.SpawnPortalForServer(transform.position, isOutside, isFake: true);
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
        if (closestPortal.isOutside != isOutside)
        {
            this.GoTowardsEntrance();
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
                if (Camera != null)
                    Camera.enabled = false;
                player.gameplayCamera = PlayerCamera;
            }

            if (LFCUtilities.IsServer)
            {
                LFCNetworkManager.Instance.TeleportPlayerEveryoneRpc((int)player.playerClientId, transform.position, isInElevator: false, isInHangarShipRoom: false, !isOutside);
                if (isInUpsideDown) CorruptPortalForServer(player);
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
        HashSet<UpsideDownPortal> upsideDownPortals = MapObjectsManager.GetUpsideDownPortals();
        if (upsideDownPortals.Count != 0)
        {
            UpsideDownPortal upsideDownPortal = MapObjectsManager.GetFurthestPortal(player.transform.position);
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
        PlaySFXEveryoneRpc((int)Sound.DIG);
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
            PlaySFXEveryoneRpc((int)Sound.ROAR);
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

        if (LFCUtilities.ShouldBeLocalPlayer(player) && Camera != null)
        {
            if (PlayerCamera == null)
                PlayerCamera = player.gameplayCamera;
            Camera.enabled = true;
            player.gameplayCamera = Camera;
        }
    }
}