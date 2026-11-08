using UnityEngine;
using UnityEngine.AI;

// Последний этап бабки: она носится по стенам и потолку. Настоящая бабка (навмеш, погоня, поимка, шкафы)
// остаётся на полу и только прячет свою модель, а по поверхностям над ней бежит копия без логики,
// поэтому поведение и проходимость не меняются. Рядом с игроком копия спрыгивает на пол.
[DefaultExecutionOrder(100)]
public class GrannySurfaceCrawl : MonoBehaviour
{
    enum Surface { Floor, WallLeft, WallRight, Ceiling }

    public float jumpTime = 0.4f;
    public Vector2 switchEvery = new Vector2(1.4f, 3f);
    [Tooltip("Ближе этого расстояния до игрока она спрыгивает на пол")]
    public float dropDistance = 6.5f;
    public float ceilingDropDistance = 3.8f;

    private NavMeshAgent agent;
    private GhostChase chase;
    private Animator sourceAnim, bodyAnim;
    private Transform player;
    private Renderer[] ownRenderers;
    private GameObject body;

    private Surface current = Surface.Floor;
    private float nextSwitch, missTime, jumpT = 1f;
    private Vector3 jumpFromPos, lastDir, heldOffset, heldUp = Vector3.up;
    private Quaternion jumpFromRot;

    private Transform head, spine, chest;
    private Quaternion headTurn = Quaternion.identity;
    private float arch, twitch, twitchTarget, twitchUntil, nextTwitch;

    private bool holding;
    private float burstSwitch;
    private AudioSource clicks;
    private static AudioClip clickClip;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        chase = GetComponent<GhostChase>();
        sourceAnim = GetComponent<Animator>();
        var p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;
        lastDir = transform.forward;

        body = GrannyJumpscare.MakeCopy(gameObject);
        body.name = "Granny Surface Body";
        body.transform.SetPositionAndRotation(transform.position, transform.rotation);
        body.SetActive(true);
        bodyAnim = body.GetComponent<Animator>();
        bodyAnim.Play("crawl", 0, 0f);
        head = bodyAnim.GetBoneTransform(HumanBodyBones.Head);
        spine = bodyAnim.GetBoneTransform(HumanBodyBones.Spine);
        chest = bodyAnim.GetBoneTransform(HumanBodyBones.Chest);

        clicks = body.AddComponent<AudioSource>();
        clicks.spatialBlend = 1f;
        clicks.rolloffMode = AudioRolloffMode.Linear;
        clicks.minDistance = 1.5f;
        clicks.maxDistance = 20f;
        clicks.dopplerLevel = 0f;
        clicks.playOnAwake = false;

        ownRenderers = GetComponentsInChildren<Renderer>();
        foreach (var r in ownRenderers) r.enabled = false;
        nextSwitch = Time.time + 0.6f;
    }

    void OnEnable() { if (body != null) ShowBody(); }
    void OnDisable() { if (body != null) body.SetActive(false); }
    void OnDestroy() { if (body != null) Destroy(body); }

    void ShowBody()
    {
        body.SetActive(true);
        bodyAnim.Play("crawl", 0, 0f);
    }

    void LateUpdate()
    {
        if (body == null || player == null) return;

        // Скример показывает свою копию
        if (GrannyJumpscare.IsPlaying) { if (body.activeSelf) body.SetActive(false); return; }
        if (!body.activeSelf) ShowBody();

        Vector3 feet = transform.position;
        Vector3 flat = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
        if (flat.sqrMagnitude > 0.04f) lastDir = flat.normalized;

        // На лестнице, у шкафа и рядом с игроком - только пол
        // С потолка она падает почти на голову, со стены спрыгивает раньше
        float drop = current == Surface.Ceiling ? ceilingDropDistance : dropDistance;
        bool free = !chase.Scripted && Mathf.Abs(agent.velocity.y) < 0.4f && Vector3.Distance(feet, player.position) >= drop;
        if (!free) Switch(Surface.Floor);
        else if (Time.time >= nextSwitch) Switch(PickSurface(feet));

        if (Pose(current, feet, out Vector3 pos, out Vector3 up))
        {
            missTime = 0f;
            heldOffset = pos - feet;
            heldUp = up;
        }
        else
        {
            // Дверной проём: перемахивает его по той же линии, стена кончилась совсем - перепрыгивает
            missTime += Time.deltaTime;
            if (missTime > 0.5f)
            {
                Switch(free && Pose(Surface.Ceiling, feet, out _, out _) ? Surface.Ceiling : Surface.Floor);
                Pose(current, feet, out pos, out up);
            }
            else
            {
                pos = feet + heldOffset;
                up = heldUp;
            }
        }

        Vector3 forward = Vector3.ProjectOnPlane(lastDir, up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(transform.forward, up);
        Quaternion rot = Quaternion.LookRotation(forward.normalized, up);

        var t = body.transform;
        if (jumpT < 1f)
        {
            jumpT = Mathf.Min(1f, jumpT + Time.deltaTime / jumpTime);
            float k = Mathf.SmoothStep(0f, 1f, jumpT);
            Vector3 p = Vector3.Lerp(jumpFromPos, pos, k);
            // Дуга через середину коридора
            p += (feet + Vector3.up * 1.2f - p) * Mathf.Sin(k * Mathf.PI) * 0.35f;
            t.SetPositionAndRotation(p, Quaternion.Slerp(jumpFromRot, rot, k));
            if (jumpT >= 1f) AudioSource.PlayClipAtPoint(Pentagram.StoneSound(), pos, 0.7f);
        }
        else if ((t.position - pos).sqrMagnitude > 49f) t.SetPositionAndRotation(pos, rot);   // её телепортировали
        else
        {
            // Издалека движется рывками: замерла и смотрит, сорвалась с места, снова замерла
            if (Time.time >= burstSwitch)
            {
                holding = free && !holding;
                burstSwitch = Time.time + (holding ? Random.Range(0.08f, 0.24f) : Random.Range(0.25f, 0.6f));
                if (!holding) Click();
            }
            if (!holding) t.SetPositionAndRotation(Vector3.Lerp(t.position, pos, Time.deltaTime * 20f), Quaternion.Slerp(t.rotation, rot, Time.deltaTime * 14f));
        }

        // Неровный темп: то семенит, то почти замирает. В замирании тело стоит, а голова продолжает следить
        bodyAnim.speed = holding ? 0f : sourceAnim.speed * Mathf.Lerp(0.55f, 1.3f, Mathf.PerlinNoise(Time.time * 2.2f, 0.7f));
        PoseBones(Vector3.Distance(feet, player.position));
    }

    // Своя анимация поверх клипа: кости правим после Animator, каждый кадр заново
    void PoseBones(float distance)
    {
        var t = body.transform;
        float now = Time.time;
        float moving = Mathf.Clamp01(agent.velocity.magnitude / 2f);

        // Спина ходит волной из стороны в сторону
        float wave = Mathf.Sin(now * 8.5f) * 9f * moving;
        Twist(spine, t.up, wave);
        Twist(chest, t.up, -wave * 1.4f);
        Twist(chest, t.forward, Mathf.Sin(now * 8.5f + 1f) * 7f * moving);

        // Перед броском встаёт на дыбы, в прыжке выгибается
        float rear = current == Surface.Floor ? Mathf.InverseLerp(dropDistance, 2.5f, distance) : 0f;
        float jump = jumpT < 1f ? Mathf.Sin(jumpT * Mathf.PI) : 0f;
        arch = Mathf.MoveTowards(arch, rear * 42f + jump * 30f, 160f * Time.deltaTime);
        Twist(spine, t.right, -arch * 0.45f);
        Twist(chest, t.right, -arch * 0.55f);

        if (head == null) return;

        // Голова следит за игроком под ненормальным наклоном и резко дёргается
        if (now >= nextTwitch)
        {
            nextTwitch = now + Random.Range(0.8f, 2.4f);
            twitchUntil = now + 0.16f;
            twitchTarget = Random.value < 0.5f ? -55f : 55f;
        }
        twitch = Mathf.MoveTowards(twitch, now < twitchUntil ? twitchTarget : 0f, 900f * Time.deltaTime);

        Vector3 toPlayer = (Camera.main != null ? Camera.main.transform.position : player.position + Vector3.up * 1.6f) - head.position;
        Vector3 face = head.forward;
        Quaternion wanted = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(face, toPlayer.normalized), 80f);
        headTurn = Quaternion.Slerp(headTurn, wanted, Time.deltaTime * 9f);
        head.rotation = Quaternion.AngleAxis(18f + twitch, toPlayer.normalized) * headTurn * head.rotation;
    }

    void Click()
    {
        if (clicks == null) return;
        clicks.pitch = Random.Range(0.85f, 1.25f);
        clicks.PlayOneShot(ClickClip(), Random.Range(0.6f, 1f));
    }

    // Сухой щёлк когтей: несколько коротких ударов по твёрдому
    static AudioClip ClickClip()
    {
        if (clickClip != null) return clickClip;
        const int rate = 44100;
        int n = (int)(rate * 0.28f);
        var d = new float[n];
        var rnd = new System.Random(5);
        float[] hits = { 0f, 0.07f, 0.12f, 0.2f };
        foreach (float at in hits)
        {
            float ph = 0f, freq = 2400f + (float)rnd.NextDouble() * 1800f;
            for (int i = (int)(at * rate); i < n; i++)
            {
                float t = i / (float)rate - at;
                ph += freq / rate;
                d[i] += (Mathf.Sin(ph * 2f * Mathf.PI) * 0.5f + ((float)rnd.NextDouble() * 2f - 1f) * 0.5f) * Mathf.Exp(-t * 260f) * 0.55f;
            }
        }
        clickClip = AudioClip.Create("Granny Claws", n, 1, rate, false);
        clickClip.SetData(d, 0);
        return clickClip;
    }

    static void Twist(Transform bone, Vector3 axis, float angle)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }

    void Switch(Surface next)
    {
        nextSwitch = Time.time + Random.Range(switchEvery.x, switchEvery.y);
        if (next == current) return;
        current = next;
        missTime = 0f;
        jumpT = 0f;
        jumpFromPos = body.transform.position;
        jumpFromRot = body.transform.rotation;
    }

    Surface PickSurface(Vector3 feet)
    {
        var options = new System.Collections.Generic.List<Surface>();
        // Стены в списке чаще потолка: в коридоре они эффектнее
        foreach (Surface s in new[] { Surface.WallLeft, Surface.WallRight, Surface.WallLeft, Surface.WallRight, Surface.Ceiling })
            if (s != current && Pose(s, feet, out _, out _)) options.Add(s);
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : current;
    }

    // Точка и нормаль поверхности над настоящей бабкой
    bool Pose(Surface s, Vector3 feet, out Vector3 pos, out Vector3 up)
    {
        pos = feet;
        up = Vector3.up;
        if (s == Surface.Floor) return true;

        Vector3 origin = feet + Vector3.up * 1.2f;
        bool ceiling = s == Surface.Ceiling;
        Vector3 side = Vector3.Cross(Vector3.up, lastDir) * (s == Surface.WallRight ? 1f : -1f);
        Vector3 dir = ceiling ? Vector3.up : side;
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, ceiling ? 3.6f : 3.2f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.collider.CompareTag("Player")) return false;
        if (ceiling ? hit.normal.y > -0.7f : Mathf.Abs(hit.normal.y) > 0.25f) return false;

        pos = hit.point + hit.normal * 0.04f;
        up = hit.normal;
        return true;
    }
}
