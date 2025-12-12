using HarmonyLib;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class FlowerSnakeEnemyPatch
{
    [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.Update))]
    [HarmonyPostfix]
    private static void UpdateTulipSnake(ref FlowerSnakeEnemy __instance)
    {
        if (__instance.clingingToPlayer != null && !DimensionRegistry.AreInSameDimension(__instance.gameObject, __instance.clingingToPlayer.gameObject))
            __instance.StopClingingOnLocalClient(__instance.clingPosition == 4);
    }
}
