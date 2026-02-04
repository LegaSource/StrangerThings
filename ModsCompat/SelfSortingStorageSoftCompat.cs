using HarmonyLib;
using StrangerThings.Behaviours.Items;
using StrangerThings.Managers;
using System;
using System.Linq;
using System.Reflection;
using Unity.Netcode;

namespace StrangerThings.ModsCompat;

public static class SelfSortingStorageSoftCompat
{
    public static void Patch(Harmony harmony)
    {
        Type effectsType = Type.GetType("SelfSortingStorage.Utils.Effects, SelfSortingStorage") ?? AccessTools.TypeByName("SelfSortingStorage.Utils.Effects");
        if (effectsType != null)
        {
            MethodInfo spawnItem = AccessTools.Method(effectsType, "SpawnItem");
            if (spawnItem != null)
            {
                HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(SelfSortingStorageSoftCompat), nameof(SpawnItem)));
                _ = harmony.Patch(spawnItem, postfix: postfix);
            }
        }
    }

    private static void SpawnItem(object __result)
    {
        if (__result == null) return;

        // Trouve le champ NetworkObjectReference dans le struct ItemNetworkReference
        FieldInfo norField = AccessTools.GetDeclaredFields(__result.GetType()).FirstOrDefault(f => f.FieldType == typeof(NetworkObjectReference));
        if (norField == null) return;

        NetworkObjectReference networkObjectReference = (NetworkObjectReference)norField.GetValue(__result);
        if (networkObjectReference.TryGet(out NetworkObject networkObject) && networkObject != null)
        {
            GrabbableObject grabbableObject = networkObject.GetComponent<GrabbableObject>();
            if (grabbableObject is UpsideDownObject)
                StrangerThingsNetworkManager.Instance?.SetGObjectInUpsideDownEveryoneRpc(networkObject, false);
        }
    }
}
