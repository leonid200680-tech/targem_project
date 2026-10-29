using UnityEngine;

public class ScreamerGhost : MonoBehaviour
{
    public float speed = 10f;

    void Update()
    {
        // transform.right — это красная стрелка (вправо).
        // Минус перед ней означает "влево" (противоположно красной).
        transform.position += -transform.right * speed * Time.deltaTime;
    }
}