using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using StrangerThings.Registries;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Patches;

public class StartOfRoundPatch
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void StartRound(ref StartOfRound __instance)
    {
        LFCShaderFilterRegistry.AddFilter($"{StrangerThings.modName}", ShouldRender);

        if (NetworkManager.Singleton.IsHost && StrangerThingsNetworkManager.Instance == null)
        {
            GameObject gameObject = Object.Instantiate(StrangerThings.managerPrefab, __instance.transform.parent);
            gameObject.GetComponent<NetworkObject>().Spawn();
            StrangerThings.mls.LogInfo("Spawning StrangerThingsNetworkManager");
        }
    }

    private static bool ShouldRender(GameObject gObject)
        => DimensionRegistry.AreInSameDimension(GameNetworkManager.Instance.localPlayerController.gameObject, gObject);

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipLeave))]
    [HarmonyPostfix]
    public static void EndRound()
    {
        PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;

        if (DimensionRegistry.IsInUpsideDown(player.gameObject))
            LFCNetworkManager.Instance.KillPlayerEveryoneRpc((int)player.playerClientId, Vector3.zero, false, (int)CauseOfDeath.Unknown);

        DimensionRegistry.upsideDownPortals.Clear();
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnDisable))]
    [HarmonyPostfix]
    public static void OnDisable() => StrangerThingsNetworkManager.Instance = null;

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.UpdatePlayerVoiceEffects))]
    [HarmonyPostfix]
    private static void UpdatePlayerVoiceEffects()
    {
        PlayerControllerB localPlayer = GameNetworkManager.Instance?.localPlayerController;
        if (localPlayer == null || !localPlayer.isPlayerControlled || localPlayer.isPlayerDead) return;

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (!LFCUtilities.ShouldNotBeLocalPlayer(player)
                || !player.isPlayerControlled
                || player.isPlayerDead
                || player.voicePlayerState == null
                || player.currentVoiceChatAudioSource == null)
            {
                continue;
            }

            float multiplier = 1f;

            if (DimensionRegistry.AreInSameDimension(localPlayer.gameObject, player.gameObject))
                multiplier = DimensionRegistry.IsInUpsideDown(localPlayer.gameObject) ? 0.5f : 0f;

            player.voicePlayerState.Volume = multiplier;
            player.currentVoiceChatAudioSource.volume = multiplier;
        }
    }
}
