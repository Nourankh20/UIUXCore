using UnityEngine;
using UnityEngine.UI;
using System;

public class NasaTlxUiManager : MonoBehaviour
{
    [Header("UI Canvas Groups")]
    public GameObject surveyPanel;

    [Header("NASA-TLX Sliders (1 to 7)")]
    public Slider mentalSlider;
    public Slider physicalSlider;
    public Slider temporalSlider;
    public Slider performanceSlider;
    public Slider effortSlider;
    public Slider frustrationSlider;

    [Header("Submit Button")]
    public Button submitButton;

    private string _evaluatedPhase;

    // A concrete serializable class so JsonUtility doesn't output empty brackets "{}"
    [Serializable]
    public class NasaTlxData
    {
        public string phaseEvaluated;
        public float mentalDemand;
        public float physicalDemand;
        public float temporalDemand;
        public float performance;
        public float effort;
        public float frustration;
    }

    void Start()
    {
        submitButton.onClick.AddListener(OnSubmitButtonPressed);
        _evaluatedPhase = SessionManager.Instance != null ? SessionManager.Instance.currentPhase : "Unknown_Phase";
    }

    private void OnSubmitButtonPressed()
    {
        // Populate our serializable data container
        NasaTlxData surveyResults = new NasaTlxData
        {
            phaseEvaluated = _evaluatedPhase,
            mentalDemand = mentalSlider != null ? mentalSlider.value : -1f,
            physicalDemand = physicalSlider != null ? physicalSlider.value : -1f,
            temporalDemand = temporalSlider != null ? temporalSlider.value : -1f,
            performance = performanceSlider != null ? performanceSlider.value : -1f,
            effort = effortSlider != null ? effortSlider.value : -1f,
            frustration = frustrationSlider != null ? frustrationSlider.value : -1f
        };

        if (TelemetryManager.Instance != null)
        {
            // Route through your existing, working events channel
            TelemetryManager.Instance.LogEvent("nasa_tlx_survey", surveyResults);
            Debug.Log("[NASA-TLX] Survey data pushed to existing events collection.");
        }

        ProceedToNextScene();
    }

    private void ProceedToNextScene()
    {
        submitButton.interactable = false;

        // Use the TutorialManager to handle the transition logic
        // This automatically picks the correct scene (LowCog vs HighCog) 
        // based on the room index.
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.TransitionToNextRoom();
        }
        else
        {
            Debug.LogError("[NasaTlxUiManager] TutorialManager instance not found! Cannot transition.");
        }
    }
}