using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class TombTaskSlot : MonoBehaviour
{
    [Header("Task Validation")]
    [Tooltip("The required Unity tag for the object to complete this specific slot.")]
    public string requiredTag = "Necklace";

    [Tooltip("The step number this item represents in the order (0 = Necklace, 1 = Cat, 2 = Dagger, 3 = Pot).")]
    public int requiredOrderIndex = 0;

    private bool _isTaskCompleted = false;

    /// <summary>
    /// Call this from your XR Socket Interactor's 'On Select Entered' event.
    /// This method is designed for the Dynamic SelectEnterEventArgs event.
    /// </summary>
    public void TryPlaceItem(SelectEnterEventArgs args)
    {
        if (_isTaskCompleted) return;

        // Extract the actual object that was placed into the socket
        GameObject placedObject = args.interactableObject.transform.gameObject;

        // 1. STAGE ORDER CHECK
        if (TutorialManager.Instance != null)
        {
            int currentActiveStep = TutorialManager.Instance.currentTaskIndex;

            if (requiredOrderIndex != currentActiveStep)
            {
                Debug.LogWarning($"[TombTaskSlot] Out of order! Required: {requiredOrderIndex}, Active: {currentActiveStep}.");
                RejectItem(placedObject);
                return;
            }
        }

        // 2. TAG VALIDATION
        if (placedObject.CompareTag(requiredTag))
        {
            CompleteSlotTask();
        }
        else
        {
            RejectItem(placedObject);
        }
    }

    private void CompleteSlotTask()
    {
        _isTaskCompleted = true;

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.CompleteTask(requiredOrderIndex);
        }

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("tomb_sequence_step_success", new
            {
                itemPlaced = requiredTag,
                sequenceIndex = requiredOrderIndex
            });
        }
    }

    private void RejectItem(GameObject placedObject)
    {
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("tomb_sequence_step_wrong_attempt", new
            {
                attemptedTag = placedObject.tag,
                expectedTag = requiredTag,
                activeSequenceIndex = requiredOrderIndex
            });
        }
    }
}