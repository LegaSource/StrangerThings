using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Scripts;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class StormyWeatherPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.Update))]
    private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new List<CodeInstruction>(instructions);

        FieldInfo fiIsInFactory = AccessTools.Field(typeof(GrabbableObject), nameof(GrabbableObject.isInFactory));
        MethodInfo miIsUntargetableObject = AccessTools.Method(typeof(StormyWeatherPatch), nameof(IsUntargetableObject));

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f && f == fiIsInFactory)
            {
                code[i].opcode = OpCodes.Call;
                code[i].operand = miIsUntargetableObject;
            }
        }

        return code;
    }

    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.LightningStrike))]
    [HarmonyPrefix]
    private static bool ShouldRunLightningStrike(StormyWeather __instance)
    {
        if (StartOfRound.Instance.currentLevel.currentWeather != LevelWeatherType.Stormy && !DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject))
        {
            __instance.staticElectricityParticle.Stop();
            __instance.staticElectricityParticle.GetComponent<AudioSource>().Stop();
            __instance.setStaticToObject = null;
            return false;
        }
        return true;
    }

    private static bool IsUntargetableObject(GrabbableObject grabbableObject)
        => grabbableObject == null
            || grabbableObject.isInFactory
            || (StartOfRound.Instance.currentLevel.currentWeather != LevelWeatherType.Stormy && !DimensionRegistry.IsInUpsideDown(grabbableObject.gameObject));

    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.SetStaticElectricityWarning))]
    [HarmonyPrefix]
    private static bool SetStaticElectricityWarning(NetworkObject warningObject)
        => DimensionRegistry.AreInSameDimension(warningObject.gameObject, LFCUtilities.LocalPlayer?.gameObject)
            && (StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy || DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));

    [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.PlayThunderEffects))]
    [HarmonyPrefix]
    private static void FogFlash() => UpsideDownAtmosphereController.Instance?.TriggerLightning();
}
