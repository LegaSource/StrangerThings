using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace StrangerThings.Patches;

public class RadMechAIPatch
{
    [HarmonyPatch(typeof(RadMechAI), nameof(RadMechAI.ShootGun))]
    [HarmonyPrefix]
    private static bool ShootGun(RadMechAI __instance)
        => DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject);

    [HarmonyPatch(typeof(RadMechAI), nameof(RadMechAI.SetExplosion))]
    [HarmonyPrefix]
    private static bool SetExplosion(RadMechAI __instance)
        => DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject);

    [HarmonyPatch(typeof(RadMechAI), nameof(RadMechAI.Stomp))]
    [HarmonyPrefix]
    private static bool Stomp(RadMechAI __instance)
        => DimensionRegistry.AreInSameDimension(LFCUtilities.LocalPlayer?.gameObject, __instance.gameObject);

    [HarmonyPatch(typeof(RadMechAI), nameof(RadMechAI.CheckSightForThreat))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CheckSightForThreatTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        List<CodeInstruction> code = new List<CodeInstruction>(instructions);

        FieldInfo fiFocusedThreat = AccessTools.Field(typeof(RadMechAI), "focusedThreatTransform");
        MethodInfo miAreInSameDimension = AccessTools.Method(typeof(RadMechAIPatch), nameof(AreInSameDimension));

        for (int i = 0; i < code.Count - 1; i++)
        {
            if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo f && f == fiFocusedThreat)
            {
                Label okLabel = il.DefineLabel();
                code[i + 1].labels.Add(okLabel);

                code.InsertRange(i + 1,
                [
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, miAreInSameDimension),
                    new CodeInstruction(OpCodes.Brtrue_S, okLabel),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Ret),
                ]);

                break;
            }
        }

        return code;
    }

    private static bool AreInSameDimension(RadMechAI radMech)
    {
        if (!DimensionRegistry.AreInSameDimension(radMech.focusedThreatTransform?.gameObject, radMech.gameObject))
        {
            radMech.SwitchToBehaviourStateOnLocalClient(0);
            return false;
        }
        return true;
    }
}
