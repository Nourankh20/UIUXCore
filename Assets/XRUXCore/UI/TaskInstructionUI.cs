using UnityEngine;
using TMPro;

public class TaskInstructionUI : MonoBehaviour
{
    [Header("Static / Standard UI Components")]
    public GameObject staticCanvasParent;      // The parent object to turn on/off
    public TextMeshProUGUI staticInstructionText;

    [Header("Optimized Hand Menu UI Components")]
    public GameObject optimizedCanvasParent;  // The parent object to turn on/off
    public TextMeshProUGUI optimizedInstructionText;

    private bool _isCurrentLayoutOptimized; // Cache the layout state for telemetry tracking

    /// <summary>
    /// Call this from TutorialManager to update the visual state.
    /// </summary>
    public void SetLayoutMode(bool isOptimized)
    {
        _isCurrentLayoutOptimized = isOptimized;

        // 1. Explicitly toggle the entire GameObject hierarchy state first
        if (optimizedCanvasParent != null) optimizedCanvasParent.SetActive(isOptimized);
        if (staticCanvasParent != null) staticCanvasParent.SetActive(!isOptimized);
    }

    /// <summary>
    /// Now simply takes the instruction string provided by the TutorialManager's Inspector list.
    /// </summary>
    public void ShowStep(string instructionText)
    {
        Debug.Log($"[UI_DEBUG] Pushing step text: '{instructionText}'");

        // 1. Set the text properties safely using the string passed from the Inspector
        if (staticInstructionText != null)
        {
            staticInstructionText.text = instructionText;
        }

        if (optimizedInstructionText != null)
        {
            optimizedInstructionText.text = instructionText;
        }

        // 2. Log the "instruction_given" event
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("instruction_given", new
            {
                instructionText = instructionText, // Just logging the text itself now
                layoutMode = _isCurrentLayoutOptimized ? "optimized_hand_menu" : "static_world_canvas",
                timestamp = Time.time
            });
        }
        else
        {
            Debug.LogWarning("[TaskInstructionUI] TelemetryManager.Instance is missing! Could not log instruction event.");
        }
    }
}