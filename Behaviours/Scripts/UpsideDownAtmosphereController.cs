using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace StrangerThings.Behaviours.Scripts;

public class UpsideDownAtmosphereController : MonoBehaviour
{
    public static UpsideDownAtmosphereController Instance { get; private set; }

    public Volume Volume;
    public Fog Fog;
    public GradientSky Sky;

    public AudioSource AudioVibe;
    public ParticleSystem Spores;
    public GameObject BatsSky;
    public HashSet<GameObject> AliveTrees = [];
    public HashSet<GameObject> DeadTrees = [];

    private bool isInUpsideDown = false;

    public Color lightningColor = new Color(1f, 0.1f, 0.1f);
    public float lightningDuration = 1.5f;
    private bool lightningActive;
    private float lightningTimer;

    public Color outdoorFog = new Color(0.25f, 0.45f, 0.9f);
    public Color indoorFog = new Color(0.15f, 0.25f, 0.4f);
    [Range(0f, 1f)] public float fogVariation = 0.4f;
    public float fogSpeed = 0.6f;

    public float baseMeanFreePath = 150f;
    public float densityVariation = 0.6f;
    public float densitySpeed = 0.5f;

    public Color skyTopDay = new Color(0.25f, 0.45f, 1f);
    public Color skyTopNight = new Color(0.05f, 0.1f, 0.25f);
    public Color skyMidDay = new Color(0.2f, 0.35f, 0.7f);
    public Color skyMidNight = new Color(0.05f, 0.08f, 0.2f);
    public Color skyBotDay = new Color(0.15f, 0.25f, 0.45f);
    public Color skyBotNight = new Color(0.03f, 0.05f, 0.15f);

    public float skySpeed = 0.4f;
    private float dayFactor;

    public void Awake()
    {
        if (Instance != null)
        {
            StrangerThings.mls.LogWarning("[UpsideDown] Duplicate controller destroyed.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!Volume.profile.TryGet(out Fog)) StrangerThings.mls.LogWarning("[UpsideDown] Missing Fog override in Volume.");
        if (!Volume.profile.TryGet(out Sky)) StrangerThings.mls.LogWarning("[UpsideDown] Missing GradientSky override in Volume.");
        if (AudioVibe != null) AudioVibe.volume = ConfigManager.upsideDownVolume.Value;

        SetUpsideDownState(false);
    }

    public void SetUpsideDownState(bool enable)
    {
        isInUpsideDown = enable;
        gameObject.SetActive(enable);
        Spores?.gameObject.SetActive(enable);
        BatsSky?.SetActive(enable);

        if (enable)
            AudioVibe?.Play();
        else
            AudioVibe?.Pause();

        foreach (GameObject aliveTree in AliveTrees)
            aliveTree.SetActive(!enable);
        foreach (GameObject deadTree in DeadTrees)
            deadTree.SetActive(enable);

        EnableStormLogic();
    }

    private void EnableStormLogic()
    {
        bool wasStormy = StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy;
        StormyWeather storm = FindObjectsByType<StormyWeather>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (storm == null) return;

        if (isInUpsideDown)
        {
            if (!wasStormy)
                storm.gameObject.SetActive(true);
        }
        else
        {
            if (!wasStormy && storm != null)
            {
                storm.staticElectricityParticle.Stop();
                storm.staticElectricityParticle.GetComponent<AudioSource>().Stop();
                storm.setStaticToObject = null;
            }
        }
    }

    private void Update()
    {
        if (isInUpsideDown && LFCUtilities.LocalPlayer != null)
        {
            ComputeDayFactor();
            AnimateFog();
            AnimateSky();
            UpdateLightning();
            if (Spores != null)
                Spores.transform.position = (Vector3)(LFCUtilities.LocalPlayer?.gameplayCamera.transform.position + (Vector3.forward * 2f));
        }
    }

    public void TriggerLightning()
    {
        if (isInUpsideDown && LFCUtilities.LocalPlayer != null && !LFCUtilities.LocalPlayer.isInsideFactory)
        {
            lightningActive = true;
            lightningTimer = 0f;
        }
    }

    private void UpdateLightning()
    {
        if (lightningActive)
        {
            lightningTimer += Time.deltaTime;
            if (lightningTimer >= lightningDuration) lightningActive = false;
        }
    }

    private void ComputeDayFactor()
    {
        Light sun = TimeOfDay.Instance?.sunDirect;
        dayFactor = sun != null && sun.transform != null
            ? Mathf.Clamp01((Vector3.Dot(sun.transform.forward, Vector3.down) * 0.5f) + 0.5f)
            : (Mathf.Sin(Time.time * 0.05f) * 0.5f) + 0.5f;
    }

    private void AnimateFog()
    {
        if (LFCUtilities.LocalPlayer == null || Fog == null) return;

        bool inside = LFCUtilities.LocalPlayer.isInsideFactory;
        Color baseColor = inside ? indoorFog : outdoorFog;
        float t = (Mathf.Sin(Time.time * fogSpeed) * 0.5f) + 0.5f;
        Color animatedColor = Color.Lerp(
            baseColor * (1f - fogVariation),
            baseColor * (1f + fogVariation),
            t
        );

        if (lightningActive && !inside)
        {
            float blend = 1f - (lightningTimer / lightningDuration);
            animatedColor = Color.Lerp(animatedColor, lightningColor, blend);
        }

        float intensity = Mathf.Lerp(0.5f, 2f, Mathf.Pow(dayFactor, 0.8f));
        Fog.albedo.value = animatedColor * intensity;

        float dp = (Mathf.Sin(Time.time * densitySpeed) * 0.5f) + 0.5f;
        float meanFP = baseMeanFreePath * Mathf.Lerp(1f - densityVariation, 1f + densityVariation, dp);

        if (inside)
            meanFP *= 0.8f;

        Fog.meanFreePath.value = Mathf.Clamp(meanFP, 50f, 500f);
    }

    private void AnimateSky()
    {
        if (LFCUtilities.LocalPlayer == null || Sky == null) return;

        bool inside = LFCUtilities.LocalPlayer.isInsideFactory;
        float cycle = (Mathf.Sin(Time.time * skySpeed) * 0.5f) + 0.5f;
        Color top = Color.Lerp(skyTopNight, skyTopDay, dayFactor);
        Color mid = Color.Lerp(skyMidNight, skyMidDay, dayFactor);
        Color bot = Color.Lerp(skyBotNight, skyBotDay, dayFactor);

        if (inside)
        {
            top *= 0.6f;
            mid *= 0.6f;
            bot *= 0.6f;
        }
        else if (lightningActive)
        {
            float blend = 1f - (lightningTimer / lightningDuration);
            Color tint = Color.Lerp(Color.white, lightningColor, blend);
            top = Color.Lerp(top, tint, blend * 0.5f);
            mid = Color.Lerp(mid, tint, blend * 0.5f);
            bot = Color.Lerp(bot, tint, blend * 0.5f);
        }

        float pulse = Mathf.Sin(Time.time * skySpeed * 2f) * 0.05f;

        Sky.top.value = Color.Lerp(top * (1 - pulse), top * (1 + pulse), cycle);
        Sky.middle.value = Color.Lerp(mid * (1 - pulse), mid * (1 + pulse), cycle);
        Sky.bottom.value = Color.Lerp(bot * (1 - pulse), bot * (1 + pulse), cycle);
    }
}
