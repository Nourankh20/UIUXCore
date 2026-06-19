using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton. Routes discrete events to /api/events and batches 60 Hz
/// head-tracking frames to /api/highfreq/batch on the Vercel middleware.
///
/// Drop this on the [XRUXCore] persistent GameObject.
/// Fill in apiBaseUrl and apiKey in the Inspector before each build.
/// </summary>
public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance;

    // ── Inspector config ─────────────────────────────────────────────────────
    [Header("Middleware")]
    [Tooltip("Your Vercel deployment URL, e.g. https://xruxcore.vercel.app/")]
    [SerializeField] private string apiBaseUrl = "https://middleware-smoky.vercel.app/";

    [Tooltip("Must match the API_KEY environment variable set in Vercel. " +
             "Leave empty to skip auth (local dev only).")]
    [SerializeField] private string apiKey = "";

    [Header("High-Frequency Capture")]
    [SerializeField] private List<Transform> trackedObjects = new List<Transform>();
    [Tooltip("Head tracking capture rate in Hz. 60 is the target.")]
    [SerializeField] private float highFreqHz = 60f;

    [Tooltip("How often (seconds) to flush the high-freq buffer to the server. " +
             "2 seconds = ~120 frames per batch.")]
    [SerializeField] private float flushInterval = 2f;

    // ── Private state ─────────────────────────────────────────────────────────
    private float _nextHighFreqTime;
    private float _nextFlushTime;
    private readonly List<HighFreqFrame> _highFreqBuffer = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _nextFlushTime = Time.time + flushInterval;
        StartCoroutine(PingHealth());
    }

    void Update()
    {
        // Capture high-freq frames locally
        if (Time.time >= _nextHighFreqTime)
        {
            _nextHighFreqTime = Time.time + (1f / highFreqHz);
            CaptureHighFreqFrame();
        }

        // Flush buffer to server on interval
        if (Time.time >= _nextFlushTime)
        {
            _nextFlushTime = Time.time + flushInterval;
            _nextFlushTime = Time.time + flushInterval;
            FlushHighFreqBuffer();
        }
    }

    void OnApplicationQuit()
    {
        // Best-effort flush on quit so we don't lose the last few seconds
        FlushHighFreqBuffer();
    }

    // ── Public API — called by all room managers ──────────────────────────────

    /// <summary>
    /// Log any named discrete event. Fires immediately (not batched).
    /// </summary>
    public void LogEvent(string eventType, object data)
    {
        // Guard check against missing SessionManager
        string sid = SessionManager.Instance != null ? SessionManager.Instance.sessionId : "unknown_session";
        string phase = SessionManager.Instance != null ? SessionManager.Instance.currentPhase : "unknown_phase";

        var payload = new EventPayload
        {
            session_id = sid,
            timestamp = Time.time,
            phase = phase,
            event_type = eventType,
            data = JsonUtility.ToJson(data)   // nested as JSON string
        };
        StartCoroutine(Post("api/events", JsonUtility.ToJson(payload)));
    }

    /// <summary>
    /// Sends the NASA-TLX survey structure directly to its own dedicated collection.
    /// </summary>
    public void LogNasaTlxToEndpoint(string phaseEvaluated, float mental, float physical, float temporal, float performance, float effort, float frustration)
    {
        var payload = new NasaTlxPayload
        {
            session_id = SessionManager.Instance != null ? SessionManager.Instance.sessionId : "unknown_session",
            participant_id = SessionManager.Instance != null ? SessionManager.Instance.participantId : -1,
            timestamp = Time.time,
            phase_evaluated = phaseEvaluated,
            mental_demand = mental,
            physical_demand = physical,
            temporal_demand = temporal,
            performance = performance,
            effort = effort,
            frustration = frustration
        };

        // We change the path here. Based on how your middleware handles 'api/events' -> 'Events' 
        // and 'api/highfreq/batch' -> 'HighFreq_Logs', hitting 'api/surveys' or 'api/survey'
        // tells MongoDB to dynamically spin up a brand new collection folder on your dashboard.
        StartCoroutine(Post("api/surveys", JsonUtility.ToJson(payload)));
    }

    /// <summary>
    /// Convenience: log snap success/fail with precision metrics.
    /// Called by SnapInteractor.OnReleased().
    /// </summary>
    public void LogSnapEvent(bool success, float distErr, float angErr, string objectId)
    {
        LogEvent(success ? "snap_success" : "snap_fail", new
        {
            objectId,
            distanceError = distErr,
            angularError = angErr
        });
    }

    // ── High-frequency capture ────────────────────────────────────────────────
    private void CaptureHighFreqFrame()
    {
        // Guard check if SessionManager is missing to avoid errors during rapid frame updates
        if (SessionManager.Instance == null) return;

        // 1. Log Head Tracking
        if (Camera.main != null)
        {
            AddFrameToBuffer(Camera.main.transform, "head");
        }

        // 2. Log Interactable Movements
        foreach (var obj in trackedObjects)
        {
            if (obj != null)
            {
                AddFrameToBuffer(obj, obj.name);
            }
        }
    }

    // Helper to keep the code clean
    private void AddFrameToBuffer(Transform t, string id)
    {
        _highFreqBuffer.Add(new HighFreqFrame
        {
            session_id = SessionManager.Instance.sessionId,
            timestamp = Time.time,
            event_type = id == "head" ? "head_tracking" : "object_tracking",
            object_id = id,
            pos_x = t.position.x,
            pos_y = t.position.y,
            pos_z = t.position.z,
            pitch = t.rotation.eulerAngles.x,
            yaw = t.rotation.eulerAngles.y,
            roll = t.rotation.eulerAngles.z
        });
    }

    private void FlushHighFreqBuffer()
    {
        if (_highFreqBuffer.Count == 0) return;

        // Snapshot and clear before the coroutine runs to avoid double-sending
        var frames = new List<HighFreqFrame>(_highFreqBuffer);
        _highFreqBuffer.Clear();

        StartCoroutine(PostBatch("api/highfreq/batch", frames));
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private IEnumerator Post(string path, string json)
    {
        string url = apiBaseUrl.TrimEnd('/') + "/" + path;
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(apiKey))
            req.SetRequestHeader("x-api-key", apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[TelemetryManager] POST {path} failed: {req.error}");
    }

    private IEnumerator PostBatch(string path, List<HighFreqFrame> frames)
    {
        // Manually build the JSON array — JsonUtility doesn't serialize List<T> at root
        var sb = new StringBuilder("[");
        for (int i = 0; i < frames.Count; i++)
        {
            sb.Append(JsonUtility.ToJson(frames[i]));
            if (i < frames.Count - 1) sb.Append(",");
        }
        sb.Append("]");

        yield return Post(path, sb.ToString());
    }

    private IEnumerator PingHealth()
    {
        string url = apiBaseUrl.TrimEnd('/') + "/api/health";
        using var req = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(apiKey))
            req.SetRequestHeader("x-api-key", apiKey);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[TelemetryManager] Middleware reachable. Response: {req.downloadHandler.text}");
        else
            Debug.LogError($"[TelemetryManager] Cannot reach middleware at {url}. " +
                            $"Check apiBaseUrl and network. Error: {req.error}");
    }

    // ── Serializable types (JsonUtility requires [Serializable]) ──────────────

    [Serializable]
    private class EventPayload
    {
        public string session_id;
        public float timestamp;
        public string phase;
        public string event_type;
        public string data;       // JSON-encoded string of the data object
    }

    [Serializable]
    private class HighFreqFrame
    {
        public string session_id;
        public float timestamp;
        public string event_type; // "head_tracking" or "object_tracking"
        public string object_id;  // Added this: "head", "shovel_01", etc.
        public float pos_x, pos_y, pos_z;
        public float pitch, yaw, roll;
    }

    /// <summary>
    /// Structured representation for NASA-TLX endpoints or clean serialization logs
    /// </summary>
    [Serializable]
    private class NasaTlxPayload
    {
        public string session_id;
        public int participant_id;
        public float timestamp;
        public string phase_evaluated;
        public float mental_demand;
        public float physical_demand;
        public float temporal_demand;
        public float performance;
        public float effort;
        public float frustration;
    }
}