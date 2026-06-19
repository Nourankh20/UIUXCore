using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TunnelingVignetteController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Volume globalVolume;

    private Vignette _vignette;

    void Awake()
    {
        if (globalVolume != null && globalVolume.profile.TryGet(out _vignette))
        {
            // Start with it disabled/invisible
            _vignette.active = false;
            _vignette.intensity.value = 0f;
        }
    }

    /// <summary>
    /// Toggles the tunneling effect based on the UX profile.
    /// </summary>
    public void SetEnabled(bool isEnabled, float intensity = 0.5f)
    {
        if (_vignette == null) return;

        _vignette.active = isEnabled;
        _vignette.intensity.value = isEnabled ? intensity : 0f;
    }

    /// <summary>
    /// Dynamically updates the intensity (useful for increasing tunneling during fast movement).
    /// </summary>
    public void UpdateIntensity(float intensity)
    {
        if (_vignette != null && _vignette.active)
        {
            _vignette.intensity.value = Mathf.Clamp01(intensity);
        }
    }
}