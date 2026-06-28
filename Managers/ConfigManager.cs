using BepInEx.Configuration;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrangerThings.Managers;

public class ConfigManager
{
    // Global
    public static ConfigEntry<bool> globalTips;
    // Mirror Items
    public static ConfigEntry<int> minMirrorScraps;
    public static ConfigEntry<int> maxMirrorScraps;
    public static ConfigEntry<bool> colorBlindTips;
    public static ConfigEntry<string> scrapExclusions;
    // Demogorgon
    public static ConfigEntry<string> demogorgonSpawnWeights;
    public static ConfigEntry<int> demogorgonMinHeadValue;
    public static ConfigEntry<int> demogorgonMaxHeadValue;
    // Henry
    public static ConfigEntry<string> henrySpawnWeights;
    // Vecna
    //public static ConfigEntry<int> vecnaRarity;
    // Crustapikan
    public static ConfigEntry<int> crustapikanRarity;
    public static ConfigEntry<int> crustapikanMinArmValue;
    public static ConfigEntry<int> crustapikanMaxArmValue;
    // Crustapikan Larvae
    public static ConfigEntry<int> crustapikanLarvaeRarity;
    public static ConfigEntry<int> crustapikanLarvaeMinCorpseValue;
    public static ConfigEntry<int> crustapikanLarvaeMaxCorpseValue;
    // Limadon
    public static ConfigEntry<int> limadonRarity;
    public static ConfigEntry<int> limadonMinCorpseValue;
    public static ConfigEntry<int> limadonMaxCorpseValue;
    // Upside Down
    public static ConfigEntry<int> portalLockDuration;
    public static ConfigEntry<float> upsideDownVolume;
    public static ConfigEntry<string> visibilityStateInclusions;
    public static ConfigEntry<string> visibilityStateExclusions;
    // Bats Horde
    public static ConfigEntry<int> minBatsHordeOutside;
    public static ConfigEntry<int> maxBatsHordeOutside;
    public static ConfigEntry<int> minBatsHordeInside;
    public static ConfigEntry<int> maxBatsHordeInside;
    // Vines Zone
    public static ConfigEntry<int> minVinesZoneOutside;
    public static ConfigEntry<int> maxVinesZoneOutside;
    public static ConfigEntry<int> minVinesZoneInside;
    public static ConfigEntry<int> maxVinesZoneInside;

    public static void Load()
    {
        // Global
        globalTips = StrangerThings.configFile.Bind(Constants.GLOBAL, "Global tips", true, "Enable global tips to help learn the mod’s mechanics.");
        // Mirror Items
        minMirrorScraps = StrangerThings.configFile.Bind(Constants.MIRROR_SCRAPS, $"Min {Constants.MIRROR_SCRAPS}", 6, $"Min {Constants.MIRROR_SCRAPS}.");
        maxMirrorScraps = StrangerThings.configFile.Bind(Constants.MIRROR_SCRAPS, $"Max {Constants.MIRROR_SCRAPS}", 10, $"Max {Constants.MIRROR_SCRAPS}.");
        colorBlindTips = StrangerThings.configFile.Bind(Constants.MIRROR_SCRAPS, "Colorblind tips", true, $"Enable particle color change notifications for mirror {Constants.MIRROR_SCRAPS}.");
        scrapExclusions = StrangerThings.configFile.Bind(Constants.MIRROR_SCRAPS, "Exclusion list", "Key,Bee hive,Apparatus,EnginePart1,V-type engine,Toy car", "List of scraps that will not spawn in the Upside Down.\nYou can add scraps by separating them with a comma.");
        // Demogorgon
        demogorgonSpawnWeights = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Spawn weights", "Vanilla:20,Modded:20", $"{Constants.DEMOGORGON} spawn weights.");
        demogorgonMinHeadValue = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Min head value", 80, $"{Constants.DEMOGORGON} min head value (SellBodiesFixed must be installed).");
        demogorgonMaxHeadValue = StrangerThings.configFile.Bind(Constants.DEMOGORGON, "Max head value", 120, $"{Constants.DEMOGORGON} max head value (SellBodiesFixed must be installed).");
        // Henry
        henrySpawnWeights = StrangerThings.configFile.Bind(Constants.HENRY_CREEL, "Spawn weights", "Vanilla:20,Modded:20", $"{Constants.HENRY_CREEL} spawn weights.");
        // Vecna
        //vecnaRarity = StrangerThings.configFile.Bind(Constants.VECNA, "Rarity", 20, $"{Constants.VECNA} base rarity.");
        // Crustapikan
        crustapikanRarity = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN, "Rarity", 20, $"{Constants.CRUSTAPIKAN} base rarity.");
        crustapikanMinArmValue = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN, "Min arm value", 250, $"{Constants.CRUSTAPIKAN} min arm value (SellBodiesFixed must be installed).");
        crustapikanMaxArmValue = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN, "Max arm value", 300, $"{Constants.CRUSTAPIKAN} max arm value (SellBodiesFixed must be installed).");
        // Crustapikan Larvae
        crustapikanLarvaeRarity = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN_LARVAE, "Rarity", 20, $"{Constants.CRUSTAPIKAN_LARVAE} base rarity.");
        crustapikanLarvaeMinCorpseValue = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN_LARVAE, "Min corpse value", 20, $"{Constants.CRUSTAPIKAN_LARVAE} min corpse value (SellBodiesFixed must be installed).");
        crustapikanLarvaeMaxCorpseValue = StrangerThings.configFile.Bind(Constants.CRUSTAPIKAN_LARVAE, "Max corpse value", 40, $"{Constants.CRUSTAPIKAN_LARVAE} max corpse value (SellBodiesFixed must be installed).");
        // Limadon
        limadonRarity = StrangerThings.configFile.Bind(Constants.LIMADON, "Rarity", 20, $"{Constants.LIMADON} base rarity.");
        limadonMinCorpseValue = StrangerThings.configFile.Bind(Constants.LIMADON, "Min corpse value", 70, $"{Constants.LIMADON} min corpse value (SellBodiesFixed must be installed).");
        limadonMaxCorpseValue = StrangerThings.configFile.Bind(Constants.LIMADON, "Max corpse value", 110, $"{Constants.LIMADON} max corpse value (SellBodiesFixed must be installed).");
        // Upside Down
        portalLockDuration = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Portal lock duration", 60, "Portal lock duration when entering or exiting.");
        upsideDownVolume = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Upside Down volume", 0.6f, "Volume of background music in the Upside Down.");
        visibilityStateInclusions = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Visibility state whitelist", "SP_Snowman,SP_SnowPile,LK_Lantern,SawBoxExplosive,ChainEscape,Sprout", "Additional list of Network Objects whose visibility (visible/invisible) will be updated when switching between dimensions.");
        visibilityStateExclusions = StrangerThings.configFile.Bind(Constants.UPSIDE_DOWN, "Visibility state blacklist", "Locker,DressGirl", "Network Objects whose visibility (visible/invisible) will not be updated when switching between dimensions.");
        // Bats Horde
        minBatsHordeOutside = StrangerThings.configFile.Bind(Constants.BATS_HORDE, "Min outside", 3, $"Min {Constants.BATS_HORDE} to spawn outside.");
        maxBatsHordeOutside = StrangerThings.configFile.Bind(Constants.BATS_HORDE, "Max outside", 4, $"Min {Constants.BATS_HORDE} to spawn outside.");
        minBatsHordeInside = StrangerThings.configFile.Bind(Constants.BATS_HORDE, "Min inside", 4, $"Min {Constants.BATS_HORDE} to spawn inside.");
        maxBatsHordeInside = StrangerThings.configFile.Bind(Constants.BATS_HORDE, "Max inside", 5, $"Min {Constants.BATS_HORDE} to spawn inside.");
        // Vines Zone
        minVinesZoneOutside = StrangerThings.configFile.Bind(Constants.VINES_ZONE, "Min outside", 3, $"Min {Constants.VINES_ZONE} to spawn outside.");
        maxVinesZoneOutside = StrangerThings.configFile.Bind(Constants.VINES_ZONE, "Max outside", 4, $"Min {Constants.VINES_ZONE} to spawn outside.");
        minVinesZoneInside = StrangerThings.configFile.Bind(Constants.VINES_ZONE, "Min inside", 2, $"Min {Constants.VINES_ZONE} to spawn inside.");
        maxVinesZoneInside = StrangerThings.configFile.Bind(Constants.VINES_ZONE, "Max inside", 3, $"Min {Constants.VINES_ZONE} to spawn inside.");
    }

    public static void GetEnemySpawns(string spawnWeights, out Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, out Dictionary<string, int> spawnRateByCustomLevelType)
    {
        spawnRateByLevelType = [];
        spawnRateByCustomLevelType = [];

        foreach (string spawnWeight in spawnWeights.Split(',').Select(s => s.Trim()))
        {
            string[] values = spawnWeight.Split(':');
            if (values.Length != 2)
                continue;

            string name = values[0];
            if (int.TryParse(values[1], out int spawnRate))
            {
                if (Enum.TryParse(name, ignoreCase: true, out Levels.LevelTypes levelType))
                    spawnRateByLevelType[levelType] = spawnRate;
                else
                    spawnRateByCustomLevelType[name] = spawnRate;
            }
        }
    }
}
