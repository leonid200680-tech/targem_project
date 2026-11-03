using UnityEngine;

// Анимирует модель игрока (ходьба/бег/прыжок) по скорости CharacterController.
// Висит на модели (у неё Animator), CharacterController ищется у родителя.
[RequireComponent(typeof(Animator))]
public class PlayerBodyAnimator : MonoBehaviour
{
    [Header("Ссылки")]
    public CharacterController controller;
    public PlayerController playerController;

    [Header("Скорости")]
    public float walkBlend = 2f;   // порог Walk в blend tree
    public float runBlend = 6f;    // порог Run в blend tree
    public float blendDamp = 0.1f;

    private Animator animator;
    private Vector3 lastPos;
    private float airTime;
    private readonly System.Collections.Generic.List<Renderer> upperBody = new System.Collections.Generic.List<Renderer>();
    private bool wasFirstPerson = true;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int JumpId = Animator.StringToHash("Jump");
    static readonly int FreeFallId = Animator.StringToHash("FreeFall");

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        lastPos = controller.transform.position;

        // Голова, кепка, рубашка, рюкзак от первого лица рисуют только тень, иначе лезут в камеру
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) upperBody.Add(r);
    }

    // В катсцене (камера игрока выключена) охранника видно целиком
    void LateUpdate()
    {
        var cam = playerController != null ? playerController.cameraTransform : null;
        bool firstPerson = cam != null && cam.gameObject.activeInHierarchy;
        if (firstPerson == wasFirstPerson) return;
        wasFirstPerson = firstPerson;
        var mode = firstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        foreach (var r in upperBody) if (r != null) r.shadowCastingMode = mode;
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;

        Vector3 pos = controller.transform.position;
        Vector3 delta = (pos - lastPos) / Time.deltaTime;
        lastPos = pos;

        float horizontal = new Vector3(delta.x, 0f, delta.z).magnitude;
        float walk = playerController != null ? playerController.walkSpeed : 4f;
        float run = playerController != null ? playerController.runSpeed : 7f;

        // Переводим реальную скорость в пороги blend tree (0 / 2 / 6)
        float blend = horizontal <= walk
            ? horizontal / walk * walkBlend
            : walkBlend + Mathf.Clamp01((horizontal - walk) / (run - walk)) * (runBlend - walkBlend);
        if (horizontal < 0.1f) blend = 0f;

        animator.SetFloat(SpeedId, blend, blendDamp, Time.deltaTime);
        animator.SetFloat(MotionSpeedId, horizontal < 0.1f ? 1f : Mathf.Clamp(horizontal / Mathf.Max(blend, 0.01f) * 0.5f, 0.8f, 1.5f));

        // isGrounded у CharacterController мигает, поэтому даём небольшую задержку
        airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;
        bool grounded = airTime < 0.15f;

        animator.SetBool(GroundedId, grounded);
        animator.SetBool(JumpId, !grounded && delta.y > 0.5f);
        animator.SetBool(FreeFallId, airTime > 0.3f);
    }
}
