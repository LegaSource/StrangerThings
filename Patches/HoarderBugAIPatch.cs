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
        List<GameObject> gObjects = LFCSpawnRegistry.GetAllAs<GrabbableObject>()
            .Where(g => DimensionRegistry.IsInUpsideDown(g.gameObject))
            .Select(g => g.gameObject)
            .ToList();
        _ = HoarderBugAI.grabbableObjectsInMap.RemoveAll(gObjects.Contains);
        _ = HoarderBugAI.HoarderBugItems.RemoveAll(h => gObjects.Contains(h.itemGrabbableObject.gameObject));
    }
}
