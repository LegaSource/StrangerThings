using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalLib.Modules;
using StrangerThings.Managers;
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
    private static readonly AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "strangerthings"));
    internal static ManualLogSource mls;
    public static ConfigFile configFile;

    public static GameObject managerPrefab = NetworkPrefabs.CreateNetworkPrefab("StrangerThingsNetworkManager");

    public static GameObject upsideDownAtmosphere;
    public static GameObject upsideDownPortal;

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
        //LoadShaders();

        harmony.PatchAll(typeof(NetworkBehaviourPatch));
        harmony.PatchAll(typeof(StartOfRoundPatch));
        harmony.PatchAll(typeof(PlayerControllerBPatch));
        harmony.PatchAll(typeof(StormyWeatherPatch));
        harmony.PatchAll(typeof(LightningBoltScriptPatch));
        harmony.PatchAll(typeof(DoorLockPatch));
        harmony.PatchAll(typeof(EnemyAIPatch));
        harmony.PatchAll(typeof(FlowerSnakeEnemyPatch));
        harmony.PatchAll(typeof(HoarderBugAIPatch));
        harmony.PatchAll(typeof(SandSpiderWebTrapPatch));
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

    public static void LoadItems()
    {
        /*if (ConfigManager.isHolyWater.Value)
            Add(typeof(HolyWater), bundle.LoadAsset<Item>("Assets/HolyWater/HolyWaterItem.asset"), ConfigManager.minHolyWater.Value, ConfigManager.maxHolyWater.Value, ConfigManager.holyWaterRarity.Value);*/
    }

    public static void LoadEnemies()
    {
        EnemyType demogorgonEnemy = bundle.LoadAsset<EnemyType>("Assets/Demogorgon/DemogorgonEnemy.asset");
        NetworkPrefabs.RegisterNetworkPrefab(demogorgonEnemy.enemyPrefab);
        Enemies.RegisterEnemy(demogorgonEnemy, ConfigManager.demogorgonRarity.Value, Levels.LevelTypes.All, null, null);
    }

    public static void LoadPrefabs() => upsideDownAtmosphere = bundle.LoadAsset<GameObject>("Assets/UpsideDown/UpsideDownAtmosphere.prefab");

    public void LoadNetworkPrefabs()
    {
        HashSet<GameObject> gameObjects =
        [
            (upsideDownPortal = bundle.LoadAsset<GameObject>("Assets/Portal/UpsideDownPortal.prefab"))
        ];

        foreach (GameObject gameObject in gameObjects)
        {
            NetworkPrefabs.RegisterNetworkPrefab(gameObject);
            Utilities.FixMixerGroups(gameObject);
        }
    }

    //public static void LoadShaders() => upsideDownInfection = bundle.LoadAsset<Material>("Assets/Shaders/UpsideDownInfection.mat");
}
