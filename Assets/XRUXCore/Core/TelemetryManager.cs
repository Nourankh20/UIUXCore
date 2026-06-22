using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton. Routes discrete events to /api/events and batches 60 Hz
/// head-tracking frames to /api/highfreq/batch on the Vercel middleware.
/// </summary>
public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance;

    [Serializable]
    public class TrackedObjectInfo
    {
        public Transform target;
        public string customId;
    }

    // ── Inspector config ─────────────────────────────────────────────────────
    [Header("Middleware")]
    [Tooltip("Your Vercel deployment URL, e.g. https://xruxcore.vercel.app/")]
    [SerializeField] private string apiBaseUrl = "https://middleware-smoky.vercel.app/";

    [Tooltip("Must match the API_KEY environment variable set in Vercel. Leave empty for local dev.")]
    [SerializeField] private string apiKey = "";

    [Header("Manual Unity Inputs (Overrides Auto-Generation)")]
    [Tooltip("Type the Participant ID directly here or link it to a UI Input Field.")]
    public string unityParticipantId = "";

    [Tooltip("Type a custom Session ID here. If left empty, falls back to SessionManager.")]
    public string unitySessionId = "";

    [Header("High-Frequency Capture")]
    [SerializeField] private List<TrackedObjectInfo> trackedObjects = new List<TrackedObjectInfo>();
    [Tooltip("Head tracking capture rate in Hz. 60 is the target.")]
    [SerializeField] private float highFreqHz = 60f;

    [Tooltip("How often (seconds) to flush the high-freq buffer to the server.")]
    [SerializeField] private float flushInterval = 2f;

    // ── Private state ─────────────────────────────────────────────────────────
    private float _nextHighFreqTime;
    private float _nextFlushTime;
    private readonly List<HighFreqFrame> _highFreqBuffer = new();

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
        if (Time.time >= _nextHighFreqTime)
        {
            _nextHighFreqTime = Time.time + (1f / highFreqHz);
            CaptureHighFreqFrame();
        }

        if (Time.time >= _nextFlushTime)
        {
            _nextFlushTime = Time.time + flushInterval;
            FlushHighFreqBuffer();
        }
    }

    void OnApplicationQuit()
    {
        FlushHighFreqBuffer();
    }

    // ── Public UI Input Setters ──────────────────────────────────────────────

    /// <summary>
    /// Call this from a Unity UI / TMPro Input Field (On End Edit or On Value Changed)
    /// </summary>
    public void SetParticipantIdFromUI(string inputId)
    {
        unityParticipantId = inputId;
    }

    /// <summary>
    /// Call this from a Unity UI / TMPro Input Field to manually override session strings
    /// </summary>
    public void SetSessionIdFromUI(string inputId)
    {
        unitySessionId = inputId;
    }

    // ── ID Resolver Helpers ──────────────────────────────────────────────────

    private string GetActiveSessionId()
    {
        if (!string.IsNullOrEmpty(unitySessionId)) return unitySessionId;
        return SessionManager.Instance != null ? SessionManager.Instance.sessionId : "unknown_session";
    }

    private int GetActiveParticipantId()
    {
        if (!string.IsNullOrEmpty(unityParticipantId))
        {
            if (int.TryParse(unityParticipantId, out int parsedId))
                return parsedId;
        }
        return SessionManager.Instance != null ? SessionManager.Instance.participantId : -1;
    }

    // ── Dynamic Management APIs ────────────────────────────────────

    public void AddTrackedObject(Transform target, string customId = "")
    {
        if (target == null) return;

        int index = trackedObjects.FindIndex(x => x != null && x.target == target);
        if (index == -1)
        {
            trackedObjects.Add(new TrackedObjectInfo
            {
                target = target,
                customId = string.IsNullOrEmpty(customId) ? target.name : customId
            });
        }
    }

    public void RemoveTrackedObject(Transform target)
    {
        if (target == null) return;
        trackedObjects.RemoveAll(x => x == null || x.target == target);
    }

    public void ClearTrackedObjects()
    {
        trackedObjects.Clear();
    }

    // ── Enriched Public Log APIs ──────────────────────────────────────────────

    public void LogEvent(string eventType, object data)
    {
        string sid = GetActiveSessionId();
        string phase = SessionManager.Instance != null ? SessionManager.Instance.currentPhase : "unknown_phase";

        var payload = new EventPayload
        {
            session_id = sid,
            timestamp = Time.time,
            phase = phase,
            event_type = eventType,
            data = BuildEnrichedContextJson(data)
        };

        StartCoroutine(Post("api/events", JsonUtility.ToJson(payload)));
    }

    public void LogNasaTlxToEndpoint(string phaseEvaluated, float mental, float physical, float temporal, float performance, float effort, float frustration)
    {
        var payload = new NasaTlxPayload
        {
            session_id = GetActiveSessionId(),
            participant_id = GetActiveParticipantId(),
            timestamp = Time.time,
            phase_evaluated = phaseEvaluated,
            is_optimized = SessionManager.Instance != null && SessionManager.Instance.IsOptimized,
            profile_name = (SessionManager.Instance != null && SessionManager.Instance.activeProfile != null) ? SessionManager.Instance.activeProfile.profileName : "None",
            mental_demand = mental,
            physical_demand = physical,
            temporal_demand = temporal,
            performance = performance,
            effort = effort,
            frustration = frustration
        };

        StartCoroutine(Post("api/surveys", JsonUtility.ToJson(payload)));
    }

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
        string sid = GetActiveSessionId();

        // 1. Log Head Tracking
        if (Camera.main != null)
        {
            AddFrameToBuffer(Camera.main.transform, "head", sid);
        }

        // 2. Log Interactable Movements
        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            var tracked = trackedObjects[i];
            if (tracked != null && tracked.target != null)
            {
                string finalId = string.IsNullOrEmpty(tracked.customId) ? tracked.target.name : tracked.customId;
                AddFrameToBuffer(tracked.target, finalId, sid);
            }
            else
            {
                trackedObjects.RemoveAt(i);
            }
        }
    }

    private void AddFrameToBuffer(Transform t, string id, string sid)
    {
        Vector3 pos = t.position;
        Vector3 rot = t.rotation.eulerAngles;

        _highFreqBuffer.Add(new HighFreqFrame
        {
            session_id = sid,
            timestamp = Time.time,
            event_type = id == "head" ? "head_tracking" : "object_tracking",
            object_id = id,
            pos_x = SafeFloat(pos.x),
            pos_y = SafeFloat(pos.y),
            pos_z = SafeFloat(pos.z),
            pitch = SafeFloat(rot.x),
            yaw = SafeFloat(rot.y),
            roll = SafeFloat(rot.z)
        });
    }

    private float SafeFloat(float value)
    {
        return (float.IsNaN(value) || float.IsInfinity(value)) ? 0f : value;
    }

    private void FlushHighFreqBuffer()
    {
        if (_highFreqBuffer.Count == 0) return;

        var frames = new List<HighFreqFrame>(_highFreqBuffer);
        _highFreqBuffer.Clear();

        StartCoroutine(PostBatch("api/highfreq/batch", frames));
    }

    // ── Metadata Merging Reflection Engine ────────────────────────────────────
    private string BuildEnrichedContextJson(object customFields)
    {
        int pid = GetActiveParticipantId();
        bool isOpt = SessionManager.Instance != null && SessionManager.Instance.IsOptimized;
        string profName = (SessionManager.Instance != null && SessionManager.Instance.activeProfile != null)
            ? SessionManager.Instance.activeProfile.profileName : "None";
        string room = SessionManager.Instance != null ? SessionManager.Instance.currentPhase : "unknown_room";

        List<string> jsonEntries = new List<string>
        {
            $"\"participant_id\":{pid}",
            $"\"is_optimized\":{(isOpt ? "true" : "false")}",
            $"\"profile_name\":\"{profName.Replace("\"", "\\\"")}\"",
            $"\"room_phase\":\"{room.Replace("\"", "\\\"")}\""
        };

        if (customFields != null)
        {
            Type objType = customFields.GetType();

            PropertyInfo[] props = objType.GetProperties();
            foreach (PropertyInfo prop in props)
            {
                try
                {
                    object value = prop.GetValue(customFields, null);
                    jsonEntries.Add($"\"{prop.Name}\":{FormatValue(value)}");
                }
                catch { }
            }

            FieldInfo[] fields = objType.GetFields();
            foreach (FieldInfo field in fields)
            {
                try
                {
                    object value = field.GetValue(customFields);
                    jsonEntries.Add($"\"{field.Name}\":{FormatValue(value)}");
                }
                catch { }
            }
        }

        return "{" + string.Join(",", jsonEntries) + "}";
    }

    private string FormatValue(object val)
    {
        if (val == null) return "null";
        if (val is string || val is char) return $"\"{val.ToString().Replace("\"", "\\\"")}\"";
        if (val is bool b) return b ? "true" : "false";
        if (val is int || val is float || val is double || val is long || val is decimal)
            return SafeFloat(Convert.ToSingle(val)).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return $"\"{val.ToString().Replace("\"", "\\\"")}\"";
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
            Debug.LogError($"[TelemetryManager] Cannot reach middleware at {url}. Error: {req.error}");
    }

    [Serializable]
    private class EventPayload
    {
        public string session_id;
        public float timestamp;
        public string phase;
        public string event_type;
        public string data;
    }

    [Serializable]
    private struct HighFreqFrame
    {
        public string session_id;
        public float timestamp;
        public string event_type;
        public string object_id;
        public float pos_x, pos_y, pos_z;
        public float pitch, yaw, roll;
    }

    [Serializable]
    private class NasaTlxPayload
    {
        public string session_id;
        public int participant_id;
        public float timestamp;
        public string phase_evaluated;
        public bool is_optimized;
        public string profile_name;
        public float mental_demand;
        public float physical_demand;
        public float temporal_demand;
        public float performance;
        public float effort;
        public float frustration;
    }
}