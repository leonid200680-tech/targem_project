using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform cameraTransform;

    [Header("Настройки скорости")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float acceleration = 30f;   // разгон, м/с за секунду
    public float deceleration = 45f;   // торможение: отпустил клавишу - почти сразу стоит

    [Header("Прыжок и гравитация")]
    public float gravity = -15f;
    public float jumpHeight = 1.2f;

    [Header("Ставина")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaDrain = 15f;
    public float staminaRegen = 10f;
    public Slider staminaSlider;

    [Header("Качание камеры (Head Bob)")]
    public float bobSpeed = 14f;
    public float bobAmount = 0.05f;
    private float defaultCamY;

    [Header("Звуки шагов")]
    public AudioSource audioSource;      // Ссылка на AudioSource
    public AudioClip[] footstepClips;    // Массив звуков (перетащим сюда)
    public float walkStepInterval = 0.5f;// Интервал шагов при ходьбе
    public float runStepInterval = 0.3f; // Интервал шагов при беге
    private float stepTimer = 0f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 currentMoveVelocity;
    private float xRotation = 0f;
    // Наклон камеры вверх/вниз (шкаф выставляет его сам, пока игрок внутри)
    public float Pitch { get => xRotation; set => xRotation = value; }
    private bool isRunning = false;
    private float bobTimer = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
            defaultCamY = cameraTransform.localPosition.y;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (DialogueManager.IsDialogueActive) return; // Игрок не ходит во время диалога

        if (PauseMenu.GameIsPaused) return;
        HandleMouseLook();
        HandleMovement();
        HandleHeadBob();
        HandleStaminaUI();
    }

    void HandleMouseLook()
    {
        float mouseX = BotInput.GetAxis("Mouse X") * GameSettings.MouseSensitivity;
        float mouseY = BotInput.GetAxis("Mouse Y") * GameSettings.MouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        if (VRSupport.Active) return; // наклон головы задаёт шлем

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cameraTransform != null)
        {
            Vector3 currentCamRot = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(xRotation, currentCamRot.y, currentCamRot.z);
        }
    }

    void HandleMovement()
    {
        // Raw: без встроенного сглаживания ввода, из-за него игрок "доезжал" после отпускания клавиши
        float x = BotInput.GetAxisRaw("Horizontal");
        float z = BotInput.GetAxisRaw("Vertical");

        bool wantsToRun = BotInput.GetKey(KeyCode.LeftShift) && z > 0;

        if (wantsToRun && currentStamina > 0)
        {
            isRunning = true;
            currentStamina -= staminaDrain * Time.deltaTime;
        }
        else
        {
            isRunning = false;
            if (currentStamina < maxStamina)
                currentStamina += staminaRegen * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        // В VR идём туда, куда смотрит голова
        Vector3 fwd = transform.forward, right = transform.right;
        if (VRSupport.Active && cameraTransform != null)
        {
            fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, fwd);
        }
        Vector3 targetMove = (right * x + fwd * z).normalized * targetSpeed;

        float rate = targetMove.sqrMagnitude < currentMoveVelocity.sqrMagnitude ? deceleration : acceleration;
        currentMoveVelocity = Vector3.MoveTowards(currentMoveVelocity, targetMove, rate * Time.deltaTime);
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (BotInput.GetButtonDown("Jump") && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        // Один Move на кадр: после отдельного горизонтального isGrounded сбрасывался, и прыжок срабатывал через раз
        controller.Move((currentMoveVelocity + velocity) * Time.deltaTime);

        // --- ЗВУКИ ШАГОВ ---
        HandleFootsteps();
    }

    void HandleFootsteps()
    {
        // Проверяем: на земле ли мы, движемся ли, и НЕ ИГРАЕТ ЛИ УЖЕ ЗВУК
        if (controller.isGrounded && currentMoveVelocity.magnitude > 0.1f && !audioSource.isPlaying)
        {
            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0f)
            {
                PlayFootstepSound();
                // Ставим таймер (Walk = 0.6, Run = 0.3)
                stepTimer = isRunning ? runStepInterval : walkStepInterval;
            }
        }
        else if (!controller.isGrounded || currentMoveVelocity.magnitude <= 0.1f)
        {
            // Если стоим — сбрасываем таймер
            stepTimer = 0f;
        }
    }

   

    void PlayFootstepSound()
    {
        if (audioSource != null && footstepClips.Length > 0)
        {
            // Выбираем случайный звук из массива
            int randomIndex = Random.Range(0, footstepClips.Length);
            AudioClip clip = footstepClips[randomIndex];

            // Небольшой рандом по высоте тона, чтобы звук не был одинаковым
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip);
        }
    }

    void HandleHeadBob()
    {
        if (cameraTransform == null || VRSupport.Active) return; // в VR качание камеры укачивает

        if (controller.isGrounded && currentMoveVelocity.magnitude > 0.1f)
        {
            float currentBobSpeed = isRunning ? bobSpeed * 1.5f : bobSpeed;
            float currentBobAmount = isRunning ? bobAmount * 1.5f : bobAmount;

            bobTimer += Time.deltaTime * currentBobSpeed;

            float yOffset = Mathf.Sin(bobTimer) * currentBobAmount;
            float xOffset = Mathf.Cos(bobTimer / 2) * (currentBobAmount / 2);

            Vector3 targetPos = new Vector3(xOffset, defaultCamY + yOffset, 0);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Time.deltaTime * 10f);
        }
        else
        {
            bobTimer = 0;
            Vector3 targetPos = new Vector3(0, defaultCamY, 0);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Time.deltaTime * 10f);
        }
    }

    void HandleStaminaUI()
    {
        if (staminaSlider != null)
        {
            staminaSlider.value = Mathf.Lerp(staminaSlider.value, currentStamina / maxStamina, Time.deltaTime * 10f);
        }
    }
}