using UnityEngine;

public class IllusionTrigger : MonoBehaviour
{
    public GhostIllusion illusionGhost; // Перетащи сюда копию бабки (GhostIllusion)
    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !triggered)
        {
            triggered = true;

            if (illusionGhost != null)
            {
                illusionGhost.Vanish(); // Запускаем исчезновение
            }
        }
    }
}