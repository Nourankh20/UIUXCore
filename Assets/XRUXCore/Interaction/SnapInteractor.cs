using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SnapInteractor : XRGrabInteractable
{
    // ── Inspector ────────────────────────────────────
    public Transform snapTarget;       // empty GameObject at ideal placement
    public string objectId;         // e.g. "canopic_jar_01"
    public AudioClip snapSFX;
    public GameObject glowEffect;

    // ── Runtime state ────────────────────────────────
    private float _grabTimestamp;
    private int _grabAttempts;
    private float _distanceError;
    private float _angularError;
    private bool _isSnapped;

    // ── Profile reference (from SessionManager) ──────
    private UXProfileSO _profile =>
        SessionManager.Instance.activeProfile;

    // ── XRI callbacks ───────────────────────────────
    public void OnGrabbed()
    {
        _grabTimestamp = Time.time;
        _grabAttempts++;
        TelemetryManager.Instance.LogEvent("grab", new { objectId });
    }

    public void OnReleased()
    {
        float holdDuration = Time.time - _grabTimestamp;
        CalculatePlacementErrors();

        bool snapSuccess = _profile.snappingEnabled
            && _distanceError <= _profile.snapDistanceThreshold
            && _angularError <= _profile.snapAngleThreshold;

        if (snapSuccess) ExecuteSnap();

        TelemetryManager.Instance.LogSnapEvent(
            snapSuccess, _distanceError, _angularError, objectId);

        TelemetryManager.Instance.LogEvent("release", new
        {
            objectId,
            holdDuration,
            snapSuccess,
            distanceError = _distanceError,
            angularError = _angularError,
            attemptNumber = _grabAttempts
        });

        TriggerFeedback(snapSuccess);
    }

    // ── Helpers ──────────────────────────────────────
    private void CalculatePlacementErrors()
    {
        if (snapTarget == null) return;
        _distanceError = Vector3.Distance(transform.position, snapTarget.position);
        _angularError = Quaternion.Angle(transform.rotation, snapTarget.rotation);
    }

    private void ExecuteSnap()
    {
        transform.position = snapTarget.position;
        transform.rotation = snapTarget.rotation;
        _isSnapped = true;
    }

    private void TriggerFeedback(bool success)
    {
        FeedbackManager.Instance.Trigger(
            haptic: _profile.useHaptics,
            audio: _profile.useAudioFeedback ? snapSFX : null,
            glow: _profile.useVisualGlow ? glowEffect : null,
            intensity: _profile.feedbackIntensity,
            success: success
        );
    }
}
