using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using UnityEngine;

namespace StrangerThings.Patches;

public static class JesterAIPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(JesterAI), nameof(JesterAI.Update))]
    private static void CaptureTimer(ref float ___noPlayersToChaseTimer, ref float __state) => __state = ___noPlayersToChaseTimer;

    [HarmonyPostfix]
    [HarmonyAfter("AudioKnight.StarlancerAIFix")]
    [HarmonyPatch(typeof(JesterAI), nameof(JesterAI.Update))]
    private static void UpdateJester(ref JesterAI __instance, ref bool ___targetingPlayer, ref float ___noPlayersToChaseTimer, float __state)
    {
        if (LFCUtilities.IsServer && __instance.currentBehaviourStateIndex == 2)
        {
            if (__instance.targetPlayer != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __instance.targetPlayer.gameObject))
                __instance.targetPlayer = null;

            ___targetingPlayer = false;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.isPlayerControlled && player.isInsideFactory != __instance.isOutside && DimensionRegistry.AreInSameDimension(__instance.gameObject, player.gameObject))
                {
                    ___targetingPlayer = true;
                    break;
                }
            }

            float timer = __state;
            if (!___targetingPlayer)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    __instance.SwitchToBehaviourState(0);
            }
            else
            {
                timer = 5f;
            }

            ___noPlayersToChaseTimer = timer;
        }
    }
}
