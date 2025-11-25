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
        {
            StrangerThings.mls.LogError(__instance.gameObject.name + " stopped clinging to " + __instance.clingingToPlayer.playerUsername);
            __instance.StopClingingOnLocalClient(__instance.clingPosition == 4);
        }
    }
}
