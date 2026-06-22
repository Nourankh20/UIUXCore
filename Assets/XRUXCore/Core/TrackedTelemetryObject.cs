using UnityEngine;

/// <summary>
/// Attach this script to ANY puzzle piece, item, or interactable object.
/// </summary>
public class TrackedTelemetryObject : MonoBehaviour
{
    [Header("Telemetry Configuration")]
    [Tooltip("Type a clean ID with NO spaces (e.g., 'necklace', 'cat'). If left blank, it will auto-format the GameObject name.")]
    public string customId = "";

    // Use Start instead of OnEnable to ensure TelemetryManager is fully awake first
    void Start()
    {
        if (TelemetryManager.Instance != null)
        {
            // If customId is blank, use the GameObject name but replace spaces with underscores to prevent server 400 errors
            string finalId = string.IsNullOrEmpty(customId) ? gameObject.name.Replace(" ", "_") : customId;

            TelemetryManager.Instance.AddTrackedObject(this.transform, finalId);
        }
        else
        {
            Debug.LogWarning($"[Telemetry] Could not register {name} - TelemetryManager not found in scene.");
        }
    }

    void OnDisable()
    {
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.RemoveTrackedObject(this.transform);
        }
    }
}