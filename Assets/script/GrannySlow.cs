using UnityEngine;
using UnityEngine.AI;

// Замедление бабки от бутылки: сначала она на секунду останавливается и вскрикивает,
// потом несколько секунд еле плетётся. GhostChase каждый кадр сам задаёт скорость,
// поэтому замедление накладывается после него.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(NavMeshAgent))]
public class GrannySlow : MonoBehaviour
{
    public float stunTime = 1f;

    private NavMeshAgent agent;
    private Animator anim;
    private float slowUntil;
    private float stunUntil;
    private float factor = 1f;

    [HideInInspector] public float animRate = 1f;   // темп анимации, задаёт GhostChase (ползком)

    public bool IsSlowed => Time.time < slowUntil;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    public void Slow(float seconds, float speedFactor)
    {
        slowUntil = Time.time + seconds;
        stunUntil = Time.time + stunTime;
        factor = speedFactor;
        var sounds = GetComponent<GrannySounds>();
        if (sounds != null) sounds.Hurt();
    }

    void Update()
    {
        if (agent == null || !agent.enabled) return;
        bool slowed = Time.time < slowUntil;
        if (Time.time < stunUntil) agent.speed = 0.05f;
        else if (slowed) agent.speed *= factor;
        if (anim != null) anim.speed = (slowed ? Mathf.Lerp(0.5f, 1f, factor) : 1f) * animRate;
    }
}
