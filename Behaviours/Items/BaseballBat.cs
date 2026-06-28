using GameNetcodeStuff;
using LegaFusionCore.Utilities;
using LethalStatus.Managers;
using LethalStatus.StatusEffects;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Items;

public class BaseballBat : UpsideDownObject
{
    public AudioSource BaseballBatAudio;
    public AudioClip ReelUp;
    public AudioClip Swing;
    public AudioClip[] HitSFX;

    public int baseballBatHitForce = 1;
    public bool reelingUp;
    public bool isHoldingButton;
    private Coroutine reelingUpCoroutine;
    private RaycastHit[] objectsHitByBaseballBat;
    private List<RaycastHit> objectsHitByBaseballBatList = [];
    private PlayerControllerB previousPlayerHeldBy;

    private readonly int baseballBatMask = 1084754248;

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
                reelingUpCoroutine = StartCoroutine(ReelUpBaseballBat());
            }
        }
    }

    private IEnumerator ReelUpBaseballBat()
    {
        playerHeldBy.activatingItem = true;
        playerHeldBy.twoHanded = true;
        playerHeldBy.playerBodyAnimator.ResetTrigger("shovelHit");
        playerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: true);
        ReelUpSFXEveryoneRpc();
        yield return new WaitForSeconds(0.35f);
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        SwingBaseballBat(!isHeld);
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        HitBaseballBat(!isHeld);
        yield return new WaitForSeconds(0.3f);
        reelingUp = false;
        reelingUpCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ReelUpSFXEveryoneRpc() => BaseballBatAudio.PlayOneShot(ReelUp);

    public void SwingBaseballBat(bool cancel = false)
    {
        previousPlayerHeldBy.playerBodyAnimator.SetBool("reelingUp", value: false);
        if (!cancel)
        {
            BaseballBatAudio.PlayOneShot(Swing);
            previousPlayerHeldBy.UpdateSpecialAnimationValue(specialAnimation: true, (short)previousPlayerHeldBy.transform.localEulerAngles.y, 0.4f);
        }
    }

    public void HitBaseballBat(bool cancel = false)
    {
        if (previousPlayerHeldBy == null)
        {
            StrangerThings.mls.LogError("Previousplayerheldby is null on this client when HitBaseballBat is called");
            return;
        }
        previousPlayerHeldBy.activatingItem = false;
        bool hitDetected = false;
        int footstepSurfaceIndex = -1;
        if (!cancel)
        {
            HashSet<ulong> affectedIds = [];
            previousPlayerHeldBy.twoHanded = false;
            objectsHitByBaseballBat = Physics.SphereCastAll(previousPlayerHeldBy.gameplayCamera.transform.position + (previousPlayerHeldBy.gameplayCamera.transform.right * -0.35f), 0.8f, previousPlayerHeldBy.gameplayCamera.transform.forward, 1.5f, baseballBatMask, QueryTriggerInteraction.Collide);
            objectsHitByBaseballBatList = objectsHitByBaseballBat.OrderBy((RaycastHit x) => x.distance).ToList();

            foreach (RaycastHit baseballBatHit in objectsHitByBaseballBatList)
            {
                if (baseballBatHit.transform.gameObject.layer == 8 || baseballBatHit.transform.gameObject.layer == 11)
                {
                    if (baseballBatHit.collider.isTrigger) continue;

                    hitDetected = true;
                    for (int i = 0; i < StartOfRound.Instance.footstepSurfaces.Length; i++)
                    {
                        if (StartOfRound.Instance.footstepSurfaces[i].surfaceTag == baseballBatHit.collider.gameObject.tag)
                        {
                            footstepSurfaceIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    if (!baseballBatHit.transform.TryGetComponent(out IHittable component) || baseballBatHit.transform == previousPlayerHeldBy.transform) continue;
                    if (!(baseballBatHit.point == Vector3.zero) && Physics.Linecast(previousPlayerHeldBy.gameplayCamera.transform.position, baseballBatHit.point, out RaycastHit _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)) continue;

                    hitDetected = true;
                    if (baseballBatHit.transform.TryGetComponent(out EnemyAICollisionDetect enemyCollision) && enemyCollision.mainScript != null && affectedIds.Add(enemyCollision.mainScript.NetworkObjectId))
                    {
                        LSNetworkManager.Instance.ApplyStatusEveryoneRpc((int)playerHeldBy.playerClientId, enemyCollision.mainScript.NetworkObject, (int)LSStatusEffectRegistry.StatusEffectType.BLEEDING, 2, 20);
                        _ = component.Hit(baseballBatHitForce, previousPlayerHeldBy.gameplayCamera.transform.forward, previousPlayerHeldBy, playHitSFX: true, 1);
                    }
                    else if (baseballBatHit.transform.TryGetComponent(out PlayerControllerB player) && affectedIds.Add(LFCUtilities.EncodePlayerId(player.playerClientId)))
                    {
                        LSNetworkManager.Instance.ApplyStatusEveryoneRpc((int)playerHeldBy.playerClientId, (int)player.playerClientId, (int)LSStatusEffectRegistry.StatusEffectType.BLEEDING, 2, 2);
                        _ = component.Hit(baseballBatHitForce, previousPlayerHeldBy.gameplayCamera.transform.forward, previousPlayerHeldBy, playHitSFX: true, 1);
                    }
                }
            }
        }
        if (hitDetected)
        {
            _ = RoundManager.PlayRandomClip(BaseballBatAudio, HitSFX);
            FindObjectOfType<RoundManager>().PlayAudibleNoise(transform.position, 17f, 0.8f);
            HitBaseballBatEveryoneRpc(footstepSurfaceIndex);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void HitBaseballBatEveryoneRpc(int hitSurfaceID)
    {
        _ = RoundManager.PlayRandomClip(BaseballBatAudio, HitSFX);
        if (hitSurfaceID != -1)
        {
            BaseballBatAudio.PlayOneShot(StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
            WalkieTalkie.TransmitOneShotAudio(BaseballBatAudio, StartOfRound.Instance.footstepSurfaces[hitSurfaceID].hitSurfaceSFX);
        }
    }
}
