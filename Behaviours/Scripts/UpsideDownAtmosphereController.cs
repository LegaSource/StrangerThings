using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace StrangerThings.Behaviours.Scripts;

public class UpsideDownAtmosphereController : MonoBehaviour
{
    public static UpsideDownAtmosphereController Instance { get; private set; }

    public Volume volume;
    public Fog fog;
    public GradientSky sky;

    public ParticleSystem spores;
    public AudioSource audioVibe;

    private bool isInUpsideDown = false;

    // -------------------- Lightning --------------------
    public Color lightningColor = new Color(1f, 0.1f, 0.1f);
    public float lightningDuration = 1.5f;
    private bool lightningActive;
    private float lightningTimer;

    // -------------------- Fog Settings --------------------
    public Color outdoorFog = new Color(0.25f, 0.45f, 0.9f);
    public Color indoorFog = new Color(0.15f, 0.25f, 0.4f);
    [Range(0f, 1f)] public float fogVariation = 0.4f;
    public float fogSpeed = 0.6f;

    public float baseMeanFreePath = 150f;
    public float densityVariation = 0.6f;
    public float densitySpeed = 0.5f;

    // -------------------- Sky Settings --------------------
    public Color skyTopDay = new Color(0.25f, 0.45f, 1f);
    public Color skyTopNight = new Color(0.05f, 0.1f, 0.25f);
    public Color skyMidDay = new Color(0.2f, 0.35f, 0.7f);
    public Color skyMidNight = new Color(0.05f, 0.08f, 0.2f);
    public Color skyBotDay = new Color(0.15f, 0.25f, 0.45f);
    public Color skyBotNight = new Color(0.03f, 0.05f, 0.15f);

    public float skySpeed = 0.4f;
    private float dayFactor;

    // -------------------- Storm override --------------------
    private static StormyWeather forcedStorm;
    private static bool wasStormy;

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

        if (!volume.profile.TryGet(out fog)) StrangerThings.mls.LogWarning("[UpsideDown] Missing Fog override in Volume.");
        if (!volume.profile.TryGet(out sky)) StrangerThings.mls.LogWarning("[UpsideDown] Missing GradientSky override in Volume.");

        SetUpsideDownState(false);
    }

    public void SetUpsideDownState(bool enable)
    {
        isInUpsideDown = enable;
        gameObject.SetActive(enable);

        if (enable) audioVibe?.Play();
        else audioVibe?.Pause();

        EnableStormLogic();
    }

    private void EnableStormLogic()
    {
        wasStormy = StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Stormy;

        StormyWeather storm = FindObjectsByType<StormyWeather>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();

        if (storm == null) return;

        if (isInUpsideDown)
        {
            if (!wasStormy)
            {
                storm.gameObject.SetActive(true);
                forcedStorm = storm;
            }
        }
        else
        {
            if (!wasStormy && forcedStorm != null)
            {
                forcedStorm.gameObject.SetActive(false);
                forcedStorm = null;
            }
        }
    }

    private void Update()
    {
        if (!isInUpsideDown) return;

        ComputeDayFactor();
        AnimateFog();
        AnimateSky();
        UpdateLightning();
    }

    // -----------------------------------------------------
    //                       LIGHTNING
    // -----------------------------------------------------
    public void TriggerLightning()
    {
        if (!isInUpsideDown || GameNetworkManager.Instance.localPlayerController.isInsideFactory) return;

        lightningActive = true;
        lightningTimer = 0f;
    }

    private void UpdateLightning()
    {
        if (!lightningActive) return;

        lightningTimer += Time.deltaTime;
        if (lightningTimer >= lightningDuration) lightningActive = false;
    }

    // -----------------------------------------------------
    //                      DAY FACTOR
    // -----------------------------------------------------
    private void ComputeDayFactor()
    {
        Light sun = TimeOfDay.Instance.sunDirect;
        dayFactor = sun != null
            ? Mathf.Clamp01((Vector3.Dot(sun.transform.forward, Vector3.down) * 0.5f) + 0.5f)
            : (Mathf.Sin(Time.time * 0.05f) * 0.5f) + 0.5f;
    }

    // -----------------------------------------------------
    //                       FOG
    // -----------------------------------------------------
    private void AnimateFog()
    {
        if (fog == null) return;

        bool inside = GameNetworkManager.Instance.localPlayerController.isInsideFactory;

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
        fog.albedo.value = animatedColor * intensity;

        float dp = (Mathf.Sin(Time.time * densitySpeed) * 0.5f) + 0.5f;
        float meanFP = baseMeanFreePath * Mathf.Lerp(1f - densityVariation, 1f + densityVariation, dp);

        if (inside) meanFP *= 0.8f;

        fog.meanFreePath.value = Mathf.Clamp(meanFP, 50f, 500f);
    }

    // -----------------------------------------------------
    //                        SKY
    // -----------------------------------------------------
    private void AnimateSky()
    {
        if (sky == null) return;

        bool inside = GameNetworkManager.Instance.localPlayerController.isInsideFactory;

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

        sky.top.value = Color.Lerp(top * (1 - pulse), top * (1 + pulse), cycle);
        sky.middle.value = Color.Lerp(mid * (1 - pulse), mid * (1 + pulse), cycle);
        sky.bottom.value = Color.Lerp(bot * (1 - pulse), bot * (1 + pulse), cycle);
    }
}
