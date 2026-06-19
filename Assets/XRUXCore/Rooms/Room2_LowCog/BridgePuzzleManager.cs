using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BridgePuzzleManager : MonoBehaviour
{
    [Header("Scene Sockets")]
    [Tooltip("Drag your XRSocketInteractor GameObjects here.")]
    public List<XRSocketInteractor> bridgeSockets = new List<XRSocketInteractor>();

    [Header("Environment Control")]
    [Tooltip("Drag any barriers/walls that should be disabled when the puzzle is solved.")]
    public List<GameObject> invisibleBarriers = new List<GameObject>();

    [Header("Baseline Settings")]
    public float baselineDropDistance = 0.5f;

    [Header("Tutorial Integration")]
    public TutorialManager tutorialManager;

    private int filledSlots = 0;
    private bool isOptimizedMode = true;

    private List<XRGrabInteractable> baselinePlanks = new List<XRGrabInteractable>();
    private Dictionary<XRSocketInteractor, GameObject> baselineOccupiedSockets = new Dictionary<XRSocketInteractor, GameObject>();

    private void Start()
    {
        isOptimizedMode = SessionManager.Instance != null && SessionManager.Instance.IsOptimized;

        // 1. Ensure barriers start ACTIVE so the player is blocked
        foreach (var barrier in invisibleBarriers)
        {
            if (barrier != null) barrier.SetActive(true);
        }

        // 2. Setup Sockets based on mode
        if (isOptimizedMode)
        {
            foreach (var socket in bridgeSockets)
            {
                if (socket != null)
                {
                    socket.enabled = true;
                    socket.showInteractableHoverMeshes = true;
                    socket.selectEntered.AddListener(OnOptimizedSocketFilled);
                    socket.selectExited.AddListener(OnOptimizedSocketEmptied);
                }
            }
        }
        else
        {
            foreach (var socket in bridgeSockets)
            {
                if (socket != null)
                {
                    socket.enabled = false;
                    baselineOccupiedSockets[socket] = null;
                }
            }

            GameObject[] foundPlanks = GameObject.FindGameObjectsWithTag("Plank");
            foreach (GameObject p in foundPlanks)
            {
                if (p.TryGetComponent<XRGrabInteractable>(out var interactable))
                {
                    baselinePlanks.Add(interactable);
                    interactable.selectExited.AddListener(OnBaselinePlankReleased);
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Cleanup listeners to prevent memory leaks/errors
        if (isOptimizedMode)
        {
            foreach (var socket in bridgeSockets)
            {
                if (socket != null)
                {
                    socket.selectEntered.RemoveListener(OnOptimizedSocketFilled);
                    socket.selectExited.RemoveListener(OnOptimizedSocketEmptied);
                }
            }
        }
        else
        {
            foreach (var plank in baselinePlanks)
            {
                if (plank != null) plank.selectExited.RemoveListener(OnBaselinePlankReleased);
            }
        }
    }

    // ─── OPTIMIZED LOGIC ───

    private void OnOptimizedSocketFilled(SelectEnterEventArgs args)
    {
        filledSlots++;
        FeedbackManager.Instance?.PlaySnapSuccess(args.interactorObject.transform.position);
        StartCoroutine(HeightClampRoutine(args.interactableObject.transform.gameObject, args.interactorObject.transform));
        CheckPuzzleCompletion();
    }

    private void OnOptimizedSocketEmptied(SelectExitEventArgs args) => filledSlots--;

    private IEnumerator HeightClampRoutine(GameObject plank, Transform socketTransform)
    {
        yield return new WaitForEndOfFrame();
        if (plank.transform.position.y < socketTransform.position.y)
        {
            Vector3 pos = plank.transform.position;
            pos.y = socketTransform.position.y;
            plank.transform.position = pos;
        }
    }

    // ─── BASELINE LOGIC ───

    private void OnBaselinePlankReleased(SelectExitEventArgs args)
    {
        GameObject plankObj = args.interactableObject.transform.gameObject;
        XRSocketInteractor closestSocket = null;
        float minDistance = float.MaxValue;

        foreach (var socket in bridgeSockets)
        {
            if (socket == null || baselineOccupiedSockets[socket] != null) continue;
            float dist = Vector3.Distance(plankObj.transform.position, socket.transform.position);
            if (dist < minDistance) { minDistance = dist; closestSocket = socket; }
        }

        if (closestSocket != null && minDistance <= baselineDropDistance)
        {
            baselineOccupiedSockets[closestSocket] = plankObj;
            filledSlots++;
            StartCoroutine(BaselineHardLockRoutine(plankObj, closestSocket.transform));
            CheckPuzzleCompletion();
        }
        else
        {
            Debug.LogWarning("[BridgePuzzle] Plank dropped outside valid snap threshold.");
            tutorialManager?.LogStepError();
        }
    }

    private IEnumerator BaselineHardLockRoutine(GameObject plank, Transform target)
    {
        Vector3 finalPos = plank.transform.position;
        if (finalPos.y < target.position.y) finalPos.y = target.position.y;
        Quaternion dropRot = plank.transform.rotation;

        if (plank.TryGetComponent<XRGrabInteractable>(out var interactable)) Destroy(interactable);
        yield return null;

        if (plank.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        plank.transform.position = finalPos;
        plank.transform.rotation = dropRot;
    }

    // ─── COMPLETION LOGIC ───

    private void CheckPuzzleCompletion()
    {
        if (filledSlots >= bridgeSockets.Count)
        {
            Debug.Log("[BridgePuzzle] All planks placed.");
            TelemetryManager.Instance?.LogEvent("puzzle_complete", new { puzzle = "bridge_alignment" });

            tutorialManager?.AdvanceTask();
            DisableBarriers();
        }
    }

    private void DisableBarriers()
    {
        foreach (var barrier in invisibleBarriers)
        {
            if (barrier != null) barrier.SetActive(false);
        }
    }
}