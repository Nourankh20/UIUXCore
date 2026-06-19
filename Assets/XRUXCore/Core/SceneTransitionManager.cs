using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    // Load the Low Cognitive Load room (Room Index 0)
    public void LoadLowCog()
    {
        Debug.Log("Transitioning to Low Cognitive Load Room...");
        SceneManager.LoadScene("LowCog");
    }

    // Load the High Cognitive Load room (Room Index 1)
    public void LoadHighCog()
    {
        Debug.Log("Transitioning to High Cognitive Load Room...");
        SceneManager.LoadScene("HighCog");
    }
}