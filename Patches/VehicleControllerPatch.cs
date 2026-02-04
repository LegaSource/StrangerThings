using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;

namespace StrangerThings.Patches;

public class VehicleControllerPatch
{
    [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.Start))]
    [HarmonyPostfix]
    private static void StartVehicleController(VehicleController __instance)
    {
        if (LFCUtilities.LocalPlayer != null)
            DimensionRegistry.UpdateVisibilityState(__instance.gameObject);
    }
}
