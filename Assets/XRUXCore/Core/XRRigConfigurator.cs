using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

public class XRRigConfigurator : MonoBehaviour
{
    [Header("Profile (Local Fallback)")]
    [Tooltip("This is automatically overridden by the master SessionManager at runtime. Use this slot ONLY for testing this room directly.")]
    public UXProfileSO profile;

    [Header("Locomotion")]
    public GameObject teleportProvider;
    public GameObject continuousMoveProvider;

    [Header("Input")]
    public GameObject handTrackingLeft;
    public GameObject handTrackingRight;
    public GameObject controllerLeft;
    public GameObject controllerRight;

    [Header("UI")]
    public GameObject fixedHUD;
    public GameObject wristUILeft;

    [Header("Comfort")]
    public TunnelingVignetteController vignetteController;

    void Start()
    {
        // FORCE PULL: Look at the master SessionManager instance surviving from the previous scene
        if (SessionManager.Instance != null && SessionManager.Instance.activeProfile != null)
        {
            profile = SessionManager.Instance.activeProfile;
            Debug.Log($"[XRRigConfigurator] Master Profile Found! Overriding local scene settings with: '{profile.profileName}'");
        }
        else
        {
            Debug.LogWarning("[XRRigConfigurator] Master SessionManager not found. Falling back to local scene profile asset.");
        }

        // Safety Check
        if (profile == null)
        {
            Debug.LogError("[XRRigConfigurator] CRITICAL error: No UXProfileSO profile found! Rig configuration aborted.");
            return;
        }

        // Apply the settings of whichever profile won
        ApplyProfile();

        TelemetryManager.Instance?.LogEvent("ux_profile_applied", new { profile = profile.profileName });
    }

    private void ApplyProfile()
    {
        // 1. Locomotion Logic
        if (teleportProvider != null)
            teleportProvider.SetActive(profile.teleportOnly || profile.hybridLocomotion);

        if (continuousMoveProvider != null)
            continuousMoveProvider.SetActive(profile.hybridLocomotion || profile.allowContinuousMove);

        // 2. Input mode
        if (handTrackingLeft != null) handTrackingLeft.SetActive(profile.useHandTracking);
        if (handTrackingRight != null) handTrackingRight.SetActive(profile.useHandTracking);

        if (controllerLeft != null) controllerLeft.SetActive(profile.useController);
        if (controllerRight != null) controllerRight.SetActive(profile.useController);

        // 3. UI mode
        if (fixedHUD != null) fixedHUD.SetActive(profile.useFixedHUD);
        if (wristUILeft != null) wristUILeft.SetActive(profile.useDiegeticWristUI);

        // 4. Comfort Vignette
        if (vignetteController != null)
        {
            vignetteController.SetEnabled(profile.enableComfortVignette, profile.defaultVignetteIntensity);
        }

        LogConfigurationTelemetry();
    }

    private void LogConfigurationTelemetry()
    {
        TelemetryManager.Instance?.LogEvent("ui_mode_change", new
        {
            uiMode = profile.useDiegeticWristUI ? "diegetic" : "fixed_hud",
            locomotion = profile.teleportOnly ? "teleport" : (profile.hybridLocomotion ? "hybrid" : "continuous"),
            input = profile.useHandTracking ? "hand" : "controller",
            comfortActive = profile.enableComfortVignette
        });
    }
}