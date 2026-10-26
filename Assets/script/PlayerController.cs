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
    public float acceleration = 10f;

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
        if (PauseMenu.GameIsPaused) return;
        HandleMouseLook();
        HandleMovement();
        HandleHeadBob();
        HandleStaminaUI();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * 2f;
        float mouseY = Input.GetAxis("Mouse Y") * 2f;

        transform.Rotate(Vector3.up * mouseX);

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
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && z > 0;

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
        Vector3 targetMove = (transform.right * x + transform.forward * z).normalized * targetSpeed;

        currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, targetMove, Time.deltaTime * acceleration);
        controller.Move(currentMoveVelocity * Time.deltaTime);

        // --- ЗВУКИ ШАГОВ ---
        HandleFootsteps();

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleFootsteps()
    {
        // Если мы на земле и движемся
        if (controller.isGrounded && currentMoveVelocity.magnitude > 1f)
        {
            stepTimer -= Time.deltaTime;

            if (stepTimer <= 0f)
            {
                PlayFootstepSound();
                // Устанавливаем таймер в зависимости от того, бежим мы или идем
                stepTimer = isRunning ? runStepInterval : walkStepInterval;
            }
        }
        else
        {
            stepTimer = 0f; // Сбрасываем, если остановились
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
        if (cameraTransform == null) return;

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