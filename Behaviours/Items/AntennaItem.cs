using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.MapObjects;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Items;

public class AntennaItem : Shovel
{
    public AudioSource AntennaAudio;
    public int antennaHitForce = 3;
    public RaycastHit[] objectsHitByAntenna;
    public List<RaycastHit> objectsHitByAntennaList = [];

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (playerHeldBy != null)
        {
            isHoldingButton = buttonDown;
            if (!reelingUp && buttonDown)
            {
                reelingUp = true;
                previousPlayerHeldBy = playerHeldBy;
                if (reelingUpCoroutine != null)
                {
                    StopCoroutine(reelingUpCoroutine);
                    reelingUpCoroutine = null;
                }
                reelingUpCoroutine = StartCoroutine(ReelUpAntenna());
            }
        }
    }

    private IEnumerator ReelUpAntenna()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger("shovelHit");
        playerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: true);
        ReelUpSFXEveryoneRpc();
        yield return new WaitForSeconds(0.35f);
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        SwingAntenna(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        HitAntenna(!isHeld);
        yield return new WaitForSeconds(0.3f);
        reelingUp = false;
        reelingUpCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ReelUpSFXEveryoneRpc() => AntennaAudio.PlayOneShot(reelUp);

    public void SwingAntenna(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            AntennaAudio.PlayOneShot(swing);
            previousPlayerHeldBy.UpdateSpecialAnimationValue(specialAnimation: true, (short)previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
        }
    }

    public void HitAntenna(bool cancel = false)
    {
        if (previousPlayerHeldBy == null)
        {
            StrangerThings.mls.LogError("Previousplayerheldby is null on this client when HitAntenna is called");
            return;
        }
        previousPlayerHeldBy.activatingItem = false;
        bool hitDetected = false;
        int footstepSurfaceIndex = -1;
        if (!cancel)
        {
            HashSet<ulong> affectedIds = [];
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByAntenna = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + (previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f), 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, shovelMask, QueryTriggerInteraction.Collide);
            objectsHitByAntennaList = objectsHitByAntenna.OrderBy((RaycastHit x) => x.distance).ToList();

            foreach (RaycastHit antennaHit in objectsHitByAntennaList)
            {
                if (antennaHit.transform.gameObject.layer == 8 || antennaHit.transform.gameObject.layer == 11)
                {
                    if (antennaHit.collider.isTrigger) continue;

                    hitDetected = true;
                    for (int i = 0; i < StartOfRound.Instance.footstepSurfaces.Length; i++)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[i].surfaceTag == antennaHit.collider.gameObject.tag)
                        {
                            footstepSurfaceIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    if (!antennaHit.transform.TryGetComponent(out IHittable component) || antennaHit.transform == previousPlayerHeldBy.transform) continue;
                    if (!(antennaHit.point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, antennaHit.point, out RaycastHit _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)) continue;

                    hitDetected = true;
                    if (antennaHit.transform.TryGetComponent(out EnemyAICollisionDetect enemyCollision) && enemyCollision.mainScript != null && affectedIds.Add(enemyCollision.mainScript.NetworkObjectId))
                        _ = component.Hit(antennaHitForce, previousPlayerHeldBy.gameplayCamera.transform.forward, previousPlayerHeldBy, playHitSFX: true, 1);
                    else if (antennaHit.transform.TryGetComponent(out PlayerControllerB player) && affectedIds.Add(LFCUtilities.EncodePlayerId(player.playerClientId)))
                        _ = component.Hit(antennaHitForce, previousPlayerHeldBy.gameplayCamera.transform.forward, previousPlayerHeldBy, playHitSFX: true, 1);
                }
            }
        }
        if (hitDetected)
        {
            _ = RoundManager.PlayRandomClip(AntennaAudio, hitSFX);
            FindObjectOfType<RoundManager>().PlayAudibleNoise(transform.position, 17f, 0.8f);
            HitAntennaEveryoneRpc(footstepSurfaceIndex);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void HitAntennaEveryoneRpc(int hitSurfaceID)
    {
        _ = RoundManager.PlayRandomClip(AntennaAudio, hitSFX);
        if (hitSurfaceID != -1)
        {
            AntennaAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
            WalkieTalkie.TransmitOneShotAudio(AntennaAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        }
        DestroyObjectInHand(playerHeldBy);
    }

    public override void ItemInteractLeftRight(bool right)
    {
        base.ItemInteractLeftRight(right);

        if (!right && LFCUtilities.ShouldBeLocalPlayer(playerHeldBy) && !reelingUp)
        {
            if (DimensionRegistry.IsInUpsideDown(playerHeldBy.gameObject))
            {
                HUDManager.Instance.DisplayTip("Impossible action", "Antennas can only be activated in the real world.");
                return;
            }
            SpawnAntennaHazardServerRpc((int)playerHeldBy.playerClientId, playerHeldBy.gameplayCamera.transform.position + playerHeldBy.gameplayCamera.transform.forward);
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SpawnAntennaHazardServerRpc(int playerId, Vector3 position)
    {
        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
        {
            Quaternion rotation = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>().transform.rotation;
            GameObject gameObject = Instantiate(StrangerThings.AntennaHazardObj, hit.point, Quaternion.Euler(0f, rotation.eulerAngles.y, rotation.eulerAngles.z), RoundManager.Instance.mapPropsContainer.transform);
            NetworkObject spawnedNetworkObject = gameObject.GetComponent<NetworkObject>();
            spawnedNetworkObject.Spawn(true);
            SpawnAntennaHazardEveryoneRpc(playerId, spawnedNetworkObject);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnAntennaHazardEveryoneRpc(int playerId, NetworkObjectReference obj)
    {
        if (obj.TryGet(out NetworkObject networkObject))
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();

            isBeingUsed = true;
            this.DropHeldObject(player);
            EnablePhysics(enable: false);
            transform.SetParent(null);
            targetFloorPosition = new Vector3(3000f, -400f, 3000f);
            startFallingPosition = new Vector3(3000f, -400f, 3000f);

            AntennaHazard antennaHazard = networkObject.gameObject.GetComponentInChildren<AntennaHazard>();
            antennaHazard.antennaItem = this;
            antennaHazard.previousPlayerHeldBy = player;
            MapObjectsManager.AddAntenna(antennaHazard);
            LFCMapObjectsManager.AttachMapObjectForEveryone(player, antennaHazard.gameObject);
        }
    }

    public override void EquipItem()
    {
        base.EquipItem();
        playerHeldBy.equippedUsableItemQE = true;
    }

    public override void PocketItem()
    {
        base.PocketItem();
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
            playerHeldBy.equippedUsableItemQE = false;
        }
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null)
        {
            playerHeldBy.activatingItem = false;
            playerHeldBy.equippedUsableItemQE = false;
        }
        base.DiscardItem();
    }
}
