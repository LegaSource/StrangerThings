using HarmonyLib;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class StormyWeatherPatch
{
    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.Update))]
    [HarmonyPostfix]
    private static void UpdateStormy(ref StormyWeather __instance)
    {
        if (__instance.setStaticToObject != null && __instance.setStaticGrabbableObject != null && DimensionRegistry.IsInUpsideDown(GameNetworkManager.Instance.localPlayerController.gameObject))
            __instance.staticElectricityParticle.Stop();
    }

    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.PlayThunderEffects))]
    [HarmonyPrefix]
    private static void FogFlash() => UpsideDownAtmosphereController.Instance?.TriggerLightning();
}
