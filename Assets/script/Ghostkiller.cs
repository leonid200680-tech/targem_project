using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GhostKiller : MonoBehaviour
{
    [Header("Настройки")]
    public float killDistance = 2.0f;
    public GameObject deathScreen;   // используется, только если в сцене нет системы рассудка
    public AudioClip eatSound;

    private bool isDead = false;
    private Transform player;
    private Animator anim;
    private PlayerController pc;
    private Camera playerCam;
    private Vector3 camRestPos;
    private Quaternion camRestRot;

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
        if (player == null || isDead || HideableCloset.IsPlayerHidden) return; // в шкафу не достанет

        float dist = Vector3.Distance(transform.position, player.position);

        // Если бабка подошла близко — хватает игрока
        if (dist <= killDistance)
        {
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
            camRestPos = playerCam.transform.localPosition;
            camRestRot = playerCam.transform.localRotation;
            Vector3 endCamPos = new Vector3(camRestPos.x, 0.2f, camRestPos.z); // Опускаем вниз к полу
            Quaternion endCamRot = Quaternion.Euler(-60f, camRestRot.eulerAngles.y, camRestRot.eulerAngles.z); // Наклоняем

            float elapsed = 0;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fallDuration;

                playerCam.transform.localPosition = Vector3.Lerp(camRestPos, endCamPos, t);
                playerCam.transform.localRotation = Quaternion.Slerp(camRestRot, endCamRot, t);
                yield return null;
            }
        }

        // 6. Ждем, пока бабка ест
        yield return new WaitForSeconds(2f);

        // 7. Игрок не умирает, а теряет рассудок и приходит в себя (см. Sanity)
        if (Sanity.Instance != null)
        {
            Sanity.Instance.OnCaught(this);
            yield break;
        }

        // Запасной вариант без системы рассудка: экран смерти
        yield return new WaitForSeconds(1f);
        if (deathScreen != null) deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Игрок пришёл в себя: возвращаем камеру и управление, бабка снова ходит,
    // но несколько секунд не может схватить
    public void Revive(float grace)
    {
        if (playerCam != null)
        {
            playerCam.transform.localPosition = camRestPos;
            playerCam.transform.localRotation = camRestRot;
        }
        if (pc != null) pc.enabled = true;

        if (anim != null)
        {
            anim.ResetTrigger("EatPlayer");
            anim.Rebind();
            anim.Update(0f);
        }
        GhostChase chase = GetComponent<GhostChase>();
        if (chase != null) chase.enabled = true;
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.isStopped = false;

        StartCoroutine(GraceRoutine(grace));
    }

    IEnumerator GraceRoutine(float grace)
    {
        yield return new WaitForSeconds(grace);
        isDead = false;
    }
}
