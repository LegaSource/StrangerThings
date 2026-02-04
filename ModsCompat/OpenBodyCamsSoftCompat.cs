using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StrangerThings.ModsCompat;

public static class OpenBodyCamsSoftCompat
{
    private static FieldInfo fiCurrentActualTarget;
    private static FieldInfo fiCurrentPlayer;
    private static FieldInfo fiEnableCamera;
    private static MethodInfo miGetForceTargetInvalid;

    private static Type statusEnumType;
    private static object statusDisabled;
    private static object statusTargetInvalid;

    public static void Patch(Harmony harmony)
    {
        Type bodyCamComponentType = Type.GetType("OpenBodyCams.BodyCamComponent, OpenBodyCams");
        if (bodyCamComponentType != null)
        {
            MethodInfo getUpdatedCameraStatus = AccessTools.Method(bodyCamComponentType, "GetUpdatedCameraStatus");
            if (getUpdatedCameraStatus != null)
            {
                statusEnumType = getUpdatedCameraStatus.ReturnType;
                if (statusEnumType != null && statusEnumType.IsEnum)
                {
                    statusDisabled = Enum.Parse(statusEnumType, "Disabled", ignoreCase: false);
                    statusTargetInvalid = Enum.Parse(statusEnumType, "TargetInvalid", ignoreCase: false);
                    fiEnableCamera = AccessTools.Field(bodyCamComponentType, "EnableCamera");
                    miGetForceTargetInvalid = AccessTools.PropertyGetter(bodyCamComponentType, "ForceTargetInvalid");
                    fiCurrentActualTarget = AccessTools.Field(bodyCamComponentType, "currentActualTarget");
                    fiCurrentPlayer = AccessTools.Field(bodyCamComponentType, "currentPlayer");

                    if (fiEnableCamera == null || miGetForceTargetInvalid == null || fiCurrentActualTarget == null || fiCurrentPlayer == null)
                    {
                        StrangerThings.mls.LogError("OpenBodyCams compat: members not found.");
                        return;
                    }

                    HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(typeof(OpenBodyCamsSoftCompat), nameof(GetUpdatedCameraStatus_Prefix)));
                    _ = harmony.Patch(getUpdatedCameraStatus, prefix: prefix);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool GetUpdatedCameraStatus_Prefix(object __instance, ref object __result)
    {
        if (!(bool)fiEnableCamera.GetValue(__instance)
            || fiCurrentActualTarget.GetValue(__instance) == null
            || (bool)miGetForceTargetInvalid.Invoke(__instance, null))
        {
            return true;
        }

        if (LFCUtilities.LocalPlayer != null && DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer.gameObject))
        {
            __result = statusDisabled;
            return false;
        }

        PlayerControllerB currentPlayer = fiCurrentPlayer != null ? fiCurrentPlayer.GetValue(__instance) as PlayerControllerB : null;
        if (LFCUtilities.LocalPlayer != null && currentPlayer != null && currentPlayer.isPlayerControlled && !DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer.gameObject, currentPlayer.gameObject))
        {
            __result = statusTargetInvalid;
            return false;
        }

        return true;
    }
}
