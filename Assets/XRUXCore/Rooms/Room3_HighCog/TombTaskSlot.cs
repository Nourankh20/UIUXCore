using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class TombTaskSlot : MonoBehaviour
{
    [Header("Task Validation")]
    [Tooltip("The required Unity tag for the object to complete this specific slot.")]
    public string requiredTag = "Pottery";

    [Tooltip("The step number this item represents in the order (0 = Necklace, 1 = Cat, 2 = Dagger, 3 = Pot).")]
    public int requiredOrderIndex = 0;

    [Header("Baseline Settings")]
    [Tooltip("How close an item must be dropped to lock in place when sockets are disabled.")]
    public float baselineDropDistance = 0.6f;

    private XRSocketInteractor _socket;
    private bool _isTaskCompleted = false;
    private bool _isOptimizedMode = true;

    private List<XRGrabInteractable> _trackedBaselineInteractables = new List<XRGrabInteractable>();

    private void Start()
    {
        _socket = GetComponent<XRSocketInteractor>();
        _isOptimizedMode = SessionManager.Instance != null && SessionManager.Instance.IsOptimized;

        if (_isOptimizedMode)
        {
            if (_socket != null)
            {
                _socket.enabled = true;
                _socket.showInteractableHoverMeshes = true;
            }
        }
        else
        {
            if (_socket != null)
            {
                _socket.enabled = false;
            }

            XRGrabInteractable[] allInteractables = FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            foreach (var interactable in allInteractables)
            {
                _trackedBaselineInteractables.Add(interactable);
                interactable.selectExited.AddListener(OnBaselineItemReleased);
            }

            Debug.Log($"[TombTaskSlot-{gameObject.name}] Baseline setup complete. Listening to {_trackedBaselineInteractables.Count} interactables in scene.");
        }
    }

    private void OnDestroy()
    {
        foreach (var interactable in _trackedBaselineInteractables)
        {
            if (interactable != null)
            {
                interactable.selectExited.RemoveListener(OnBaselineItemReleased);
            }
        }
    }

    public void TryPlaceItem(SelectEnterEventArgs args)
    {
        if (_isTaskCompleted || !_isOptimizedMode) return;

        GameObject placedObject = args.interactableObject.transform.gameObject;
        ProcessItemPlacement(placedObject, isBaselineDrop: false);
    }

    private void OnBaselineItemReleased(SelectExitEventArgs args)
    {
        if (_isTaskCompleted) return;

        GameObject releasedObject = args.interactableObject.transform.gameObject;
        float distance = Vector3.Distance(releasedObject.transform.position, transform.position);

        // check if this is the target item intended for this slot position
        if (releasedObject.CompareTag(requiredTag))
        {
            Debug.Log($"[TombTaskSlot-{gameObject.name}] Target item '{releasedObject.name}' dropped at distance: {distance:F2}m (Allowed Threshold: {baselineDropDistance}m).");

            // CRITICAL ERROR LOG: Correct item dropped, but misses physical baseline proximity parameters
            if (distance > baselineDropDistance)
            {
                Debug.LogWarning($"[TombTaskSlot-{gameObject.name}] Distance Error! Target item dropped outside completion target radius.");

                // 1. Log to the TutorialManager tracking step runtime error counts
                TutorialManager.Instance?.LogStepError();

                // 2. Transmit discrete distance error data object payload packet to MongoDB server collection
                TelemetryManager.Instance?.LogEvent("tomb_baseline_distance_error", new
                {
                    itemTag = requiredTag,
                    measuredDistance = distance,
                    thresholdMax = baselineDropDistance,
                    targetSlotIndex = requiredOrderIndex
                });
                return;
            }
        }
        else
        {
            // If it's a completely unrelated object dropped completely outside range, ignore it safely
            if (distance > baselineDropDistance) return;
        }

        ProcessItemPlacement(releasedObject, isBaselineDrop: true);
    }

    private void ProcessItemPlacement(GameObject placedObject, bool isBaselineDrop)
    {
        if (TutorialManager.Instance != null)
        {
            int currentActiveStep = TutorialManager.Instance.currentTaskIndex;

            if (requiredOrderIndex != currentActiveStep)
            {
                if (placedObject.CompareTag(requiredTag))
                {
                    Debug.LogWarning($"[TombTaskSlot-{gameObject.name}] Out of order! Slot expects step {requiredOrderIndex}, but TutorialManager is active on step {currentActiveStep}.");

                    // CRITICAL ERROR LOG: Item matches this slot, but the user attempted placement out of order sequence
                    TutorialManager.Instance.LogStepError();
                }

                RejectItem(placedObject);
                return;
            }
        }

        if (placedObject.CompareTag(requiredTag))
        {
            CompleteSlotTask(placedObject.transform.position);

            if (isBaselineDrop)
            {
                StartCoroutine(BaselineHardLockRoutine(placedObject, transform));
            }
        }
        else
        {
            // CRITICAL ERROR LOG: Wrong item intentionally dropped directly into this slot's zone boundaries
            TutorialManager.Instance?.LogStepError();
            RejectItem(placedObject);
        }
    }

    private void CompleteSlotTask(Vector3 snapPosition)
    {
        _isTaskCompleted = true;
        Debug.Log($"[TombTaskSlot-{gameObject.name}] Task verified! Successfully matched tag '{requiredTag}' for step index {requiredOrderIndex}.");

        if (_isOptimizedMode)
        {
            FeedbackManager.Instance?.PlaySnapSuccess(snapPosition);
        }

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
                activeSequenceIndex = TutorialManager.Instance != null ? TutorialManager.Instance.currentTaskIndex : requiredOrderIndex
            });
        }
    }

    private IEnumerator BaselineHardLockRoutine(GameObject item, Transform socketTarget)
    {
        Vector3 finalPos = socketTarget.position;
        Quaternion finalRot = socketTarget.rotation;

        if (item.TryGetComponent<XRGrabInteractable>(out var interactable))
            Destroy(interactable);

        yield return null;

        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        item.transform.position = finalPos;
        item.transform.rotation = finalRot;
    }
}