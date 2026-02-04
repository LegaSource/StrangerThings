using GameNetcodeStuff;
using HarmonyLib;
using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System;
using System.Reflection;

namespace StrangerThings.ModsCompat;

public static class MelaniesVoiceSoftCompat
{
    private static PropertyInfo piPlayerAudioGroup;
    private static PropertyInfo piPlayerScript;
    private static PropertyInfo piGroupVolume;

    public static void Patch(Harmony harmony)
    {
        Type voiceControllerType = Type.GetType("com.github.zehsteam.MelaniesVoice.MonoBehaviours.VoiceController, com.github.zehsteam.MelaniesVoice");
        if (voiceControllerType != null)
        {
            MethodInfo updateVoiceVolume = AccessTools.Method(voiceControllerType, "UpdateVoiceVolume");
            if (updateVoiceVolume != null)
            {
                piPlayerAudioGroup = AccessTools.Property(voiceControllerType, "PlayerAudioGroup");
                piPlayerScript = AccessTools.Property(voiceControllerType, "PlayerScript");

                HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(MelaniesVoiceSoftCompat), nameof(UpdateVoiceVolume)));
                _ = harmony.Patch(updateVoiceVolume, postfix: postfix);
            }
        }
    }

    private static void UpdateVoiceVolume(object __instance)
    {
        if (__instance == null) return;

        PlayerControllerB localPlayer = LFCUtilities.LocalPlayer;
        if (localPlayer == null || !localPlayer.isPlayerControlled || localPlayer.isPlayerDead) return;

        PlayerControllerB targetedPlayer = piPlayerScript?.GetValue(__instance, null) as PlayerControllerB;
        if (LFCUtilities.ShouldNotBeLocalPlayer(targetedPlayer) && !DimensionRegistry.AreInSameDimension(localPlayer.gameObject, targetedPlayer.gameObject))
        {
            object playerAudioGroup = piPlayerAudioGroup?.GetValue(__instance, null);
            if (playerAudioGroup != null)
            {
                if (piGroupVolume == null)
                    piGroupVolume = AccessTools.Property(playerAudioGroup.GetType(), "Volume");

                float currentVolume = piGroupVolume != null ? (float)piGroupVolume.GetValue(playerAudioGroup, null) : 0f;
                if (currentVolume > 0f)
                {
                    float multiplier = DimensionRegistry.IsInUpsideDown(localPlayer.gameObject)
                    ? (localPlayer.speakingToWalkieTalkie && targetedPlayer.holdingWalkieTalkie) || (targetedPlayer.speakingToWalkieTalkie && localPlayer.holdingWalkieTalkie)
                        ? MapObjectsManager.IsNearAntennaHazard(localPlayer) ? 1f : 0f
                        : 0.1f
                    : (localPlayer.speakingToWalkieTalkie && targetedPlayer.holdingWalkieTalkie) || (targetedPlayer.speakingToWalkieTalkie && localPlayer.holdingWalkieTalkie)
                        ? MapObjectsManager.IsNearAntennaHazard(targetedPlayer) ? 1f : 0f
                        : 0f;

                    if (piGroupVolume != null && piGroupVolume.CanWrite)
                        piGroupVolume.SetValue(playerAudioGroup, currentVolume * multiplier, null);
                }
            }
        }
    }
}
