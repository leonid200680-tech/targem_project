using System.Collections;
using UnityEngine;

public class FlyingGhost : MonoBehaviour
{
    public float speed = 20f; // Скорость полета

    private Vector3 targetPosition;
    private bool isFlying = false;

    public void StartFlight(Vector3 startPos, Vector3 endPos)
    {
        transform.position = startPos; // Ставим в начало
        targetPosition = endPos;       // Запоминаем, куда лететь

        // Разворачиваем её носом в сторону цели (чтобы летела лицом вперед)
        transform.LookAt(targetPosition);
        // Если модель смотрит назад, раскомментируй строку ниже:
        // transform.Rotate(0, 180, 0);

        gameObject.SetActive(true);
        isFlying = true;

        // Запускаем таймер, чтобы она исчезла, если вдруг промахнется
        Invoke("StopFlight", 3f);
    }

    void Update()
    {
        if (!isFlying) return;

        // Двигаем бабку к финишной точке
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        // Если долетела до конца — исчезает
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            StopFlight();
        }
    }

    public void StopFlight()
    {
        isFlying = false;
        gameObject.SetActive(false);
    }
}