using UnityEngine;

[CreateAssetMenu(fileName = "UX_Profile", menuName = "XRUXCore/UX Profile")]
public class UXProfileSO : ScriptableObject
{
    // ── General ─────────────────────────────────────
    public string profileName;       // "Baseline" | "Optimized"
    public bool isOptimized;         // Used for quick bool checks

    // ── Interaction & Snapping ───────────────────────
    public bool snappingEnabled;
    public float snapDistanceThreshold; // 0.15 m
    public float snapAngleThreshold;    // 45 degrees

    // ── Locomotion ──────────────────────────────────
    public bool hybridLocomotion;    // Optimized: true
    public bool teleportOnly;        // Baseline: true
    public bool allowContinuousMove; // Added for XRRigConfigurator logic

    // ── Feedback & Comfort ──────────────────────────
    public bool useHaptics;
    public bool useAudioFeedback;
    public bool useVisualGlow;
    public float feedbackIntensity;  // 0.3 baseline / 1.0 optimized
    public bool enableComfortVignette;
    [Range(0f, 1f)]
    public float defaultVignetteIntensity = 0.5f; // Added for the vignette controller

    // ── UI Style ────────────────────────────────────
    public bool useDiegeticWristUI;  // Optimized
    public bool useFixedHUD;         // Baseline

    // ── Input Method ────────────────────────────────
    public bool useHandTracking;     // Optimized
    public bool useController;       // Baseline
}

//using UnityEngine;

//[CreateAssetMenu(fileName = "UXProfileSO", menuName = "Scriptable Objects/UXProfileSO", order = 1)]
//public class UXProfileSO : ScriptableObject
//{
//    [Header("General")]
//    public string profileName = "New Profile"; //e.g., "Optimized" or "Baseline"
//    public bool isOptimized = true; // Quick flag for condition checks

//    [Header("Interaction & Snapping")]
//    public bool snappingEnabled = true;
//    public float snapDistanceThreshold = 0.15f; //Smaller = more precise (Optimized)
//    public float snapAngleThreshold = 45f; // Degrees for orientation match

//    [Header("Locomotion")]
//    public bool hybridLocomotion = true; // true = smooth + teleport
//    public bool teleportOnly = false; // For baseline

//    [Header("Feedback & Comfort")]
//    public bool useHaptics = true;
//    public bool useAudioFeedback = true;
//    public bool useVisualGlow = true;
//    public float feedbackIntensity = 1f; // 0-1 scale (lower for Baseline)
//    public bool enableComfortVignette = true;

//    [Header("UI Style")]
//    public bool useDiegeticWristUI = true; //Optimized: wrist-mounted
//    public bool useFixedHUD = false; //Baseline: peripheral panels

//    [Header("Interaction")]
//    public bool useHandTracking = true; //Optimized: hand tracking
//    public bool useController = false; //Baseline: controller visualized

//    //Optional: Add more domains later (fidelity, lighting stability, etc.)
//}
