using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Registries;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.MapObjects;

public class VinesZone : NetworkBehaviour
{
    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly float AoERadius = 100f;
    private readonly int AoEMask = 524288;

    public void Start() => DimensionRegistry.SetInUpsideDown(gameObject, true);

    private void OnTriggerEnter(Collider collider)
    {
        if (collider != null && collider.TryGetComponent(out PlayerControllerB player) && LFCUtilities.ShouldBeLocalPlayer(player))
        {
            LFCCustomPassManager.SetupScreenFilter(StrangerThings.ZoneFilterMat, $"{StrangerThings.modName}{GetInstanceID()}");

            int count = Physics.OverlapSphereNonAlloc(player.transform.position, AoERadius, overlapBuffer, AoEMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                EnemyAI enemy = overlapBuffer[i].GetComponent<EnemyAICollisionDetect>()?.mainScript;
                if (enemy == null || enemy == this || enemy.isEnemyDead || !DimensionRegistry.AreInSameDimension(gameObject, enemy.gameObject)) continue;
                if (enemy is not UpsideDownEnemyAI upsideDownEnemy || Vector3.Distance(upsideDownEnemy.transform.position, transform.position) > upsideDownEnemy.syncDistance) continue;

                upsideDownEnemy.SetSyncedTarget(player);
                upsideDownEnemy.ForceSend();
            }
        }
    }

    private void OnTriggerExit(Collider collider)
    {
        if (collider != null && collider.TryGetComponent(out PlayerControllerB player) && LFCUtilities.ShouldBeLocalPlayer(player))
            LFCCustomPassManager.RemoveFiltersByTag($"{StrangerThings.modName}{GetInstanceID()}");
    }
}
