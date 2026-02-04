using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using LethalLib.Modules;

namespace StrangerThings.ModsCompat;

public static class SellBodiesFixedSoftCompat
{
    private static readonly BaseUnityPlugin SellBodiesInstance;

    static SellBodiesFixedSoftCompat()
    {
        if (Chainloader.PluginInfos.TryGetValue("Entity378.sellbodies", out PluginInfo info))
            SellBodiesInstance = info.Instance;
    }

    public static void RegisterBody(string enemyName, Item item, int minValue, int maxValue, bool enabled)
    {
        if (SellBodiesInstance != null && item != null)
        {
            Utilities.FixMixerGroups(item.spawnPrefab);
            _ = item.spawnPrefab.AddComponent(AccessTools.TypeByName("BodySyncer"));
            item.minValue = minValue;
            item.maxValue = maxValue;
            NetworkPrefabs.RegisterNetworkPrefab(item.spawnPrefab);
            Items.RegisterItem(item);

            if (enabled && AccessTools.Field(SellBodiesInstance.GetType(), "BodySpawns")?.GetValue(SellBodiesInstance) is System.Collections.IDictionary bodySpawns)
                bodySpawns[enemyName] = item;
        }
    }
}
