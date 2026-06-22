using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[System.Serializable]
public class RoomTask
{
    public string taskName;
    [TextArea] public string instructionText;
}

public class TutorialManager : MonoBehaviour
{
    // ── SINGLETON INSTANCE ──
    public static TutorialManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Leave at -1 to auto-detect scene index.")]
    public int manualRoomIndex = -1;
    public float startDelay = 0.5f;
    public List<RoomTask> tasks = new List<RoomTask>();

    [Header("Scene Objects")]
    public TaskInstructionUI instructionUI;
    public GameObject standardTutorialCanvas;
    public GameObject optimizedHandMenuCanvas;
    public GameObject nasaTlxCanvas;

    public int currentTaskIndex => _currentTaskIndex;

    private int _roomIndex;
    private int _currentTaskIndex = 0;
    private float _stepStartTime;
    private int _stepErrors;
    private bool _isReady = false;
    private bool _isCompleted = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        _roomIndex = (manualRoomIndex != -1) ? manualRoomIndex : SceneManager.GetActiveScene().buildIndex;

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.TransitionToPhase($"room_0{_roomIndex}");
        }

        ConfigureUI();
        StartCoroutine(StartupRoutine());
    }

    private void ConfigureUI()
    {
        bool isOptimized = SessionManager.Instance?.activeProfile?.isOptimized ?? false;
        if (instructionUI != null) instructionUI.SetLayoutMode(isOptimized);
        if (standardTutorialCanvas) standardTutorialCanvas.SetActive(!isOptimized);
        if (optimizedHandMenuCanvas) optimizedHandMenuCanvas.SetActive(isOptimized);
    }

    private IEnumerator StartupRoutine()
    {
        yield return new WaitForSeconds(startDelay);
        _isReady = true;
        if (tasks.Count > 0) BeginTask(0);
    }

    public void CompleteTask(int completedIndex)
    {
        if (completedIndex == _currentTaskIndex) AdvanceTask();
    }

    public void AdvanceTask()
    {
        if (!_isReady || _isCompleted)
        {
            Debug.LogWarning($"[TutorialManager] AdvanceTask ignored. Ready: {_isReady}, Already Completed: {_isCompleted}");
            return;
        }

        CompleteTaskLogging();
        Debug.Log($"[TutorialManager] Successfully completed task index: {_currentTaskIndex} ('{tasks[_currentTaskIndex].taskName}').");

        _currentTaskIndex++;

        // Check if there are more tasks left in the Inspector list
        if (_currentTaskIndex < tasks.Count)
        {
            BeginTask(_currentTaskIndex);
        }
        else
        {
            Debug.Log($"[TutorialManager] All tasks completed! Final reached index: {_currentTaskIndex}. Inspector tasks count: {tasks.Count}. Launching survey...");
            _isCompleted = true;
            OnTutorialComplete();
        }
    }

    private void BeginTask(int index)
    {
        _currentTaskIndex = index;
        _stepStartTime = Time.time;
        _stepErrors = 0;

        bool isOptimized = SessionManager.Instance?.activeProfile?.isOptimized ?? false;

        if (instructionUI != null)
        {
            if (isOptimized)
            {
                instructionUI.ShowStep(tasks[index].instructionText);
            }
            else
            {
                instructionUI.ShowAllSteps(tasks, index);
            }
        }

        TelemetryManager.Instance?.LogEvent("tutorial_step_begin", new
        {
            room = _roomIndex,
            task = tasks[index].taskName
        });
    }

    private void CompleteTaskLogging()
    {
        TelemetryManager.Instance?.LogEvent("tutorial_step_complete", new
        {
            task = tasks[_currentTaskIndex].taskName,
            duration = Time.time - _stepStartTime,
            errorCount = _stepErrors
        });
    }

    private void OnTutorialComplete()
    {
        TelemetryManager.Instance?.LogEvent("task_complete", new
        {
            task = "room_" + _roomIndex,
            duration = SessionManager.Instance?.GetPhaseDuration()
        });

        if (nasaTlxCanvas != null)
        {
            Debug.Log("[TutorialManager] Activating NASA-TLX Canvas at its native design layout coordinates.");

            // Just activate the object—retaining its default scene coordinates, rotation, and scale intact.
            nasaTlxCanvas.SetActive(true);
        }
        else
        {
            Debug.LogError("[TutorialManager] CRITICAL: nasaTlxCanvas reference field is EMPTY in the Unity Inspector! Instantly skipping to fallback scene transfer.");
            TransitionToNextRoom();
        }
    }

    /// <summary>
    /// Call this from the 'Submit' button on your NASA-TLX canvas.
    /// </summary>
    public void TransitionToNextRoom()
    {
        Debug.Log($"[TutorialManager] Determining next scene transition for Room: {_roomIndex}");

        if (SceneTransitionManager.Instance != null)
        {
            if (_roomIndex == 1)
            {
                SceneTransitionManager.Instance.LoadHighCog();
            }
            else if (_roomIndex == 0)
            {
                SceneTransitionManager.Instance.LoadLowCog();
            }
            else
            {
                Debug.LogWarning($"[TutorialManager] No transition defined for index {_roomIndex}");
            }
        }
    }

    public void LogStepError() => _stepErrors++;
}