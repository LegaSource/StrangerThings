using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StrangerThings.ModsCompat;

public static class SpectateEnemySoftCompat
{
    private static FieldInfo fiSpectatedEnemyIndex;
    private static FieldInfo fiSpectatingEnemies;
    private static FieldInfo fiSpectatorList;

    public static void Patch(Harmony harmony)
    {
        Type spectateEnemiesType = Type.GetType("SpectateEnemy.SpectateEnemies, SpectateEnemy");
        if (spectateEnemiesType != null)
        {
            MethodInfo getNextValidSpectatable = AccessTools.Method(spectateEnemiesType, "GetNextValidSpectatable");
            if (getNextValidSpectatable != null)
            {
                HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(SpectateEnemySoftCompat), nameof(GetNextValidSpectatable)));
                _ = harmony.Patch(getNextValidSpectatable, postfix: postfix);

                fiSpectatedEnemyIndex = AccessTools.Field(spectateEnemiesType, "SpectatedEnemyIndex");
                fiSpectatingEnemies = AccessTools.Field(spectateEnemiesType, "SpectatingEnemies");
                fiSpectatorList = AccessTools.Field(spectateEnemiesType, "SpectatorList");
            }
        }
    }

    private static void GetNextValidSpectatable(object __instance)
    {
        if (fiSpectatedEnemyIndex != null && fiSpectatingEnemies != null && (bool)fiSpectatingEnemies.GetValue(__instance) && fiSpectatorList != null)
        {
            int spectatedEnemyIndex = (int)fiSpectatedEnemyIndex.GetValue(__instance);
            IEnumerable<object> spectatorList = (IEnumerable<object>)fiSpectatorList.GetValue(__instance);
            object spectable = spectatorList.ElementAtOrDefault(spectatedEnemyIndex);
            EnemyAI enemy = AccessTools.Field(spectable.GetType(), "enemyInstance")?.GetValue(spectable) as EnemyAI;

            if (!DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, enemy?.gameObject))
                DimensionRegistry.SetInUpsideDown(LFCUtilities.LocalPlayer.gameObject, DimensionRegistry.IsInUpsideDown(enemy.gameObject));
        }
    }
}
