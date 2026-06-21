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
    public string requiredTag = "Necklace";

    [Tooltip("The step number this item represents in the order (0 = Necklace, 1 = Cat, 2 = Dagger, 3 = Pot).")]
    public int requiredOrderIndex = 0;

    [Header("Baseline Settings")]
    [Tooltip("How close an item must be dropped to lock in place when sockets are disabled.")]
    public float baselineDropDistance = 0.5f;

    private XRSocketInteractor _socket;
    private bool _isTaskCompleted = false;
    private bool _isOptimizedMode = true;

    // Track listeners to clean them up properly on scene exit
    private List<XRGrabInteractable> _trackedBaselineInteractables = new List<XRGrabInteractable>();

    private void Start()
    {
        _socket = GetComponent<XRSocketInteractor>();
        _isOptimizedMode = SessionManager.Instance != null && SessionManager.Instance.IsOptimized;

        if (_isOptimizedMode)
        {
            // OPTIMIZED MODE: Keep socket active and keep hover meshes visible
            if (_socket != null)
            {
                _socket.enabled = true;
                _socket.showInteractableHoverMeshes = true;
            }
        }
        else
        {
            // BASELINE MODE: Completely disable the socket component to stop auto-snapping
            if (_socket != null)
            {
                _socket.enabled = false;
            }

            // Find all potential puzzle items to track manual drop placement attempts
            string[] puzzleTags = { "Necklace", "Cat", "Dagger", "Pot" };
            foreach (string t in puzzleTags)
            {
                GameObject[] items = GameObject.FindGameObjectsWithTag(t);
                foreach (GameObject itemObj in items)
                {
                    if (itemObj.TryGetComponent<XRGrabInteractable>(out var interactable))
                    {
                        _trackedBaselineInteractables.Add(interactable);
                        interactable.selectExited.AddListener(OnBaselineItemReleased);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        foreach (var interactable in _trackedBaselineInteractables)
        {
            if (interactable != null)
            {
                interactable.selectExited.RemoveListener(OnBaselineItemReleased);
            }
        }
    }

    /// <summary>
    /// OPTIMIZED PATHWAY: Called automatically via your XR Socket Interactor's 
    /// UI/Inspector 'On Select Entered' dynamic event assignment.
    /// </summary>
    public void TryPlaceItem(SelectEnterEventArgs args)
    {
        if (_isTaskCompleted || !_isOptimizedMode) return;

        GameObject placedObject = args.interactableObject.transform.gameObject;
        ProcessItemPlacement(placedObject, isBaselineDrop: false);
    }

    /// <summary>
    /// BASELINE PATHWAY: Triggered manually when any key object is released in space.
    /// </summary>
    private void OnBaselineItemReleased(SelectExitEventArgs args)
    {
        if (_isTaskCompleted) return;

        GameObject releasedObject = args.interactableObject.transform.gameObject;

        // Calculate physical proximity to this specific slot coordinate
        float distance = Vector3.Distance(releasedObject.transform.position, transform.position);

        // If it wasn't dropped near this slot, disregard completely
        if (distance > baselineDropDistance) return;

        ProcessItemPlacement(releasedObject, isBaselineDrop: true);
    }

    /// <summary>
    /// Core logic engine that handles order checks, tags, telemetry, and feedback
    /// </summary>
    private void ProcessItemPlacement(GameObject placedObject, bool isBaselineDrop)
    {
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
            CompleteSlotTask(placedObject.transform.position);

            if (isBaselineDrop)
            {
                // Force a manual position lock since the socket engine is turned off
                StartCoroutine(BaselineHardLockRoutine(placedObject, transform));
            }
        }
        else
        {
            RejectItem(placedObject);
        }
    }

    private void CompleteSlotTask(Vector3 snapPosition)
    {
        _isTaskCompleted = true;

        // Sound only plays in optimized conditions
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
                activeSequenceIndex = requiredOrderIndex
            });
        }
    }

    private IEnumerator BaselineHardLockRoutine(GameObject item, Transform socketTarget)
    {
        // Match the layout coordinates of the socket anchor point
        Vector3 finalPos = socketTarget.position;
        Quaternion finalRot = socketTarget.rotation;

        // Strip interactivity so the user cannot pull it back out of the layout sequence
        if (item.TryGetComponent<XRGrabInteractable>(out var interactable))
            Destroy(interactable);

        yield return null;

        // Lock physics state completely
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        item.transform.position = finalPos;
        item.transform.rotation = finalRot;
    }
}