using UnityEngine;

public class FlyingGhost : MonoBehaviour
{
    public float flySpeed = 15f;

    private Vector3 targetPosition;
    private bool isFlying = false;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    public void StartFlight(Vector3 startPos, Vector3 endPos)
    {
        transform.position = startPos;
        targetPosition = endPos;
        transform.LookAt(targetPosition);

        gameObject.SetActive(true);
        isFlying = true;
    }

    void Update()
    {
        if (!isFlying) return;

        // √Œ¬Œ–»Ã BLEND TREE, ◊“Œ Ã€ ¡≈∆»Ã Õ¿ Œ√–ŒÃÕŒ… — Œ–Œ—“»
        if (anim != null)
        {
            anim.SetFloat("Speed", 10f);
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, flySpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            isFlying = false;
            gameObject.SetActive(false);
        }
    }
}