using GameNetcodeStuff;
using LegaFusionCore.Registries;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public abstract class UpsideDownEnemyAI : EnemyAI
{
    protected CrustapikanAI caller;
    protected PlayerControllerB syncedTarget;

    public float callDistance = 50f;

    public virtual void SetCaller(CrustapikanAI caller, PlayerControllerB syncedTarget)
    {
        this.caller = caller;
        this.syncedTarget = syncedTarget;
        this.caller.syncedEnemies.Add(this);
    }

    public virtual void RemoveCaller()
    {
        if (caller != null)
        {
            _ = caller.syncedEnemies.Remove(this);
            caller = null;
            syncedTarget = null;
        }
    }

    public abstract void ForceSync();
    public abstract void ForceSend();

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

        bool hasHitHiveMind = false;
        foreach (EnemyAI enemy in LFCSpawnRegistry.GetAllAs<EnemyAI>())
        {
            if (enemy != null && !enemy.isEnemyDead && enemy is UpsideDownEnemyAI && enemy != this)
            {
                if (!hasHitHiveMind && enemy is CrustapikanAI && Vector3.Distance(enemy.transform.position, transform.position) < 50f)
                {
                    hasHitHiveMind = true;
                    enemy.enemyHP -= force;
                    if (enemy.enemyHP <= 0)
                    {
                        if (enemy.IsOwner)
                            enemy.KillEnemyOnOwnerClient();
                        continue;
                    }
                }
                enemy.SetEnemyStunned(true);
            }
        }

        enemyHP -= force;
        if (enemyHP <= 0 && IsOwner) KillEnemyOnOwnerClient();
    }
}
