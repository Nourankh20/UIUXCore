using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;

    // ── Fields ──────────────────────────────────────
    [HideInInspector] public string sessionId;
    [HideInInspector] public int participantId;
    [HideInInspector] public string currentPhase;  // tutorial|sorting|procedural
    [HideInInspector] public float sessionStartTime;
    [HideInInspector] public float phaseStartTime;

    public UXProfileSO activeProfile; // set before Play in Inspector

    // ── Public Helper Property ───────────────────────
    public bool IsOptimized => activeProfile != null && activeProfile.isOptimized;

    // ── Unity lifecycle ──────────────────────────────
    void Awake()
    {
        // 1. Check if a true master instance from Room 1 already exists
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[SessionManager] True Master found ('{Instance.activeProfile.profileName}'). Destroying Room 2 local duplicate.");
            Destroy(gameObject);
            return;
        }

        // 2. If this is the first scene, make this the true Master
        Instance = this;

        // ── THE CRITICAL FIX ──
        // If this script is nested inside a parent GameObject, DontDestroyOnLoad will silently FAIL.
        // Forcing the parent to null detaches it so Unity can safely move it to the DontDestroyOnLoad group.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        sessionId = System.Guid.NewGuid().ToString();
        sessionStartTime = Time.time;
    }

    void Start() => LogSessionStart();

    // ── Public API ───────────────────────────────────
    public void TransitionToPhase(string phase)
    {
        currentPhase = phase;
        phaseStartTime = Time.time;
        TelemetryManager.Instance?.LogEvent("phase_transition",
            new { phase, timestamp = Time.time });
    }

    public float GetPhaseDuration() => Time.time - phaseStartTime;
    public float GetSessionDuration() => Time.time - sessionStartTime;

    private void LogSessionStart()
    {
        TelemetryManager.Instance?.LogEvent("session_start", new
        {
            participantId,
            profile = activeProfile != null ? activeProfile.profileName : "None",
            isOptimized = IsOptimized
        });
    }
}