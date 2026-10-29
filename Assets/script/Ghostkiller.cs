using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GhostKiller : MonoBehaviour
{
    public GameObject jumpscareVideo;
    public GameObject mainMenuPanel;
    private bool isDead = false;

    void Update()
    {
        if (isDead) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && Vector3.Distance(transform.position, player.transform.position) < 1.5f)
        {
            // ≈сли ForceChase разрешил убийство (canKill), тогда убиваем
            GhostChase gc = GetComponent<GhostChase>();
            if (gc != null && gc.CanKillPlayer())
            {
                StartCoroutine(DieSequence(player));
            }
        }
    }

    IEnumerator DieSequence(GameObject player)
    {
        isDead = true;
        player.GetComponent<PlayerController>().enabled = false;

        if (jumpscareVideo != null)
        {
            jumpscareVideo.SetActive(true);
            yield return new WaitForSeconds(2f);
            jumpscareVideo.SetActive(false);
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}