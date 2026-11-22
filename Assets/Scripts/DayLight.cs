using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Turns the night level into a bright day: sunlight, bright ambient light, light fog, sky-blue background
/// and a lighter post-processing look. Called from GridManager.Awake, so nothing has to be set up in the scene.
/// Set <see cref="daytime"/> to false to get the dark night look back.
/// </summary>
public static class DayLight
{
    public static bool daytime = true;

    public static void Apply()
    {
        if (!daytime) return;

        // Ambient light, fog, sky.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.68f, 0.70f, 0.75f);
        RenderSettings.reflectionIntensity = 0.6f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 170f;

        // Sun.
        bool sunSet = false;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.intensity = 1.25f;
            l.color = new Color(1f, 0.96f, 0.86f);
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.75f;
            if (!sunSet)
            {
                l.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                sunSet = true;
            }
        }

        // Background behind the map.
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.78f, 0.95f);
        }

        // Post-processing: lighter and with less vignette (works on a copy of the profile, the asset stays untouched).
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            VolumeProfile p = v.profile;
            if (p == null) continue;

            if (p.TryGet(out Vignette vig))
            {
                vig.active = true;
                vig.intensity.Override(0.12f);
            }
            if (!p.TryGet(out ColorAdjustments ca)) ca = p.Add<ColorAdjustments>(true);
            ca.active = true;
            ca.postExposure.Override(0.6f);
            ca.colorFilter.Override(Color.white);
            ca.saturation.Override(8f);
            if (p.TryGet(out FilmGrain grain)) grain.intensity.Override(0.05f);
        }
        Debug.Log("[DayLight] Daytime lighting applied.");
    }
}
