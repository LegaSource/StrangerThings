using HarmonyLib;
using LegaFusionCore.Registries;
using StrangerThings.Registries;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StrangerThings.Patches;

public class HoarderBugAIPatch
{
    [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.RefreshGrabbableObjectsInMapList))]
    [HarmonyPostfix]
    private static void RefreshGrabbableObjects()
    {
        HashSet<GameObject> gObjects = new HashSet<GameObject>(LFCSpawnRegistry.GetAllAs<GrabbableObject>()
            .Where(g => g != null && DimensionRegistry.IsInUpsideDown(g.gameObject))
            .Select(g => g.gameObject));
        _ = HoarderBugAI.grabbableObjectsInMap.RemoveAll(g => g == null || gObjects.Contains(g));
        _ = HoarderBugAI.HoarderBugItems.RemoveAll(h => h == null || h.itemGrabbableObject == null || gObjects.Contains(h.itemGrabbableObject.gameObject));
    }
}
