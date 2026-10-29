using System.Collections;
using UnityEngine;

public class HideableCloset : MonoBehaviour
{
    [Header("Настройки")]
    public Transform hidePoint; // Точка внутри шкафа
    public Transform exitPoint; // Точка, куда игрок выйдет (перед шкафом)
    public string prompt = "Нажмите E, чтобы спрятаться";

    public static bool IsPlayerHidden = false; // Глобальный флаг: игрок в укрытии

    private bool isPlayerInside = false;
    private GameObject playerObj;
    private PlayerController pc;

    void Start()
    {
        playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) pc = playerObj.GetComponent<PlayerController>();
    }

    void Update()
    {
        // Выход из шкафа
        if (isPlayerInside && Input.GetKeyDown(KeyCode.E))
        {
            ExitCloset();
        }
    }

    public void Interact()
    {
        if (!isPlayerInside)
        {
            EnterCloset();
        }
    }

    void EnterCloset()
    {
        if (pc == null || hidePoint == null) return;

        isPlayerInside = true;
        IsPlayerHidden = true; // Игрок невидим для бабки

        // 1. Отключаем управление
        pc.enabled = false;

        // 2. Телепортируем игрока
        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerObj.transform.position = hidePoint.position;
        playerObj.transform.rotation = hidePoint.rotation;

        if (cc != null) cc.enabled = true;

        Debug.Log("Игрок спрятался. Бабка потеряла его!");
    }

    void ExitCloset()
    {
        isPlayerInside = false;
        IsPlayerHidden = false; // Игрок снова видим

        CharacterController cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // Выходим в точку выхода
        if (exitPoint != null)
        {
            playerObj.transform.position = exitPoint.position;
            playerObj.transform.rotation = exitPoint.rotation;
        }

        if (cc != null) cc.enabled = true;

        if (pc != null) pc.enabled = true;

        Debug.Log("Игрок вышел из шкафа.");
    }
}