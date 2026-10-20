using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform cameraTransform; // Сюда перетащи Main Camera

    [Header("Настройки скорости")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float crouchSpeed = 2f;
    public float acceleration = 10f; // Плавность набора скорости

    [Header("Прыжок и гравитация")]
    public float gravity = -15f;
    public float jumpHeight = 1.2f;

    [Header("Ставина")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaDrain = 15f; // Сколько тратится в секунду
    public float staminaRegen = 10f; // Сколько восстанавливается в секунду
    public Slider staminaSlider;     // Ссылка на UI полоску стамины (если есть)

    [Header("Качание камеры (Head Bob)")]
    public float bobSpeed = 14f;
    public float bobAmount = 0.05f;
    private float defaultCamY;

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
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleHeadBob();
        HandleStaminaUI();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * 2f;
        float mouseY = Input.GetAxis("Mouse Y") * 2f;

        // Поворот тела
        transform.Rotate(Vector3.up * mouseX);

        // Поворот камеры (вверх/вниз)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (cameraTransform != null)
        {
            // Применяем поворот по X, но оставляем Y для покачивания
            Vector3 currentCamRot = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(xRotation, currentCamRot.y, currentCamRot.z);
        }
    }

    void HandleMovement()
    {
        // Получаем ввод
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Проверяем бег и стамину
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && z > 0;

        if (wantsToRun && currentStamina > 0)
        {
            isRunning = true;
            currentStamina -= staminaDrain * Time.deltaTime;
        }
        else
        {
            isRunning = false;
            // Восстанавливаем стамину, если не бежим
            if (currentStamina < maxStamina)
                currentStamina += staminaRegen * Time.deltaTime;
        }
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        // Выбираем скорость
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 targetMove = (transform.right * x + transform.forward * z).normalized * targetSpeed;

        // Плавное ускорение/замедление
        currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, targetMove, Time.deltaTime * acceleration);
        controller.Move(currentMoveVelocity * Time.deltaTime);

        // Гравитация и прыжок
        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f; // Прижимаем к земле

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleHeadBob()
    {
        if (cameraTransform == null) return;

        // Если мы движемся по земле, качаем камеру
        if (controller.isGrounded && currentMoveVelocity.magnitude > 0.1f)
        {
            // Скорость качания зависит от того, бежим мы или идем
            float currentBobSpeed = isRunning ? bobSpeed * 1.5f : bobSpeed;
            float currentBobAmount = isRunning ? bobAmount * 1.5f : bobAmount;

            bobTimer += Time.deltaTime * currentBobSpeed;

            // Вычисляем смещение по Y и X (восьмерка)
            float yOffset = Mathf.Sin(bobTimer) * currentBobAmount;
            float xOffset = Mathf.Cos(bobTimer / 2) * (currentBobAmount / 2);

            // Применяем смещение к позиции камеры
            Vector3 targetPos = new Vector3(xOffset, defaultCamY + yOffset, 0);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Time.deltaTime * 10f);
        }
        else
        {
            // Возвращаем камеру в исходное положение, если стоим
            bobTimer = 0;
            Vector3 targetPos = new Vector3(0, defaultCamY, 0);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Time.deltaTime * 10f);
        }
    }

    void HandleStaminaUI()
    {
        if (staminaSlider != null)
        {
            // Плавно обновляем полоску стамины
            staminaSlider.value = Mathf.Lerp(staminaSlider.value, currentStamina / maxStamina, Time.deltaTime * 10f);
        }
    }
}