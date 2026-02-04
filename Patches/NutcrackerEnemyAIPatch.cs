using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class NutcrackerEnemyAIPatch
{
    [HarmonyPatch(typeof(NutcrackerEnemyAI), nameof(NutcrackerEnemyAI.CheckLineOfSightForLocalPlayer))]
    [HarmonyPostfix]
    private static void PreventCheckLineOfSightForLocalPlayer(NutcrackerEnemyAI __instance, ref bool __result)
    {
        if (__result && LFCUtilities.LocalPlayer != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, LFCUtilities.LocalPlayer.gameObject))
            __result = false;
    }

    [HarmonyPatch(typeof(NutcrackerEnemyAI), nameof(NutcrackerEnemyAI.TurnTorsoToTargetDegrees))]
    [HarmonyPostfix]
    private static void TurnTorsoAudio(NutcrackerEnemyAI __instance)
    {
        if (!DimensionRegistry.AreInSameDimension(__instance.gameObject, LFCUtilities.LocalPlayer?.gameObject))
            __instance.torsoTurnAudio.volume = 0f;
    }
}
