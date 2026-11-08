using System.Collections;
using UnityEngine;

// Шкаф-укрытие. По E (на дверцу или на сам шкаф) игрок плавно залезает внутрь, дверцы не открываются.
// Внутри камера на уровне глаз, смотрит на дверцы: сквозь щели решётки видно комнату, можно чуть осмотреться.
// Изнутри по E - игрок плавно выходит. Пока игрок внутри, бабка его не видит и не может схватить.
// Точки "внутри" и "выход" создаются сами по габаритам шкафа, если не заданы вручную.
public class HideableCloset : MonoBehaviour
{
    [Header("Настройки")]
    public Transform hidePoint; // Точка внутри шкафа (ноги игрока)
    public Transform exitPoint; // Точка, куда игрок выйдет (перед шкафом)
    public string prompt = "Нажмите E, чтобы спрятаться";
    public float eyeHeight = 1.4f;   // не используется: высота считается по габаритам шкафа
    [Tooltip("Изнутри дверцы прозрачные (у модели одна сторона), поэтому на них изнутри ставится панель со щелями")]
    public Material insideSlatsMaterial;

    [Header("Внутри")]
    public float enterTime = 0.75f;
    public float exitTime = 0.55f;
    public float lookYaw = 45f;      // насколько можно повернуть голову внутри
    public float lookPitch = 25f;

    public static bool IsPlayerHidden = false; // Глобальный флаг: игрок в укрытии
    // Один раз за игру бабка подходит к шкафу, где сидит игрок, и заглядывает в решётку (но не трогает)
    public static bool PeekUsed = false;
    // Один раз за игру она оказывается в шкафу вместе с игроком. Только смотрит: не ловит и рассудок не забирает
    public static bool FaceUsed = false;

    private bool isPlayerInside = false;
    private bool moving = false;
    private int enteredFrame = -1;
    private GameObject playerObj;
    private PlayerController pc;
    private GameObject slats;
    private Vector3 front = Vector3.forward;
    private Vector3 frontDoorPoint;   // точка перед дверцами, через неё проходит путь внутрь и наружу
    private float lookX, lookY;

    void Start()
    {
        playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) pc = playerObj.GetComponent<PlayerController>();
        BuildPoints();
    }

    // Внутренняя точка - центр шкафа, лицом к дверцам; выход - перед дверцами
    void BuildPoints()
    {
        var doors = GetComponentsInChildren<InteractableDoor>(true);
        var body = transform.Find("Cube");
        var r = body != null ? body.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
        if (r == null) return;
        Bounds b = r.bounds;

        front = Vector3.zero;
        foreach (var d in doors)
        {
            var dr = d.GetComponent<Renderer>();
            front += (dr != null ? dr.bounds.center : d.transform.position) - b.center;
        }
        front.y = 0f;
        if (front.sqrMagnitude < 1e-4f) front = transform.forward;
        front.Normalize();

        float floor = b.min.y;
        if (Physics.Raycast(b.center, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point.y;
        float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z))));

        // Панель со щелями изнутри на дверцах: сквозь щели видно комнату, как в хоррорах
        if (insideSlatsMaterial != null && transform.Find("Inside Slats") == null && doors.Length > 0)
        {
            Bounds db = doors[0].GetComponent<Renderer>().bounds;
            foreach (var d in doors) db.Encapsulate(d.GetComponent<Renderer>().bounds);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Inside Slats";
            q.transform.SetParent(transform, true);
            Vector3 c = db.center - front * (Mathf.Abs(Vector3.Dot(db.extents, new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z)))) + 0.02f);
            q.transform.SetPositionAndRotation(c, Quaternion.LookRotation(front));   // лицом внутрь шкафа (квад виден со стороны -forward)
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float width = Mathf.Abs(Vector3.Dot(db.size, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));
            q.transform.localScale = new Vector3(width, db.size.y, 1f);
            var qr = q.GetComponent<MeshRenderer>();
            qr.sharedMaterial = insideSlatsMaterial;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.SetActive(false);   // видна только пока игрок внутри
            slats = q;
        }

        // Ноги игрока - на полу шкафа, чуть позади центра, чтобы глаза были внутри и дверцы перед лицом.
        // Если шкаф низкий, опускаем так, чтобы камера была под крышей.
        float camHeight = CameraHeight();
        float rootY = Mathf.Min(floor + 0.02f, b.max.y - 0.22f - camHeight);
        Vector3 inside = new Vector3(b.center.x, rootY, b.center.z) - front * Mathf.Min(0.1f, depth * 0.2f);
        if (hidePoint == null)
        {
            hidePoint = new GameObject("HidePoint").transform;
            hidePoint.SetParent(transform, true);
        }
        hidePoint.SetPositionAndRotation(inside, Quaternion.LookRotation(front));

        frontDoorPoint = new Vector3(b.center.x, floor + 0.02f, b.center.z) + front * (depth + 0.45f);

        if (exitPoint == null)
        {
            exitPoint = new GameObject("exit point").transform;
            exitPoint.SetParent(transform, true);
            Vector3 basePos = new Vector3(b.center.x, floor + 0.05f, b.center.z);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Vector3 p = basePos + front * (depth + 0.75f);
            // Перед шкафом может стоять мебель: ищем свободное место чуть дальше или сбоку
            foreach (float dist in new[] { 0.75f, 1.0f, 1.3f })
            {
                bool found = false;
                foreach (float s in new[] { 0f, 0.5f, -0.5f, 0.9f, -0.9f })
                {
                    Vector3 q = basePos + front * (depth + dist) + side * s;
                    if (!Physics.CheckCapsule(q + Vector3.up * 0.45f, q + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    { p = q; found = true; break; }
                }
                if (found) break;
            }
            exitPoint.SetPositionAndRotation(p, Quaternion.LookRotation(front));
        }
    }

    // Высота камеры над ногами игрока
    float CameraHeight()
    {
        FindPlayer();
        if (pc != null && pc.cameraTransform != null)
            return pc.cameraTransform.position.y - pc.transform.position.y;
        return 1.64f;
    }

    void FindPlayer()
    {
        // Игрок выключен, пока открыто главное меню, поэтому ищем его лениво
        if (pc != null) return;
        pc = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (pc != null) playerObj = pc.gameObject;
    }

    void Update()
    {
        if (!isPlayerInside || moving) return;

        // Выход из шкафа (не в тот же кадр, когда залезли)
        if (Time.frameCount != enteredFrame && BotInput.GetKeyDown(KeyCode.E) && !PauseMenu.GameIsPaused)
        {
            StartCoroutine(ExitRoutine());
            return;
        }

        // Внутри можно немного осмотреться
        if (PauseMenu.GameIsPaused || pc == null || pc.cameraTransform == null) return;
        lookX = Mathf.Clamp(lookX + BotInput.GetAxis("Mouse X") * GameSettings.MouseSensitivity, -lookYaw, lookYaw);
        lookY = Mathf.Clamp(lookY - BotInput.GetAxis("Mouse Y") * GameSettings.MouseSensitivity, -lookPitch, lookPitch);
        playerObj.transform.rotation = hidePoint.rotation * Quaternion.Euler(0f, lookX, 0f);
        SetPitch(lookY);
    }

    public void Interact()
    {
        if (!isPlayerInside && !moving && !IsPlayerHidden) StartCoroutine(EnterRoutine());
    }

    void SetPitch(float x)
    {
        pc.Pitch = x;
        var e = pc.cameraTransform.localEulerAngles;
        pc.cameraTransform.localEulerAngles = new Vector3(x, e.y, e.z);
    }

    IEnumerator EnterRoutine()
    {
        FindPlayer();
        if (pc == null || hidePoint == null) yield break;
        moving = true;
        isPlayerInside = true;
        enteredFrame = Time.frameCount;
        pc.enabled = false;
        IsPlayerHidden = true; // Игрок невидим для бабки
        lookX = lookY = 0f;
        // Дверцы должны быть закрыты: игрок проходит сквозь них, внутри видна решётка
        foreach (var d in GetComponentsInChildren<InteractableDoor>(true)) d.SetOpen(false);

        // Подходим к дверцам и плавно "проскальзываем" внутрь, разворачиваясь лицом к дверцам
        yield return Glide(new[] { frontDoorPoint, hidePoint.position }, hidePoint.rotation, 0f, enterTime);
        moving = false;

        if (!FaceUsed && PickupItem.stonesCollected >= 2) { FaceUsed = true; StartCoroutine(FaceBehind()); }
        else if (!PeekUsed) StartCoroutine(GrannyPeek());
    }

    // Двери закрылись, всё стихло, за спиной дыхание. Камера сама медленно поворачивается - лицо вплотную
    IEnumerator FaceBehind()
    {
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        var cam = pc.cameraTransform;
        if (hm == null || hm.realGhost == null) yield break;
        moving = true;   // ни выйти, ни осмотреться

        OutdoorSound.HushFor(9f);
        yield return new WaitForSeconds(1.2f);
        var breath = cam.gameObject.AddComponent<AudioSource>();
        breath.spatialBlend = 0f;
        breath.clip = BreathClip();
        breath.loop = true;
        breath.Play();
        yield return new WaitForSeconds(1.8f);

        Quaternion startRot = playerObj.transform.rotation;
        Quaternion backRot = startRot * Quaternion.Euler(0f, 170f, 0f);
        SetPitch(0f);
        Vector3 fwd = backRot * Vector3.forward;

        var face = GrannyJumpscare.MakeCopy(hm.realGhost);
        face.name = "Closet Granny";
        var anim = face.GetComponent<Animator>();
        Transform head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        face.transform.rotation = Quaternion.LookRotation(-fwd);
        face.SetActive(true);
        if (anim != null) { anim.speed = 0f; anim.Play("скример", 0, 0.15f); anim.Update(0f); }
        Vector3 startPos = playerObj.transform.position;
        // Камера смещена от центра игрока: считаем, где она окажется после поворота
        Vector3 eyeLine = startPos + backRot * (Quaternion.Inverse(startRot) * (cam.position - startPos));
        // Шкаф неглубокий: сдвигаем игрока к дверцам, иначе лицо окажется за задней стенкой
        Vector3 toDoors = front * 0.27f;
        playerObj.transform.position = startPos + toDoors;
        eyeLine += toDoors;

        // Голова ставится так же, как в скримере: на таком расстоянии лицо видно целиком
        void PlaceFace(float dist)
        {
            Vector3 headAt = head != null ? head.position : face.transform.position + Vector3.up * 1.55f;
            face.transform.position += eyeLine + fwd * dist - Vector3.up * 0.1f - headAt;
        }
        const float faceFar = 0.44f, faceNear = 0.33f;
        PlaceFace(faceFar);

        // Тусклый свет снизу: иначе в шкафу лица не видно
        var lightGo = new GameObject("Closet Face Light");
        lightGo.transform.SetParent(cam, false);
        lightGo.transform.localPosition = new Vector3(0.05f, -0.3f, 0.05f);
        var glow = lightGo.AddComponent<Light>();
        lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        glow.type = LightType.Point;
        glow.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        glow.intensity = 45f;
        glow.range = 2f;
        glow.color = new Color(1f, 0.75f, 0.6f);
        glow.shadows = LightShadows.None;

        const float turnTime = 2.4f;
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            playerObj.transform.rotation = Quaternion.Slerp(startRot, backRot, Mathf.SmoothStep(0f, 1f, t / turnTime));
            yield return null;
        }
        playerObj.transform.rotation = backRot;

        // Лицо медленно придвигается
        for (float t = 0f; t < 1.7f; t += Time.deltaTime)
        {
            PlaceFace(Mathf.Lerp(faceFar, faceNear, t / 1.7f));
            yield return null;
        }

        // Моргнул - её нет
        Destroy(face);
        Destroy(lightGo);
        breath.Stop();
        Destroy(breath);
        playerObj.transform.SetPositionAndRotation(startPos, startRot);
        lookX = lookY = 0f;
        SetPitch(0f);
        moving = false;
    }

    static AudioClip breathClip;

    // Медленное хриплое дыхание у самого уха
    static AudioClip BreathClip()
    {
        if (breathClip != null) return breathClip;
        const int rate = 22050;
        int n = rate * 3;
        var data = new float[n];
        var rnd = new System.Random(9);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float white = (float)rnd.NextDouble() * 2f - 1f;
            lp += (white - lp) * (0.12f + 0.1f * Mathf.Abs(Mathf.Sin(t * 19f)));
            data[i] = lp * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI / 1.5f)), 1.6f) * 1.6f;
        }
        breathClip = AudioClip.Create("Closet Breath", n, 1, rate, false);
        breathClip.SetData(data, 0);
        return breathClip;
    }

    // Бабка рядом (по пути не дальше 30 м) - через пару секунд она подходит и заглядывает в щели
    IEnumerator GrannyPeek()
    {
        yield return new WaitForSeconds(Random.Range(1.5f, 3f));
        if (!isPlayerInside || PeekUsed) yield break;
        var chase = FindAnyObjectByType<GhostChase>();
        if (chase == null) yield break;
        if (!chase.TryPeekCloset(frontDoorPoint, -front, Random.Range(4.5f, 6f))) yield break;
        PeekUsed = true;

        // Встала у дверец, лицо у самой решётки. Пару раз дёргает дверцу, будто пробует открыть - и не открывает.
        while (chase != null && chase.Scripted && !chase.Peeking) yield return null;
        if (chase == null || !chase.Peeking) yield break;
        InteractableDoor door = null;
        foreach (var d in GetComponentsInChildren<InteractableDoor>(true))
            if (door == null || Vector3.Distance(d.transform.position, chase.transform.position) < Vector3.Distance(door.transform.position, chase.transform.position)) door = d;
        if (door == null) yield break;
        float speed = door.speed;
        door.speed = 14f;
        yield return new WaitForSeconds(1.6f);
        for (int i = 0; i < 2 && chase.Peeking; i++)
        {
            door.SetAjar(0.02f);
            yield return new WaitForSeconds(0.12f);
            door.SetOpen(false);
            yield return new WaitForSeconds(0.5f);
        }
        while (chase.Peeking) yield return null;
        door.speed = speed;
    }

    IEnumerator ExitRoutine()
    {
        moving = true;
        if (slats != null) slats.SetActive(false);
        Quaternion outRot = exitPoint != null ? exitPoint.rotation : playerObj.transform.rotation;
        Vector3 target = exitPoint != null ? exitPoint.position : frontDoorPoint;
        yield return Glide(new[] { frontDoorPoint, target }, outRot, 0f, exitTime);

        IsPlayerHidden = false; // Игрок снова видим
        isPlayerInside = false;
        if (pc != null) pc.enabled = true;
        moving = false;
    }

    // Плавное перемещение игрока по ломаной (с выключенным CharacterController), поворот и наклон камеры
    IEnumerator Glide(Vector3[] points, Quaternion endRot, float endPitch, float time)
    {
        var cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var t0 = playerObj.transform;
        Vector3 start = t0.position;
        Quaternion startRot = t0.rotation;
        float startPitch = pc.Pitch;

        // Длины отрезков, чтобы скорость по пути была ровной
        var path = new Vector3[points.Length + 1];
        path[0] = start;
        for (int i = 0; i < points.Length; i++) path[i + 1] = points[i];
        float total = 0f;
        for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i - 1], path[i]);

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / time));
            t0.position = AlongPath(path, total, k);
            t0.rotation = Quaternion.Slerp(startRot, endRot, Mathf.Clamp01(k * 1.4f));
            SetPitch(Mathf.Lerp(startPitch, endPitch, k));
            yield return null;
        }
        t0.SetPositionAndRotation(path[path.Length - 1], endRot);
        SetPitch(endPitch);

        if (cc != null) cc.enabled = true;
    }

    static Vector3 AlongPath(Vector3[] path, float total, float k)
    {
        float d = k * total;
        for (int i = 1; i < path.Length; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            if (d <= seg || i == path.Length - 1)
                return Vector3.Lerp(path[i - 1], path[i], seg > 1e-4f ? Mathf.Clamp01(d / seg) : 1f);
            d -= seg;
        }
        return path[path.Length - 1];
    }

    void OnDisable()
    {
        if (isPlayerInside) IsPlayerHidden = false;
    }
}
