using GameNetcodeStuff;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public abstract class UpsideDownEnemyAI : EnemyAI
{
    protected PlayerControllerB syncedTarget;

    public bool isSynced = false;
    private float syncTimer = 0f;
    public float syncDuration = 30f;
    public float syncDistance = 50f;

    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly float AoERadius = 100f;
    private readonly int AoEMask = 524288;

    public override void Update()
    {
        base.Update();
        LFCUtilities.UpdateTimer(ref syncTimer, syncDuration, isSynced, () => { isSynced = false; syncedTarget = null; });
    }

    public virtual void SetSyncedTarget(PlayerControllerB syncedTarget)
    {
        isSynced = true;
        this.syncedTarget = syncedTarget;
    }
    public abstract void ForceSend();

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

        bool isCrustapikanClose = false;
        // Si hors Upside Down (Demogorgon), on ne vérifie pas les monstres proches
        if (!DimensionRegistry.IsInUpsideDown(gameObject))
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, AoERadius, overlapBuffer, AoEMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                EnemyAI enemy = overlapBuffer[i].GetComponent<EnemyAICollisionDetect>()?.mainScript;
                if (enemy != null && enemy != this && !enemy.isEnemyDead && enemy is CrustapikanAI)
                {
                    isCrustapikanClose = true;
                    enemy.enemyHP -= force;
                    if (enemy.enemyHP <= 0)
                    {
                        if (enemy.IsOwner)
                            enemy.KillEnemyOnOwnerClient();
                        break;
                    }
                    enemy.SetEnemyStunned(true);
                    break;
                }
            }
        }

        // Si pas de Crustapikan proche, tout le monde est stun
        if (!isCrustapikanClose)
        {
            foreach (EnemyAI enemy in LFCSpawnRegistry.GetAllAs<EnemyAI>())
            {
                if (enemy != null && !enemy.isEnemyDead && enemy is UpsideDownEnemyAI && enemy != this)
                    enemy.SetEnemyStunned(true);
            }
        }

        enemyHP -= force;
        if (enemyHP <= 0 && IsOwner) KillEnemyOnOwnerClient();
    }

    public abstract void CancelActionsForServer();

    public override void OnNetworkDespawn()
    {
        CancelActionsForServer();
        base.OnNetworkDespawn();
    }
}
