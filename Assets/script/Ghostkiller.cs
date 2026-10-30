using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GhostKiller : MonoBehaviour
{
    [Header("Настройки")]
    public float killDistance = 2.0f;
    public GameObject deathScreen;
    public AudioClip eatSound;

    private bool isDead = false;
    private Transform player;
    private Animator anim;
    private PlayerController pc;
    private Camera playerCam;

    void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            pc = p.GetComponent<PlayerController>();
            playerCam = p.GetComponentInChildren<Camera>();
        }
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (player == null || isDead) return;

        float dist = Vector3.Distance(transform.position, player.position);

        // Если бабка подошла близко — сразу убиваем (без проверок canKill)
        if (dist <= killDistance)
        {
            Debug.Log("Бабка близко! Начинаем поедание игрока.");
            StartCoroutine(EatSequence());
        }
    }

    IEnumerator EatSequence()
    {
        isDead = true;

        // 1. Отключаем управление игроком
        if (pc != null) pc.enabled = false;

        // 2. Останавливаем бабку
        GhostChase chase = GetComponent<GhostChase>();
        if (chase != null) chase.enabled = false;
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = true;

        // 3. Разворачиваем бабку к игроку
        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

        // 4. Включаем анимацию поедания (Триггер EatPlayer)
        if (anim != null) anim.SetTrigger("EatPlayer");

        if (eatSound != null)
            AudioSource.PlayClipAtPoint(eatSound, transform.position);

        // 5. Опускаем камеру игрока (эффект падения)
        if (playerCam != null)
        {
            float fallDuration = 1.0f;
            Vector3 startCamPos = playerCam.transform.localPosition;
            Vector3 endCamPos = new Vector3(startCamPos.x, 0.2f, startCamPos.z); // Опускаем вниз к полу
            Quaternion startCamRot = playerCam.transform.localRotation;
            Quaternion endCamRot = Quaternion.Euler(-60f, startCamRot.eulerAngles.y, startCamRot.eulerAngles.z); // Наклоняем

            float elapsed = 0;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fallDuration;

                playerCam.transform.localPosition = Vector3.Lerp(startCamPos, endCamPos, t);
                playerCam.transform.localRotation = Quaternion.Slerp(startCamRot, endCamRot, t);
                yield return null;
            }
        }

        // 6. Ждем, пока бабка ест
        yield return new WaitForSeconds(3f);

        // 7. Включаем экран смерти
        if (deathScreen != null) deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}