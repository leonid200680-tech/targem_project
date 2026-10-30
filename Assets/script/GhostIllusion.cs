using UnityEngine;

public class GhostIllusion : MonoBehaviour
{
    // Этот метод вызовет триггер, когда игрок подойдет
    public void Vanish()
    {
        // Просто мгновенно выключаем объект (бабка исчезает)
        gameObject.SetActive(false);
    }
}