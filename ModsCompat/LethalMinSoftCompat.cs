using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System;
using System.Reflection;

namespace StrangerThings.ModsCompat;

public static class LethalMinSoftCompat
{
    public static void Patch(Harmony harmony)
    {
        Type pikminAIType = Type.GetType("LethalMin.PikminAI, NoteBoxz.LethalMin");
        if (pikminAIType != null)
        {
            MethodInfo start = AccessTools.Method(pikminAIType, "Start");
            if (start != null)
            {
                HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(LethalMinSoftCompat), nameof(Start)));
                _ = harmony.Patch(start, postfix: postfix);
            }
        }
    }

    private static void Start(object __instance)
    {
        if (__instance is EnemyAI enemy && !DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, enemy.gameObject))
            DimensionRegistry.UpdateVisibilityState(enemy.gameObject);
    }
}
