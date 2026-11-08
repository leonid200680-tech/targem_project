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

        // 4. Скример: бабка бросается в лицо с криком
        if (playerCam != null)
        {
            camRestPos = playerCam.transform.localPosition;
            camRestRot = playerCam.transform.localRotation;
        }
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        AudioClip scream = hm != null && hm.screamSound != null ? hm.screamSound : eatSound;
        const float scareTime = 1.6f;
        GrannyJumpscare.Play(gameObject, scream, scareTime);
        yield return new WaitForSeconds(scareTime);

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
