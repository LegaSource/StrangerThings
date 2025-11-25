using GameNetcodeStuff;
using StrangerThings.Registries;
using System.Collections.Generic;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts;

public class UpsideDownHiveMindController : MonoBehaviour
{
    public static UpsideDownHiveMindController Instance;

    private readonly HashSet<EnemyAI> enemies = [];
    private PlayerControllerB currentTarget;

    private readonly float broadcastInterval = 20f;
    private float timer;

    private void Awake() => Instance = this;

    public void AddEnemy(EnemyAI enemy) => enemies.Add(enemy);

    public void RemoveEnemy(EnemyAI enemy) => enemies.Remove(enemy);

    public void ReportTarget(PlayerControllerB player)
    {
        if (player != null && currentTarget == null && DimensionRegistry.IsInUpsideDown(player.gameObject))
        {
            currentTarget = player;

            /*UpsideDownPortal upsideDownPortal = DimensionRegistry.GetClosestPortal(player.transform.position);
            foreach (EnemyAI enemy in enemies)
            {
                if (enemy != null && !enemy.isEnemyDead && enemy.IsSpawned && enemy.targetPlayer == null)
                    StrangerThingsNetworkManager.Instance.TeleportEnemyEveryoneRpc(enemy.thisNetworkObject, upsideDownPortal.transform.position);
            }*/
        }
    }

    private void Update()
    {
        if (currentTarget == null || enemies.Count == 0) return;
        if (!DimensionRegistry.IsInUpsideDown(currentTarget.gameObject))
        {
            currentTarget = null;
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = broadcastInterval;
            BroadcastTarget();
        }
    }

    private void BroadcastTarget()
    {
        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || enemy.targetPlayer != null || enemy.isOutside == currentTarget.isInsideFactory || !DimensionRegistry.IsInUpsideDown(enemy.gameObject))
                continue;

            _ = enemy.SetDestinationToPosition(currentTarget.transform.position);
        }
    }
}
