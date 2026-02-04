using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using StrangerThings.Registries;
using UnityEngine.Audio;

namespace StrangerThings.Patches;

public class AudioMixerPatch
{
    [HarmonyPatch(typeof(AudioMixer), nameof(AudioMixer.SetFloat))]
    [HarmonyBefore(["me.swipez.melonloader.morecompany"])]
    [HarmonyPrefix]
    private static void UpdatePlayerVolume(string name, ref float value)
    {
        if (StartOfRound.Instance == null || !name.StartsWith("PlayerVolume")) return;

        PlayerControllerB localPlayer = LFCUtilities.LocalPlayer;
        if (localPlayer == null || !localPlayer.isPlayerControlled || localPlayer.isPlayerDead) return;

        string playerSuffix = name["PlayerVolume".Length..];
        if (!int.TryParse(playerSuffix, out int playerIndex) || playerIndex < 0 || playerIndex >= StartOfRound.Instance.allPlayerScripts.Length) return;

        float multiplier = 1f;
        PlayerControllerB targetedPlayer = StartOfRound.Instance.allPlayerScripts[playerIndex];
        if (targetedPlayer != null && !DimensionRegistry.AreInSameDimension(localPlayer.gameObject, targetedPlayer.gameObject))
        {
            multiplier = DimensionRegistry.IsInUpsideDown(localPlayer.gameObject)
                ? (localPlayer.speakingToWalkieTalkie && targetedPlayer.holdingWalkieTalkie) || (targetedPlayer.speakingToWalkieTalkie && localPlayer.holdingWalkieTalkie)
                    ? MapObjectsManager.IsNearAntennaHazard(localPlayer) ? 1f : 0f
                    : 0.1f
                : (localPlayer.speakingToWalkieTalkie && targetedPlayer.holdingWalkieTalkie) || (targetedPlayer.speakingToWalkieTalkie && localPlayer.holdingWalkieTalkie)
                    ? MapObjectsManager.IsNearAntennaHazard(targetedPlayer) ? 1f : 0f
                    : 0f;
        }
        value *= multiplier;
    }
}
