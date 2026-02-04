using GameNetcodeStuff;
using HarmonyLib;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace StrangerThings.Patches;

public class ShotgunItemPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ShotgunItem), nameof(ShotgunItem.ShootGun))]
    private static IEnumerable<CodeInstruction> ShootGun(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        List<CodeInstruction> code = new List<CodeInstruction>(instructions);

        MethodInfo miDamagePlayer = AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer), [typeof(int), typeof(bool), typeof(bool), typeof(CauseOfDeath), typeof(int), typeof(bool), typeof(Vector3)]);
        MethodInfo miHit = AccessTools.Method(typeof(IHittable), nameof(IHittable.Hit), [typeof(int), typeof(Vector3), typeof(PlayerControllerB), typeof(bool), typeof(int)]);
        MethodInfo miGetGameObject = AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject));
        MethodInfo miAreInSameDimension = AccessTools.Method(typeof(DimensionRegistry), nameof(DimensionRegistry.AreInSameDimension), [typeof(GameObject), typeof(GameObject)]);

        for (int i = 0; i < code.Count; i++)
        {
            // Bloquer DamagePlayer si pas même dimension que le gun
            if (miDamagePlayer != null && code[i].Calls(miDamagePlayer))
            {
                int startArgsIndex = FindPrevLdlocIndex(code, i, localIndex: 1);
                if (startArgsIndex == -1) continue;

                Label continueDamage = il.DefineLabel();
                Label skipDamage = il.DefineLabel();

                code[startArgsIndex].labels.Add(continueDamage);
                if (i + 1 < code.Count)
                    code[i + 1].labels.Add(skipDamage);

                List<CodeInstruction> toInsert =
                [
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, miGetGameObject),
                    new CodeInstruction(OpCodes.Ldloc_1), // localPlayerController
                    new CodeInstruction(OpCodes.Callvirt, miGetGameObject),
                    new CodeInstruction(OpCodes.Call, miAreInSameDimension),
                    new CodeInstruction(OpCodes.Brtrue_S, continueDamage),
                    new CodeInstruction(OpCodes.Br_S, skipDamage),
                ];

                code.InsertRange(startArgsIndex, toInsert);
                i += toInsert.Count;

                continue;
            }

            // Bloquer IHittable.Hit si pas même dimension que le gun
            if (miHit != null && code[i].Calls(miHit))
            {
                int startArgsIndex = FindPrevLdlocIndex(code, i, localIndex: 10); // ldloc.s 10 == IHittable component
                if (startArgsIndex == -1) continue;

                Label skipHitLabel;
                if (i + 1 < code.Count
                    && (code[i + 1].opcode == OpCodes.Brfalse || code[i + 1].opcode == OpCodes.Brfalse_S)
                    && code[i + 1].operand is Label existing)
                {
                    skipHitLabel = existing;
                }
                else
                {
                    skipHitLabel = il.DefineLabel();
                    if (i + 1 < code.Count)
                        code[i + 1].labels.Add(skipHitLabel);
                }

                List<CodeInstruction> toInsert =
                [
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, miGetGameObject),
                    new CodeInstruction(OpCodes.Ldloc_S, (byte)12), // mainScript
                    new CodeInstruction(OpCodes.Callvirt, miGetGameObject),
                    new CodeInstruction(OpCodes.Call, miAreInSameDimension),
                    new CodeInstruction(OpCodes.Brfalse_S, skipHitLabel),
                ];

                code.InsertRange(startArgsIndex, toInsert);
                i += toInsert.Count;

                continue;
            }
        }

        return code;
    }

    private static int FindPrevLdlocIndex(List<CodeInstruction> code, int fromIndex, int localIndex)
    {
        for (int j = fromIndex; j >= 0; j--)
        {
            if (TryGetLdlocIndex(code[j], out int index) && index == localIndex)
                return j;
        }
        return -1;
    }

    private static bool TryGetLdlocIndex(CodeInstruction ci, out int index)
    {
        index = -1;

        if (ci.opcode == OpCodes.Ldloc_0) { index = 0; return true; }
        if (ci.opcode == OpCodes.Ldloc_1) { index = 1; return true; }
        if (ci.opcode == OpCodes.Ldloc_2) { index = 2; return true; }
        if (ci.opcode == OpCodes.Ldloc_3) { index = 3; return true; }

        if (ci.opcode != OpCodes.Ldloc && ci.opcode != OpCodes.Ldloc_S)
            return false;

        if (ci.operand is LocalBuilder lb) { index = lb.LocalIndex; return true; }
        if (ci.operand is int i) { index = i; return true; }
        if (ci.operand is byte b) { index = b; return true; }

        return false;
    }
}
