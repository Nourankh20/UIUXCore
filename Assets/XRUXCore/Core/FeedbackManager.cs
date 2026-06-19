using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // Required for XRController

public class FeedbackManager : MonoBehaviour
{
    public static FeedbackManager Instance;
    public AudioSource audioSource;

    [Header("Modern XR Controllers")]
    public XRController leftController;
    public XRController rightController;

    [Header("Bridge Puzzle Sounds")]
    [Tooltip("Sound to play when a plank snaps into a correct slot.")]
    public AudioClip snapSuccessClip;
    [Tooltip("Sound to play when a plank is dropped too far from a slot.")]
    public AudioClip snapFailClip;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Automatically catch the AudioSource if you forgot to drag it in
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Force-disable Play On Awake via code just to be safe
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }
    }

    public void Trigger(bool haptic, AudioClip audio,
                        GameObject glow, float intensity, bool success)
    {
        // 1. Audio Feedback (Fires first so controller issues can't block it)
        if (audio != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(audio, intensity);
                Debug.Log($"[FeedbackManager] Successfully playing audio clip: {audio.name}");
            }
            else
            {
                Debug.LogError("[FeedbackManager] Audio clip provided, but AudioSource component is missing!");
            }
        }

        // 2. Haptic Feedback via XRController
        if (haptic)
        {
            float amp = success ? intensity : intensity * 0.4f;
            float duration = 0.1f;

            if (leftController != null) leftController.SendHapticImpulse(amp, duration);
            if (rightController != null) rightController.SendHapticImpulse(amp, duration);
        }

        // 3. Visual Feedback (Glow)
        if (glow != null)
        {
            StartCoroutine(FlashGlow(glow, intensity));
        }
    }

    private IEnumerator FlashGlow(GameObject g, float intensity)
    {
        g.SetActive(true);
        yield return new WaitForSeconds(0.3f * intensity);
        g.SetActive(false);
    }

    // ── BRIDGE PUZZLE SPECIFIC METHODS ──

    /// <summary>
    /// Called by the BridgePuzzleManager when a plank successfully locks into a slot.
    /// </summary>
    /// <param name="position">The 3D position where the snap occurred (for future 3D audio scaling if needed).</param>
    public void PlaySnapSuccess(Vector3 position)
    {
        // Fires haptics, plays the success clip, no visual glow override, 100% intensity, flag as success
        Trigger(true, snapSuccessClip, null, 1.0f, true);
    }

    /// <summary>
    /// Called by the BridgePuzzleManager when a plank is dropped in an invalid area.
    /// </summary>
    public void PlaySnapFail()
    {
        // Fires haptics, plays the fail clip, no visual glow override, 80% intensity, flag as failure
        Trigger(true, snapFailClip, null, 0.8f, false);
    }
}