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
    public int antennaHitForce = 3;
    public RaycastHit[] objectsHitByAntenna;
    public List<RaycastHit> objectsHitByAntennaList = [];
    public AudioSource antennaAudio;

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
    public void ReelUpSFXEveryoneRpc() => antennaAudio.PlayOneShot(reelUp);

    public void SwingAntenna(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            antennaAudio.PlayOneShot(swing);
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
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByAntenna = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + (previousPlayerHeldBy.gameplayCamera.transform.right * 0.1f), 0.5f, previousPlayerHeldBy.gameplayCamera.transform.forward, 0.75f, shovelMask, QueryTriggerInteraction.Collide);
            objectsHitByAntennaList = objectsHitByAntenna.OrderBy((RaycastHit x) => x.distance).ToList();

            foreach (RaycastHit antennaHit in objectsHitByAntennaList)
            {
                if (antennaHit.transform.gameObject.layer == 8 || antennaHit.transform.gameObject.layer == 11)
                {
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
                    if (!(antennaHit.point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, antennaHit.point, out RaycastHit hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault)) continue;

                    hitDetected = true;
                    _ = component.Hit(antennaHitForce, previousPlayerHeldBy.gameplayCamera.transform.forward, previousPlayerHeldBy, playHitSFX: true, 5);

                }
            }
        }
        if (hitDetected)
        {
            _ = RoundManager.PlayRandomClip(antennaAudio, hitSFX);
            FindObjectOfType<RoundManager>().PlayAudibleNoise(transform.position, 17f, 0.8f);
            HitAntennaEveryoneRpc(footstepSurfaceIndex);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void HitAntennaEveryoneRpc(int hitSurfaceID)
    {
        _ = RoundManager.PlayRandomClip(antennaAudio, hitSFX);
        if (hitSurfaceID != -1)
        {
            antennaAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
            WalkieTalkie.TransmitOneShotAudio(antennaAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
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
            GameObject gameObject = Instantiate(StrangerThings.antennaHazard, hit.point, Quaternion.Euler(0f, rotation.eulerAngles.y, rotation.eulerAngles.z), RoundManager.Instance.mapPropsContainer.transform);
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
            MapObjectsManager.AddAntennaHazards(antennaHazard);
            LFCMapObjectsManager.AttachMapObjectForEveryone(player, antennaHazard.gameObject);
        }
    }

    public override void UseUpBatteries()
    {
        base.UseUpBatteries();
        insertedBattery = new Battery(isEmpty: true, chargeNumber: 0f);
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
