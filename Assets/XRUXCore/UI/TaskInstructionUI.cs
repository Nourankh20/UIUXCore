using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class TaskInstructionUI : MonoBehaviour
{
    [Header("Static / Standard UI Components")]
    public GameObject staticCanvasParent;
    public TextMeshProUGUI staticInstructionText;

    [Header("Optimized Hand Menu UI Components")]
    public GameObject optimizedCanvasParent;
    public TextMeshProUGUI optimizedInstructionText;

    private bool _isCurrentLayoutOptimized;

    public void SetLayoutMode(bool isOptimized)
    {
        _isCurrentLayoutOptimized = isOptimized;
        if (optimizedCanvasParent != null) optimizedCanvasParent.SetActive(isOptimized);
        if (staticCanvasParent != null) staticCanvasParent.SetActive(!isOptimized);
    }

    // Used for Optimized Mode (Single Step)
    public void ShowStep(string instructionText)
    {
        if (optimizedInstructionText != null) optimizedInstructionText.text = instructionText;
        if (staticInstructionText != null) staticInstructionText.text = instructionText;

        LogInstructionTelemetry(instructionText);
    }

    // Used for Baseline Mode (Full Board)
    public void ShowAllSteps(List<RoomTask> allTasks, int currentTaskIndex)
    {
        if (staticInstructionText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<size=120%><b>MISSION OBJECTIVES</b></size>\n");

        for (int i = 0; i < allTasks.Count; i++)
        {
            if (i < currentTaskIndex)
                sb.AppendLine($"<s>[✓] {allTasks[i].instructionText}</s>");
            else if (i == currentTaskIndex)
                sb.AppendLine($"<color=#FFFF00><b>[>] {allTasks[i].instructionText}</b></color>");
            else
                sb.AppendLine($"[ ] {allTasks[i].instructionText}");
        }

        staticInstructionText.text = sb.ToString();

        // Log the current active task from the list
        LogInstructionTelemetry(allTasks[currentTaskIndex].instructionText);
    }

    private void LogInstructionTelemetry(string text)
    {
        Debug.Log($"[UI_DEBUG] Pushing step text: '{text}'");

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("instruction_given", new
            {
                instructionText = text,
                layoutMode = _isCurrentLayoutOptimized ? "optimized_hand_menu" : "static_world_canvas",
                timestamp = Time.time
            });
        }
        else
        {
            Debug.LogWarning("[TaskInstructionUI] TelemetryManager.Instance is missing!");
        }
    }
}