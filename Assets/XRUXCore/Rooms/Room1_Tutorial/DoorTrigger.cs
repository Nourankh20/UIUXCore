using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public TombDoorOpener doorScript;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Strictly check for the Broom/Shovel tag. 
        // The player's body walking into this zone will now do absolutely nothing!
        if (other.CompareTag("Shovel"))
        {
            Debug.Log("[TRIGGER] Broom touched the door. Opening...");

            if (doorScript != null)
            {
                doorScript.OpenDoor();
                gameObject.SetActive(false); // Turn off the trigger once successful
            }
        }
    }
}