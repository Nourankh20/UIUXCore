using UnityEngine;

public class WaypointTrigger : MonoBehaviour
{
    [Header("References")]
    public TutorialManager tutorialManager;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the zone is the Player
        if (other.CompareTag("Player"))
        {
            Debug.Log("[WaypointTrigger] Player reached the tomb entrance! Advancing step.");

            if (tutorialManager != null)
            {
                // FIX: Call the generic AdvanceTask() method instead of the removed specific method
                tutorialManager.AdvanceTask();

                // Deactivate this zone so it doesn't accidentally re-trigger later
                gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("[WaypointTrigger] TutorialManager reference is missing in the Inspector!");
            }
        }
    }
}