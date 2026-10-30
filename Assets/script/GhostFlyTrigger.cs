using UnityEngine;

public class GhostFlyTrigger : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject screamerGhost;    // Копия летающей бабки
    public Transform startPoint;        // Откуда вылетит
    public Transform endPoint;          // Куда улетит
    public AudioClip screamSound;       // Звук

    private bool playerInside = false;  // Запоминаем, что игрок внутри
    private bool triggered = false;     // Защита от повторного срабатывания

    // Когда игрок ЗАХОДИТ в зону
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            Debug.Log("Игрок зашел в зону. Ждем выхода...");
        }
    }

    // Когда игрок ВЫХОДИТ из зоны
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && playerInside && !triggered)
        {
            playerInside = false; // Игрок вышел
            triggered = true;     // Блокируем повторный запуск

            Debug.Log("Игрок вышел! Запускаем скример!");
            TriggerScreamer();
        }
    }

    private void TriggerScreamer()
    {
        if (screamSound != null)
            AudioSource.PlayClipAtPoint(screamSound, transform.position, 1f);

        if (screamerGhost != null && startPoint != null && endPoint != null)
        {
            screamerGhost.SetActive(true);

            FlyingGhost flyScript = screamerGhost.GetComponent<FlyingGhost>();
            if (flyScript != null)
            {
                flyScript.StartFlight(startPoint.position, endPoint.position);
            }
        }
        else
        {
            Debug.LogError("Не привязаны объекты в инспекторе!");
        }
    }
}