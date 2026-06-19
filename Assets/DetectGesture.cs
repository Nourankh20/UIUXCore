using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

public class DetectGesture : MonoBehaviour
{
    [SerializeField] private XRHandTrackingEvents handTrackingEvents;
    [SerializeField] private XRHandShape[] handShapes;
    [SerializeField] private float minimumDetectionInterval = 0.9f;
    [SerializeField] private HandShapeCompletenessCalculator handShapeCompletenessCalculator;

    // Events to trigger your VR actions
    public UnityEvent OnGestureDetected;
    public UnityEvent OnGestureReleased;

    private bool isDetected = false;

    void OnEnable() => handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
    void OnDisable() => handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);

    void OnJointsUpdated(XRHandJointsUpdatedEventArgs eventArgs)
    {
        bool currentDetection = false;

        foreach (var handShape in handShapes)
        {
            handShapeCompletenessCalculator.TryCalculateHandShapeCompletenessScore(eventArgs.hand, handShape, out float completenessScore);

            if (handTrackingEvents.handIsTracked && completenessScore >= minimumDetectionInterval)
            {
                currentDetection = true;
                break;
            }
        }

        // State machine logic to trigger events only once per gesture
        if (currentDetection && !isDetected)
        {
            isDetected = true;
            OnGestureDetected?.Invoke();
            Debug.Log($"{handTrackingEvents.handedness} Gesture Performed!");
        }
        else if (!currentDetection && isDetected)
        {
            isDetected = false;
            OnGestureReleased?.Invoke();
            Debug.Log($"{handTrackingEvents.handedness} Gesture Released!");
        }
    }
}