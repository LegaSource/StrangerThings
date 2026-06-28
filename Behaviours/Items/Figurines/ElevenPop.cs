using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Behaviours.Items.Figurines;

public class ElevenPop : FigurinePop
{
    public HenryAI aimedEnemy;
    public PlayerControllerB aimedPlayer;

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
            RemoveAuraFromEnemy(auraTag, ref aimedEnemy);
            RemoveAuraFromPlayer(auraTag, ref aimedPlayer);
            return;
        }

        int count = Physics.OverlapSphereNonAlloc(playerHeldBy.transform.position, auraRadius, overlapBuffer, auraMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (collider == null || !DimensionRegistry.AreInSameDimension(playerHeldBy.gameObject, collider.gameObject))
                continue;

            if (collider.gameObject.TryGetComponentInParent(out PlayerControllerB player) && !player.isPlayerDead && player != playerHeldBy && aimedPlayer != player)
            {
                RemoveAuraFromEnemy(auraTag, ref aimedEnemy);
                RemoveAuraFromPlayer(auraTag, ref aimedPlayer);

                aimedPlayer = player;
                LFCCustomPassManager.SetupAuraForObjects([player.gameObject], LegaFusionCore.LegaFusionCore.transparentShader, auraTag, Color.yellow);
                return;
            }

            if (collider.gameObject.TryGetComponentInParent(out EnemyAICollisionDetect collisionDetect) && collisionDetect.mainScript is HenryAI henry && !henry.isEnemyDead && henry.NetworkObject != null && aimedEnemy != henry)
            {
                RemoveAuraFromEnemy(auraTag, ref aimedEnemy);
                RemoveAuraFromPlayer(auraTag, ref aimedPlayer);

                aimedEnemy = henry;
                LFCCustomPassManager.SetupAuraForObjects([henry.gameObject], LegaFusionCore.LegaFusionCore.transparentShader, auraTag, Color.red);
                return;
            }
        }

        RemoveAuraFromEnemy(auraTag, ref aimedEnemy);
        RemoveAuraFromPlayer(auraTag, ref aimedPlayer);
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (buttonDown && !onCooldown && playerHeldBy != null)
        {
            if (aimedEnemy != null && aimedEnemy.enemyHP <= 1)
            {
                aimedEnemy.KillEnemyServerRpc(destroy: true);
                StartChronoEveryoneRpc(cooldown: 120);
            }
            // if (aimedPlayer != null && aimedPlayer in dimension X)
        }
    }

    public void RemoveAuraFromEnemy(string tag, ref HenryAI aimedEnemy)
    {
        if (aimedEnemy != null)
        {
            LFCCustomPassManager.RemoveAuraFromObject(aimedEnemy.gameObject, tag);
            aimedEnemy = null;
        }
    }

    public static void RemoveAuraFromPlayer(string tag, ref PlayerControllerB aimedPlayer)
    {
        if (aimedPlayer != null)
        {
            LFCCustomPassManager.RemoveAuraFromObject(aimedPlayer.gameObject, tag);
            aimedPlayer = null;
        }
    }
}
