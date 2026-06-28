using HarmonyLib;
using StrangerThings.Behaviours.Enemies;
using UnityEngine;

namespace StrangerThings.Patches;

public class StartMatchLeverPatch
{
    [HarmonyPatch(typeof(StartMatchLever), nameof(StartMatchLever.EndGame))]
    [HarmonyPrefix]
    public static bool SMLEndGame()
    {
        Collider[] overlapBuffer = new Collider[64];
        int count = Physics.OverlapSphereNonAlloc(StartOfRound.Instance.shipAnimator.gameObject.transform.position, 20f, overlapBuffer, 524288, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            EnemyAI enemy = overlapBuffer[i].GetComponent<EnemyAICollisionDetect>()?.mainScript;
            if (enemy is HenryAI henry && !henry.isEnemyDead && henry.killCoroutine == null)
            {
                henry.StopShipServerRpc();
                return false;
            }
        }
        return true;
    }
}
