using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Behaviours.Items.Figurines;

public class ElevenPop : FigurinePop
{
    private HenryAI aimedEnemy;
    private PlayerControllerB aimedPlayer;

    private readonly string auraTag = $"{StrangerThings.modName}ElevenPopAimed";
    private readonly float auraRadius = 5f;
    private readonly int auraMask = 1084754248;
    private readonly Collider[] overlapBuffer = new Collider[64];

    public override void GrabItem()
    {
        base.GrabItem();
        LFCCustomPassManager.RemoveAuraFromObject(gameObject, $"{StrangerThings.modName}ElevenPop");
    }

    public override void Update()
    {
        base.Update();

        if (onCooldown || !isHeld || isPocketed || !LFCUtilities.ShouldBeLocalPlayer(playerHeldBy))
        {
            ClearAuras();
            return;
        }

        int count = Physics.OverlapSphereNonAlloc(playerHeldBy.transform.position, auraRadius, overlapBuffer, auraMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (collider == null || !DimensionRegistry.AreInSameDimension(playerHeldBy.gameObject, collider.gameObject))
                continue;

            if (collider.gameObject.TryGetComponentInParent(out EnemyAICollisionDetect collisionDetect) && collisionDetect.mainScript is HenryAI henry && !henry.isEnemyDead && henry.NetworkObject != null && aimedEnemy != henry)
            {
                SetAimedTarget(henry, Color.red, ref aimedEnemy);
                return;
            }

            if (collider.gameObject.TryGetComponentInParent(out PlayerControllerB player) && !player.isPlayerDead && player != playerHeldBy && aimedPlayer != player)
            {
                SetAimedTarget(player, Color.yellow, ref aimedPlayer);
                return;
            }
        }

        ClearAuras();
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (buttonDown && !onCooldown && playerHeldBy != null)
        {
            if (aimedEnemy != null)
            {
                aimedEnemy.KillEnemyServerRpc(destroy: true);
                StartChronoEveryoneRpc(cooldown: 120);
            }
            // if (aimedPlayer != null && aimedPlayer in dimension X)
        }
    }

    private void SetAimedTarget<T>(T target, Color color, ref T aimedTarget) where T : Component
    {
        ClearAuras();
        aimedTarget = target;
        LFCCustomPassManager.SetupAuraForObjects([target.gameObject], LegaFusionCore.LegaFusionCore.transparentShader, auraTag, color);
    }

    private void ClearAuras()
    {
        if (aimedEnemy?.gameObject != null)
        {
            LFCCustomPassManager.RemoveAuraFromObject(aimedEnemy.gameObject, auraTag);
            aimedEnemy = null;
        }
        if (aimedPlayer?.gameObject != null)
        {
            LFCCustomPassManager.RemoveAuraFromObject(aimedPlayer.gameObject, auraTag);
            aimedPlayer = null;
        }
    }
}
