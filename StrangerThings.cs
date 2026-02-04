using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LegaFusionCore.Managers;
using LethalLib.Modules;
using StrangerThings.Behaviours.Items;
using StrangerThings.Managers;
using StrangerThings.ModsCompat;
using StrangerThings.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StrangerThings;

[BepInPlugin(modGUID, modName, modVersion)]
public class StrangerThings : BaseUnityPlugin
{
    internal const string modGUID = "Lega.StrangerThings";
    internal const string modName = "Stranger Things";
    internal const string modVersion = "1.0.0";

    private readonly Harmony harmony = new Harmony(modGUID);
    internal static readonly AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "strangerthings"));
    internal static ManualLogSource mls;
    public static ConfigFile configFile;

    public static GameObject managerPrefab = NetworkPrefabs.CreateNetworkPrefab("StrangerThingsNetworkManager");

    public static Dictionary<EnemyType, int> upsideDownEnemies = [];
    public static GameObject upsideDownAtmosphere;
    public static GameObject upsideDownPortal;
    public static GameObject upsideDownMirrorObject;
    public static GameObject antennaHazard;
    public static GameObject rockProjectileObj;
    public static GameObject rockExplosionAudio;

    public static Item antennaItem;

    public static EnemyType limadonType;
    public static EnemyType crustopikanLarvaeType;

    public void Awake()
    {
        mls = BepInEx.Logging.Logger.CreateLogSource("StrangerThings");
        configFile = Config;
        ConfigManager.Load();

        LoadManager();
        NetcodePatcher();
        LoadItems();
        LoadEnemies();
        LoadPrefabs();
        LoadNetworkPrefabs();

        harmony.PatchAll(typeof(AudioMixerPatch));
        harmony.PatchAll(typeof(NetworkBehaviourPatch));
        harmony.PatchAll(typeof(StartOfRoundPatch));
        harmony.PatchAll(typeof(RoundManagerPatch));
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

        MelaniesVoiceSoftCompat.Patch(harmony);
        SelfSortingStorageSoftCompat.Patch(harmony);
        OpenBodyCamsSoftCompat.Patch(harmony);
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
        antennaItem = LFCObjectsManager.RegisterObject(typeof(AntennaItem), bundle.LoadAsset<Item>("Assets/Antenna/AntennaItem.asset"));
        Items.RegisterShopItem(antennaItem, price: 10);
    }

    public void LoadEnemies()
    {
        _ = RegisterEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Demogorgon/DemogorgonKidnapperEnemy.asset"),
            rarity: ConfigManager.demogorgonRarity.Value,
            levelTypes: Levels.LevelTypes.All,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Demogorgon/DemogorgonTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Demogorgon/DemogorgonTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Demogorgon/DemogorgonHeadItem.asset"),
            bodyMinValue: ConfigManager.demogorgonMinHeadValue.Value,
            bodyMaxValue: ConfigManager.demogorgonMaxHeadValue.Value);
        _ = RegisterEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Demogorgon/DemogorgonHunterEnemy.asset"),
            rarity: ConfigManager.demogorgonRarity.Value,
            levelTypes: Levels.LevelTypes.All,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Demogorgon/DemogorgonTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Demogorgon/DemogorgonTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Demogorgon/DemogorgonHeadItem.asset"),
            bodyMinValue: ConfigManager.demogorgonMinHeadValue.Value,
            bodyMaxValue: ConfigManager.demogorgonMaxHeadValue.Value);
        _ = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Crustapikan/CrustapikanEnemy.asset"),
            rarity: ConfigManager.crustapikanRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Crustapikan/CrustapikanTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Crustapikan/CrustapikanTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Crustapikan/CrustapikanArmItem.asset"),
            bodyMinValue: ConfigManager.crustapikanMinArmValue.Value,
            bodyMaxValue: ConfigManager.crustapikanMaxArmValue.Value);
        limadonType = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/Limadon/LimadonEnemy.asset"),
            rarity: ConfigManager.limadonRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/Limadon/LimadonTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/Limadon/LimadonTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/Limadon/LimadonCorpseItem.asset"),
            bodyMinValue: ConfigManager.limadonMinCorpseValue.Value,
            bodyMaxValue: ConfigManager.limadonMaxCorpseValue.Value);
        crustopikanLarvaeType = RegisterUpsideDownEnemy(enemyType: bundle.LoadAsset<EnemyType>("Assets/CrustapikanLarvae/CrustapikanLarvaeEnemy.asset"),
            rarity: ConfigManager.crustapikanLarvaeRarity.Value,
            terminalNode: bundle.LoadAsset<TerminalNode>("Assets/CrustapikanLarvae/CrustapikanLarvaeTN.asset"),
            terminalKeyword: bundle.LoadAsset<TerminalKeyword>("Assets/CrustapikanLarvae/CrustapikanLarvaeTK.asset"),
            bodyItem: bundle.LoadAsset<Item>("Assets/CrustapikanLarvae/CrustapikanLarvaeCorpseItem.asset"),
            bodyMinValue: ConfigManager.crustapikanLarvaeMinCorpseValue.Value,
            bodyMaxValue: ConfigManager.crustapikanLarvaeMaxCorpseValue.Value);
    }

    public EnemyType RegisterEnemy(EnemyType enemyType, int rarity, Levels.LevelTypes levelTypes, TerminalNode terminalNode, TerminalKeyword terminalKeyword, Item bodyItem, int bodyMinValue, int bodyMaxValue, bool bodyEnabled = true)
    {
        NetworkPrefabs.RegisterNetworkPrefab(enemyType.enemyPrefab);
        Enemies.RegisterEnemy(enemyType, rarity, levelTypes, terminalNode, terminalKeyword);
        SellBodiesFixedSoftCompat.RegisterBody(enemyType.enemyName, bodyItem, bodyMinValue, bodyMaxValue, bodyEnabled);
        return enemyType;
    }

    public EnemyType RegisterUpsideDownEnemy(EnemyType enemyType, int rarity, TerminalNode terminalNode, TerminalKeyword terminalKeyword, Item bodyItem, int bodyMinValue, int bodyMaxValue, bool bodyEnabled = true)
    {
        _ = RegisterEnemy(enemyType, rarity, Levels.LevelTypes.None, terminalNode, terminalKeyword, bodyItem, bodyMinValue, bodyMaxValue, bodyEnabled);
        upsideDownEnemies.Add(enemyType, rarity);
        return enemyType;
    }

    public void LoadPrefabs() => upsideDownAtmosphere = bundle.LoadAsset<GameObject>("Assets/UpsideDown/UpsideDownAtmosphere.prefab");

    public void LoadNetworkPrefabs()
    {
        HashSet<GameObject> gameObjects =
        [
            (upsideDownPortal = bundle.LoadAsset<GameObject>("Assets/UpsideDown/Portal/UpsideDownPortal.prefab")),
            (upsideDownMirrorObject = bundle.LoadAsset<GameObject>("Assets/Items/UpsideDownMirrorObject.prefab")),
            (antennaHazard = bundle.LoadAsset<GameObject>("Assets/Antenna/AntennaHazard.prefab")),
            (rockProjectileObj = bundle.LoadAsset<GameObject>("Assets/Crustapikan/RockProjectile.prefab")),
            (rockExplosionAudio = bundle.LoadAsset<GameObject>("Assets/Crustapikan/RockExplosionAudio.prefab"))
        ];

        foreach (GameObject gameObject in gameObjects)
        {
            NetworkPrefabs.RegisterNetworkPrefab(gameObject);
            Utilities.FixMixerGroups(gameObject);
        }
    }
}
