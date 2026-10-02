using UnityEngine;

public class EnemyTrigger : MonoBehaviour
{
    [HideInInspector] public bool playerDetected = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[Trigger] Вошёл объект: " + other.name + " | тег: " + other.tag);
        if (other.CompareTag("Player"))
        {
            playerDetected = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerDetected = false;
        }
    }
}