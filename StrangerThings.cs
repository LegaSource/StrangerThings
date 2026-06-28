using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LegaFusionCore.Managers;
using LegaFusionCore.Registries;
using LethalLib.Extras;
using LethalLib.Modules;
using StrangerThings.Behaviours.Items;
using StrangerThings.Behaviours.Items.Figurines;
using StrangerThings.Managers;
using StrangerThings.ModsCompat;
using StrangerThings.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using static LegaFusionCore.Registries.LFCSpawnableItemRegistry;
using static LethalLib.Modules.Levels;

namespace StrangerThings;

[BepInPlugin(modGUID, modName, modVersion)]
public class StrangerThings : BaseUnityPlugin
{
    public const string modGUID = "Lega.StrangerThings";
    public const string modName = "Stranger Things";
    public const string modVersion = "1.0.4";

    private readonly Harmony harmony = new Harmony(modGUID);
    internal static readonly AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "strangerthings"));
    internal static ManualLogSource mls;
    public static ConfigFile configFile;

    public static GameObject managerPrefab = NetworkPrefabs.CreateNetworkPrefab("StrangerThingsNetworkManager");

    public static Dictionary<EnemyType, int> UpsideDownEnemies = [];
    public static GameObject UpsideDownAtmosphereObj;
    public static GameObject UpsideDownSporesObj;
    public static GameObject UpsideDownPortalObj;
    public static GameObject UpsideDownMirrorObjectObj;
    public static GameObject AntennaHazardObj;
    public static GameObject RockProjectileObj;
    public static GameObject PebbleProjectileObj;
    public static GameObject LogProjectileObj;
    public static GameObject Tree1Obj;
    public static GameObject Tree2Obj;
    public static GameObject Tree3Obj;
    public static GameObject BatsSkyObj;

    // Items
    public static Item AntennaItem;
    public static Item ElevenPopItem;

    // Hazard
    public static GameObject BatsHordeObj;
    public static GameObject VinesZoneObj;

    // Enemies
    public static EnemyType LimadonType;
    public static EnemyType CrustopikanLarvaeType;

    // Materials
    public static Material ZoneFilterMat;

    // Audios
    public static GameObject StoneImpactAudioObj;
    public static GameObject DoorImpact1AudioObj;
    public static GameObject DoorImpact2AudioObj;
    public static GameObject DoorImpact3AudioObj;

    public void Awake()
    {
        mls = BepInEx.Logging.Logger.CreateLogSource("StrangerThings");
        configFile = Config;
        ConfigManager.Load();

        LoadManager();
        NetcodePatcher();
        LoadItems();
        LoadHazards();
        LoadEnemies();
        LoadPrefabs();
        LoadNetworkPrefabs();

        harmony.PatchAll(typeof(AudioMixerPatch));
        harmony.PatchAll(typeof(NetworkBehaviourPatch));
        harmony.PatchAll(typeof(StartOfRoundPatch));
        harmony.PatchAll(typeof(RoundManagerPatch));
        harmony.PatchAll(typeof(StartMatchLeverPatch));
        harmony.PatchAll(typeof(PlayerControllerBPatch));
        harmony.PatchAll(typeof(GrabbableObjectPatch));
        harmony.PatchAll(typeof(ShotgunItemPatch));
        harmony.PatchAll(typeof(FlashlightItemPatch));
        harmony.PatchAll(typeof(GiftBoxItemPatch));
        harmony.PatchAll(typeof(StormyWeatherPatch));
        harmony.PatchAll(typeof(LightningBoltScriptPatch));
        harmony.PatchAll(typeof(DoorLockPatch));
        harmony.PatchAll(typeof(EnemyAIPatch));
        harmony.PatchAll(typeof(FlowerSnakeEnemyPatch));
        harmony.PatchAll(typeof(HoarderBugAIPatch));
        harmony.PatchAll(typeof(JesterAIPatch));
        harmony.PatchAll(typeof(NutcrackerEnemyAIPatch));
        harmony.PatchAll(typeof(RadMechAIPatch));
        harmony.PatchAll(typeof(SandSpiderWebTrapPatch));
        harmony.PatchAll(typeof(DeadBodyInfoPatch));
        harmony.PatchAll(typeof(VehicleControllerPatch));

        LethalMinSoftCompat.Patch(harmony);
        MelaniesVoiceSoftCompat.Patch(harmony);
        OpenBodyCamsSoftCompat.Patch(harmony);
        SelfSortingStorageSoftCompat.Patch(harmony);
        SpectateEnemySoftCompat.Patch(harmony);
    }

    public static void LoadManager()
    {
        Utilities.FixMixerGroups(managerPrefab);
        _ = managerPrefab.AddComponent<StrangerThingsNetworkManager>();
    }

    private static void NetcodePatcher()
    {
        Type[] types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (Type type in types)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length == 0) continue;
                _ = method.Invoke(null, null);
            }
        }
    }

    public void LoadItems()
    {
        AntennaItem = LFCObjectsManager.RegisterObject(typeof(AntennaItem), bundle.LoadAsset<Item>("Assets/Antenna/AntennaItem.asset"));
        Items.RegisterShopItem(AntennaItem, price: 10);

        // Spawnable Items
        Add(typeof(ElevenPop), ElevenPopItem = bundle.LoadAsset<Item>("Assets/Items/Figurines/Eleven/ElevenPopItem.asset"), 0, 1, 5, 200, 300);
        Add(typeof(BaseballBat), bundle.LoadAsset<Item>("Assets/Items/BaseballBat/BaseballBatItem.asset"), 0, 2, 10, 180, 270);
        Add(typeof(Guitar), bundle.LoadAsset<Item>("Assets/Items/Guitar/GuitarItem.asset"), 0, 2, 15, 170, 255);
        Add(typeof(UpsideDownObject), bundle.LoadAsset<Item>("Assets/Items/StarcourtSign/StarcourtSignItem.asset"), 0, 2, 50, 100, 150);
        Add(typeof(UpsideDownObject), bundle.LoadAsset<Item>("Assets/Items/WalkieTalkie/WalkieTalkieItem.asset"), 0, 2, 60, 80, 120);
        Add(typeof(UpsideDownObject), bundle.LoadAsset<Item>("Assets/Items/Camera/CameraItem.asset"), 0, 2, 70, 60, 90);
        Add(typeof(UpsideDownObject), bundle.LoadAsset<Item>("Assets/Items/WafflesBox/WafflesBoxItem.asset"), 0, 2, 75, 50, 75);
        Add(typeof(UpsideDownObject), bundle.LoadAsset<Item>("Assets/Items/FairyLights/FairyLightsItem.asset"), 0, 2, 75, 50, 75);
    }

    public void LoadHazards()
    {
        BatsHordeObj = RegisterHazard(bundle.LoadAsset<GameObject>("Assets/UpsideDown/Bats/BatsHorde.prefab"), ConfigManager.minBatsHordeInside.Value, ConfigManager.maxBatsHordeInside.Value);
        VinesZoneObj = RegisterHazard(bundle.LoadAsset<GameObject>("Assets/UpsideDown/Vines/VinesZone.prefab"), ConfigManager.minVinesZoneInside.Value, ConfigManager.maxVinesZoneInside.Value);
    }

    public GameObject RegisterHazard(GameObject gameObject, float minSpawn, float maxSpawn)
    {
        SpawnableMapObjectDef mapObjDef = ScriptableObject.CreateInstance<SpawnableMapObjectDef>();
        mapObjDef.spawnableMapObject = new SpawnableMapObject { prefabToSpawn = gameObject };

        AnimationCurve animationCurveInside = new AnimationCurve(new Keyframe(minSpawn, maxSpawn));
        NetworkPrefabs.RegisterNetworkPrefab(mapObjDef.spawnableMapObject.prefabToSpawn);
        Utilities.FixMixerGroups(mapObjDef.spawnableMapObject.prefabToSpawn);
        MapObjects.RegisterMapObject(mapObjDef, LevelTypes.All, (SelectableLevel _) => animationCurveInside);

        return mapObjDef.spawnableMapObject.prefabToSpawn;
    }

    public void LoadEnemies()
    {
        // NORMAL WORLD ONLY
        ConfigManager.GetEnemySpawns(ConfigManager.demogorgonSpawnWeights.Value, out Dictionary<LevelTypes, int> demogorgonByLevelType, out Dictionary<string, int> demogorgonByCustomLevelType);
        TerminalNode demogorgonTN = bundle.LoadAsset<TerminalNode>("Assets/Enemies/Demogorgon/DemogorgonTN.asset");
        TerminalKeyword demogorgonTK = bundle.LoadAsset<TerminalKeyword>("Assets/Enemies/Demogorgon/DemogorgonTK.asset");
        Item demogorgonHeadItem = bundle.LoadAsset<Item>("Assets/Enemies/Demogorgon/DemogorgonHeadItem.asset");

        RegisterEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/Demogorgon/DemogorgonKidnapperEnemy.asset"),
            spawnRateByLevelType: demogorgonByLevelType,
            spawnRateByCustomLevelType: demogorgonByCustomLevelType,
            terminalNode: demogorgonTN,
            terminalKeyword: demogorgonTK,
            bodyItem: demogorgonHeadItem,
            bodyMinValue: ConfigManager.demogorgonMinHeadValue.Value,
            bodyMaxValue: ConfigManager.demogorgonMaxHeadValue.Value);
        RegisterEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/Demogorgon/DemogorgonHunterEnemy.asset"),
            spawnRateByLevelType: demogorgonByLevelType,
            spawnRateByCustomLevelType: demogorgonByCustomLevelType,
            terminalNode: demogorgonTN,
            terminalKeyword: demogorgonTK,
            bodyItem: demogorgonHeadItem,
            bodyMinValue: ConfigManager.demogorgonMinHeadValue.Value,
            bodyMaxValue: ConfigManager.demogorgonMaxHeadValue.Value);

        ConfigManager.GetEnemySpawns(ConfigManager.henrySpawnWeights.Value, out Dictionary<LevelTypes, int> henryByLevelType, out Dictionary<string, int> henryByCustomLevelType);
        RegisterEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/Vecna/Henry/HenryEnemy.asset"),
            spawnRateByLevelType: henryByLevelType,
            spawnRateByCustomLevelType: henryByCustomLevelType,
            terminalNode: null,//bundle.LoadAsset<TerminalNode>("Assets/Enemies/Vecna/Henry/HenryTN.asset"),
            terminalKeyword: null/*bundle.LoadAsset<TerminalKeyword>("Assets/Enemies/Vecna/Henry/HenryTK.asset")*/);

        // UPSIDE DOWN ONLY
        _ = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/Crustapikan/CrustapikanEnemy.asset"),
            rarity: ConfigManager.crustapikanRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Enemies/Crustapikan/CrustapikanTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Enemies/Crustapikan/CrustapikanTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Enemies/Crustapikan/CrustapikanArmItem.asset"),
            bodyMinValue: ConfigManager.crustapikanMinArmValue.Value,
            bodyMaxValue: ConfigManager.crustapikanMaxArmValue.Value);
        LimadonType = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/Limadon/LimadonEnemy.asset"),
            rarity: ConfigManager.limadonRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Enemies/Limadon/LimadonTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Enemies/Limadon/LimadonTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Enemies/Limadon/LimadonCorpseItem.asset"),
            bodyMinValue: ConfigManager.limadonMinCorpseValue.Value,
            bodyMaxValue: ConfigManager.limadonMaxCorpseValue.Value);
        CrustopikanLarvaeType = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Enemies/CrustapikanLarvae/CrustapikanLarvaeEnemy.asset"),
            rarity: ConfigManager.crustapikanLarvaeRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Enemies/CrustapikanLarvae/CrustapikanLarvaeTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Enemies/CrustapikanLarvae/CrustapikanLarvaeTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Enemies/CrustapikanLarvae/CrustapikanLarvaeCorpseItem.asset"),
            bodyMinValue: ConfigManager.crustapikanLarvaeMinCorpseValue.Value,
            bodyMaxValue: ConfigManager.crustapikanLarvaeMaxCorpseValue.Value);
    }

    public void RegisterEnemy(EnemyType enemyType, Dictionary<LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType, TerminalNode terminalNode, TerminalKeyword terminalKeyword, Item bodyItem = null, int bodyMinValue = 0, int bodyMaxValue = 0, bool bodyEnabled = true)
    {
        NetworkPrefabs.RegisterNetworkPrefab(enemyType.enemyPrefab);
        Enemies.RegisterEnemy(enemyType, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode, terminalKeyword);
        if (bodyItem != null && bodyMinValue > 0 && bodyMaxValue > bodyMinValue)
            LegaFusionCore.ModsCompat.SellBodiesFixedSoftCompat.RegisterBody(enemyType.enemyName, bodyItem, bodyMinValue, bodyMaxValue, bodyEnabled);
    }

    public EnemyType RegisterUpsideDownEnemy(EnemyType enemyType, int rarity, TerminalNode terminalNode, TerminalKeyword terminalKeyword, Item bodyItem, int bodyMinValue = 0, int bodyMaxValue = 0, bool bodyEnabled = true)
    {
        NetworkPrefabs.RegisterNetworkPrefab(enemyType.enemyPrefab);
        Enemies.RegisterEnemy(enemyType, rarity, LevelTypes.None, terminalNode, terminalKeyword);
        if (bodyItem != null && bodyMinValue > 0 && bodyMaxValue > bodyMinValue)
            LegaFusionCore.ModsCompat.SellBodiesFixedSoftCompat.RegisterBody(enemyType.enemyName, bodyItem, bodyMinValue, bodyMaxValue, bodyEnabled);
        UpsideDownEnemies.Add(enemyType, rarity);
        return enemyType;
    }

    public void LoadPrefabs()
    {
        UpsideDownAtmosphereObj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/UpsideDownAtmosphere.prefab");
        UpsideDownSporesObj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Spores/Spores.prefab");
        Tree1Obj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Trees/Tree1.prefab");
        Tree2Obj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Trees/Tree2.prefab");
        Tree3Obj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Trees/Tree3.prefab");
        BatsSkyObj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Bats/BatsSky.prefab");
        // Materials
        ZoneFilterMat = bundle.LoadAsset<Material>("Assets/UpsideDown/Vines/M_ZoneFilter.mat");
    }

    public void LoadNetworkPrefabs()
    {
        Dictionary<GameObject, bool> gameObjects = new Dictionary<GameObject, bool>()
        {
            { UpsideDownPortalObj = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Portal/UpsideDownPortal.prefab"), false },
            { UpsideDownMirrorObjectObj = bundle.LoadAsset<GameObject>("Assets/Items/UpsideDownMirrorObject.prefab"), false },
            { AntennaHazardObj = bundle.LoadAsset<GameObject>("Assets/Antenna/AntennaHazard.prefab"), false },
            { RockProjectileObj = bundle.LoadAsset<GameObject>("Assets/Projectiles/Stone/RockProjectile.prefab"), false },
            { PebbleProjectileObj = bundle.LoadAsset<GameObject>("Assets/Projectiles/Stone/PebbleProjectile.prefab"), false },
            { LogProjectileObj = bundle.LoadAsset<GameObject>("Assets/Projectiles/Log/LogProjectile.prefab"), false },
            { StoneImpactAudioObj = bundle.LoadAsset<GameObject>("Assets/Audios/Prefabs/StoneImpactAudio.prefab"), false },
            { DoorImpact1AudioObj = bundle.LoadAsset<GameObject>("Assets/Audios/Prefabs/DoorImpact1Audio.prefab"), true },
            { DoorImpact2AudioObj = bundle.LoadAsset<GameObject>("Assets/Audios/Prefabs/DoorImpact2Audio.prefab"), true },
            { DoorImpact3AudioObj = bundle.LoadAsset<GameObject>("Assets/Audios/Prefabs/DoorImpact3Audio.prefab"), true }
        };

        foreach (KeyValuePair<GameObject, bool> kvpObj in gameObjects)
        {
            GameObject gameObject = kvpObj.Key;
            NetworkPrefabs.RegisterNetworkPrefab(gameObject);
            Utilities.FixMixerGroups(gameObject);
            if (kvpObj.Value)
                LFCPrefabRegistry.RegisterPrefab(gameObject.name, gameObject);
        }
    }
}
