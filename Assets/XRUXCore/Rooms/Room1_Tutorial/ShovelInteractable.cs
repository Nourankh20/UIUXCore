using UnityEngine;

public class ShovelInteractable : MonoBehaviour
{
    public TutorialManager tutorialManager;
    public SnapInteractor snapInteractor; // sibling component
    private bool _grabbed;

    // Called by XRGrabInteractable UnityEvent → SelectEntered
    public void OnGrab()
    {
        if (_grabbed) return;
        _grabbed = true;

        snapInteractor.OnGrabbed();

        // FIX: Call the generic AdvanceTask instead of the removed specific method
        if (tutorialManager != null)
        {
            tutorialManager.AdvanceTask();
        }
    }

    // Called by XRGrabInteractable UnityEvent → SelectExited
    public void OnRelease() => snapInteractor.OnReleased();
}