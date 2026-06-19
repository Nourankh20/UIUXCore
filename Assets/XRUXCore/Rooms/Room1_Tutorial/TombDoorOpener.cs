using UnityEngine;

public class TombDoorOpener : MonoBehaviour
{
    [Header("Animation & Audio")]
    public Animator doorAnimator;    // The Animator with the "Open" trigger
    public AudioClip doorCreakSFX;

    [Header("References")]
    public TutorialManager tutorialManager; // Reference to your TutorialManager

    private bool _opened;

    /// <summary>
    /// Renamed to OpenDoor to match TutorialManager's expected call.
    /// </summary>
    public void OpenDoor()
    {
        if (_opened || doorAnimator == null) return;
        _opened = true;

        // 1. Trigger the Visual Animation
        doorAnimator.SetTrigger("Open");

        // 2. Provide Multi-modal Feedback based on the active XR profile
        // This uses your existing SessionManager profile settings
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.Trigger(
                haptic: SessionManager.Instance.activeProfile.useHaptics,
                audio: doorCreakSFX,
                glow: null,
                intensity: 0.8f,
                success: true
            );
        }

        // 3. Log the discrete event to your Vercel middleware
        TelemetryManager.Instance.LogEvent("door_opened", new
        {
            door = "tutorial_entrance",
            timestamp = Time.time
        });

        // 4. Notify the TutorialManager after a delay to allow the animation to play
        Invoke(nameof(NotifyManager), 1.5f);
    }

    private void NotifyManager()
    {
        if (tutorialManager != null)
        {
            // FIX: Call the generic AdvanceTask() method instead of the removed specific method
            tutorialManager.AdvanceTask();
        }
    }
}