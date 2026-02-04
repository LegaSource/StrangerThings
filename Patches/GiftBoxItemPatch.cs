using HarmonyLib;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;

namespace StrangerThings.Patches;

public class GiftBoxItemPatch
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GiftBoxItem), nameof(GiftBoxItem.OpenGiftBoxServerRpc))]
    private static IEnumerable<CodeInstruction> OpenGiftBoxForServer(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new List<CodeInstruction>(instructions);

        MethodInfo miSpawn = AccessTools.Method(typeof(NetworkObject), nameof(NetworkObject.Spawn), [typeof(bool)]);
        MethodInfo miGetNetworkObject = AccessTools.PropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.NetworkObject));
        MethodInfo miSetInUpsideDown = AccessTools.Method(typeof(GiftBoxItemPatch), nameof(SetInUpsideDown));

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(miSpawn)) continue;

            int ldlocIndex = -1;
            for (int j = i; j >= 1; j--)
            {
                if (code[j].Calls(miGetNetworkObject) && code[j - 1].IsLdloc())
                {
                    ldlocIndex = j - 1;
                    break;
                }
            }
            if (ldlocIndex == -1) continue;

            List<CodeInstruction> toInsert =
            [
                code[ldlocIndex].Clone(),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, miSetInUpsideDown),
            ];

            int insertionIndex = i + 1;
            code.InsertRange(insertionIndex, toInsert);

            i += toInsert.Count;
        }

        return code;
    }

    private static void SetInUpsideDown(GrabbableObject grabbableObject, GiftBoxItem giftBox)
    {
        if (grabbableObject != null && giftBox != null && DimensionRegistry.IsInUpsideDown(giftBox.gameObject))
            StrangerThingsNetworkManager.Instance.SetGObjectInUpsideDownEveryoneRpc(grabbableObject.GetComponent<NetworkObject>(), true);
    }
}
