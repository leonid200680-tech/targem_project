using UnityEngine;

// Правая рука модели держит выбранный предмет (зажигалку / фонарик) кулаком, как в жизни:
// предмет лежит поперёк ладони у основания пальцев (его длинная сторона идёт от мизинца к указательному),
// пальцы сгибаются вокруг него, пока не упрутся, большой палец ложится сверху.
// Holder (LighterHolder / HandsHolder) включают скрипты Flashlight / LighterInHands,
// его положение задаёт, где предмет окажется в кадре.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Animator))]
public class PlayerHandsIK : MonoBehaviour
{
    [System.Serializable]
    public class HeldItem
    {
        public GameObject holder;
        public Transform item;

        [Tooltip("Куда смотрит длинная сторона предмета - верх зажигалки, стекло фонарика (в осях камеры). " +
                 "Этот конец торчит из кулака со стороны большого и указательного пальцев")]
        public Vector3 itemAxis = Vector3.up;
        [Tooltip("Длинная ось предмета в его собственных координатах (в сторону верха / стекла)")]
        public Vector3 longAxisLocal = Vector3.forward;
        [Tooltip("Насколько центр предмета сдвинут из кулака вдоль его оси, м (+ - к верху / стеклу)")]
        public float gripAlong;
        [Tooltip("Круглый предмет (фонарик): считать цилиндром, а не коробкой")]
        public bool round;
        [Tooltip("Насколько сгибать большой палец")]
        [Range(0, 1)] public float thumbCurl = 0.7f;
        [Tooltip("Повернуть этот объект (свет фонарика) туда, куда смотрит камера")]
        public Transform aimAtView;

        [HideInInspector] public Vector3 restLocalPos;
        [HideInInspector] public Quaternion restLocalRot;
        [HideInInspector] public Bounds meshBounds;
        [HideInInspector] public bool wasActive;
    }

    [Header("Ссылки")]
    public Transform cameraTransform;
    public HeldItem[] items;

    [Header("Анимация доставания")]
    public float raiseTime = 0.45f;

    [Header("Рука")]
    public float palmSkin = 0.012f;     // от кости до поверхности ладони, м
    public float fingerRadius = 0.008f; // толщина пальца, м
    [Range(0.3f, 1.2f)] public float gripAcrossPalm = 0.85f; // где по ладони лежит предмет: 0 - запястье, 1 - костяшки
    public float curlStep = 3f;

    [Header("Наклон тела к взгляду")]
    [Range(0, 1)] public float bodyLookWeight = 0.3f;

    private Animator animator;
    private Transform hand, middleProximal;
    private Transform[][] fingers; // указательный, средний, безымянный, мизинец
    private Transform[] thumb;
    private Vector3 fingerLocal, palmLocal, sideLocal; // оси кисти в её собственном пространстве
    private Vector3 palmCenterLocal;

    private HeldItem current;
    private HeldItem last;
    private float weight;
    private Quaternion ikGoalRot;
    // Разница между IK-целью гуманоида и реальным поворотом кости кисти (константа рига, калибруется на лету)
    private Quaternion goalToBone = Quaternion.identity;

    static readonly HumanBodyBones[,] FingerBones =
    {
        { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
        { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
        { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
        { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
    };

    void Awake()
    {
        animator = GetComponent<Animator>();
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        middleProximal = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

        fingers = new Transform[4][];
        for (int f = 0; f < 4; f++)
        {
            fingers[f] = new Transform[3];
            for (int s = 0; s < 3; s++) fingers[f][s] = animator.GetBoneTransform(FingerBones[f, s]);
        }
        thumb = new[]
        {
            animator.GetBoneTransform(HumanBodyBones.RightThumbProximal),
            animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate),
            animator.GetBoneTransform(HumanBodyBones.RightThumbDistal),
        };

        // Оси кисти: пальцы, поперёк ладони (от указательного к мизинцу) и нормаль ладони
        Quaternion inv = Quaternion.Inverse(hand.rotation);
        fingerLocal = (inv * (middleProximal.position - hand.position)).normalized;
        Vector3 side = inv * (fingers[3][0].position - fingers[0][0].position);
        sideLocal = Vector3.ProjectOnPlane(side, fingerLocal).normalized;
        palmLocal = -Vector3.Cross(fingerLocal, sideLocal); // для правой руки
        palmCenterLocal = inv * ((middleProximal.position - hand.position) * gripAcrossPalm);

        foreach (var it in items)
        {
            if (it.holder == null || it.item == null) continue;
            it.restLocalPos = it.item.localPosition;
            it.restLocalRot = it.item.localRotation;
            var mf = it.item.GetComponent<MeshFilter>();
            it.meshBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.01f);
        }
    }

    void Update()
    {
        HeldItem active = null;
        foreach (var it in items)
        {
            if (it.holder == null || it.item == null) continue;
            bool isActive = it.holder.activeInHierarchy;
            if (isActive && !it.wasActive && last != it) weight = 0f; // достаём новый предмет от бедра
            it.wasActive = isActive;
            if (isActive) active = it;
        }
        current = active;
        if (active != null) last = active;

        weight = Mathf.MoveTowards(weight, current != null ? 1f : 0f, Time.deltaTime / raiseTime);
    }

    float W => Mathf.SmoothStep(0f, 1f, weight);

    // Где предмет должен быть в кадре: центр предмета в положении, как его расставили в holder
    Vector3 RestCenter(HeldItem it)
    {
        Matrix4x4 m = it.item.parent.localToWorldMatrix * Matrix4x4.TRS(it.restLocalPos, it.restLocalRot, it.item.localScale);
        return m.MultiplyPoint(it.meshBounds.center);
    }

    // Где держим локоть: вниз и наружу, как у человека, который держит что-то перед собой
    Vector3 ElbowHint() =>
        animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position + cameraTransform.rotation * new Vector3(0.3f, -0.45f, 0.05f);

    // Кисть: от мизинца к указательному - вдоль оси предмета. Пястье продолжает предплечье настолько,
    // насколько позволяет ось предмета, - запястье гнётся ровно на нужный угол, а не выворачивается.
    // Ладонь при этом сама оказывается обращённой к предмету.
    Quaternion DesiredHandRotation(HeldItem it)
    {
        Vector3 axis = (cameraTransform.rotation * it.itemAxis).normalized;
        Vector3 forearm = RestCenter(it) - ElbowHint();
        Vector3 metacarpal = Vector3.ProjectOnPlane(forearm, axis);
        if (metacarpal.sqrMagnitude < 1e-6f) metacarpal = Vector3.ProjectOnPlane(cameraTransform.forward, axis);
        return Quaternion.LookRotation(axis, metacarpal) * Quaternion.Inverse(Quaternion.LookRotation(-sideLocal, fingerLocal));
    }

    // Поворот предмета: длинная ось - по оси хвата, плоская сторона - к ладони
    Quaternion ItemRotation(HeldItem it, Quaternion handRot)
    {
        Vector3 axis = handRot * -sideLocal;
        Vector3 palm = handRot * palmLocal;
        Vector3 thin = ThinAxisLocal(it);
        return Quaternion.LookRotation(axis, -palm) * Quaternion.Inverse(Quaternion.LookRotation(it.longAxisLocal.normalized, thin));
    }

    // Самая тонкая ось предмета (кроме длинной) - этой стороной он лежит на ладони
    static Vector3 ThinAxisLocal(HeldItem it)
    {
        Vector3 e = Vector3.Scale(it.meshBounds.extents, it.item.lossyScale);
        Vector3 l = it.longAxisLocal.normalized;
        Vector3 best = Vector3.up;
        float min = float.MaxValue;
        foreach (var a in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            if (Mathf.Abs(Vector3.Dot(a, l)) > 0.9f) continue;
            float size = Mathf.Abs(Vector3.Dot(e, a));
            if (size < min) { min = size; best = a; }
        }
        return best;
    }

    // Половина толщины предмета в сторону ладони
    float HalfThickness(HeldItem it)
    {
        Vector3 e = Vector3.Scale(it.meshBounds.extents, it.item.lossyScale);
        Vector3 l = it.longAxisLocal.normalized;
        float a = 0f, b = float.MaxValue;
        foreach (var ax in new[] { Vector3.right, Vector3.up, Vector3.forward })
        {
            if (Mathf.Abs(Vector3.Dot(ax, l)) > 0.9f) continue;
            float size = Mathf.Abs(Vector3.Dot(e, ax));
            a = Mathf.Max(a, size);
            b = Mathf.Min(b, size);
        }
        return it.round ? (a + b) * 0.5f : b;
    }

    // Смещение центра предмета от кисти при заданном повороте кисти
    Vector3 CenterFromWrist(HeldItem it, Quaternion handRot)
    {
        Vector3 palm = handRot * palmLocal;
        Vector3 axis = handRot * -sideLocal;
        return handRot * palmCenterLocal + palm * (palmSkin + HalfThickness(it)) + axis * it.gripAlong;
    }

    // ---------- Расстояние до предмета ----------

    // Ось и радиус цилиндра, вписанного в бокс (ось - самая длинная сторона)
    static void Cylinder(Matrix4x4 m, Bounds b, out Vector3 center, out Vector3 halfAxis, out float radius)
    {
        Vector3 e = b.extents;
        Vector3 x = m.MultiplyVector(new Vector3(e.x, 0, 0));
        Vector3 y = m.MultiplyVector(new Vector3(0, e.y, 0));
        Vector3 z = m.MultiplyVector(new Vector3(0, 0, e.z));
        center = m.MultiplyPoint(b.center);
        if (x.magnitude >= y.magnitude && x.magnitude >= z.magnitude) { halfAxis = x; radius = (y.magnitude + z.magnitude) * 0.5f; }
        else if (y.magnitude >= z.magnitude) { halfAxis = y; radius = (x.magnitude + z.magnitude) * 0.5f; }
        else { halfAxis = z; radius = (x.magnitude + y.magnitude) * 0.5f; }
    }

    // Расстояние от точки до предмета (отрицательное - внутри)
    float BoxDistance(Transform item, Bounds b, Vector3 p)
    {
        if (last != null && last.round)
        {
            Cylinder(item.localToWorldMatrix, b, out Vector3 c, out Vector3 halfAxis, out float radius);
            Vector3 axis = halfAxis.normalized;
            float t = Mathf.Clamp(Vector3.Dot(p - c, axis), -halfAxis.magnitude, halfAxis.magnitude);
            return Vector3.Distance(p, c + axis * t) - radius;
        }
        Vector3 lp = item.InverseTransformPoint(p) - b.center;
        Vector3 e = b.extents;
        bool inside = Mathf.Abs(lp.x) < e.x && Mathf.Abs(lp.y) < e.y && Mathf.Abs(lp.z) < e.z;
        Vector3 cp = new Vector3(Mathf.Clamp(lp.x, -e.x, e.x), Mathf.Clamp(lp.y, -e.y, e.y), Mathf.Clamp(lp.z, -e.z, e.z));
        float d = Vector3.Distance(p, item.TransformPoint(cp + b.center));
        return inside ? -d : d;
    }

    // ---------- IK ----------

    void OnAnimatorIK(int layerIndex)
    {
        if (cameraTransform == null) return;

        animator.SetLookAtWeight(1f, bodyLookWeight, 0.6f, 0f, 0.5f);
        animator.SetLookAtPosition(cameraTransform.position + cameraTransform.forward * 5f);

        float w = W;
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        // Поворот кисти тоже отдаём IK: тогда скручивание уходит в предплечье, а не ломает запястье
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w);
        if (last == null || w <= 0f) return;

        animator.SetIKHintPosition(AvatarIKHint.RightElbow, ElbowHint());

        // Кисть ставим так, чтобы предмет в кулаке оказался там, где его расставили в holder
        Quaternion handRot = DesiredHandRotation(last);
        ikGoalRot = handRot * Quaternion.Inverse(goalToBone);
        animator.SetIKRotation(AvatarIKGoal.RightHand, ikGoalRot);
        animator.SetIKPosition(AvatarIKGoal.RightHand, RestCenter(last) - CenterFromWrist(last, handRot));
    }

    void LateUpdate()
    {
        if (last == null || cameraTransform == null || fingers == null) return;
        float w = W;
        if (w <= 0f) return;

        // Калибровка: при полном весе IK кисть повёрнута ровно как цель, остаток - смещение рига
        if (w > 0.99f)
            goalToBone = Quaternion.Slerp(goalToBone, Quaternion.Inverse(ikGoalRot) * hand.rotation, 0.5f);

        hand.rotation = Quaternion.Slerp(hand.rotation, DesiredHandRotation(last), w);

        if (current == null) return; // предмет уже убран, рука просто опускается

        // Предмет лежит в кулаке
        Transform item = last.item;
        item.rotation = ItemRotation(last, hand.rotation);
        Vector3 center = hand.position + CenterFromWrist(last, hand.rotation);
        item.position += center - item.TransformPoint(last.meshBounds.center);

        if (last.aimAtView != null) last.aimAtView.rotation = cameraTransform.rotation;

        // Пальцы обхватывают предмет, большой палец ложится сверху
        Vector3 curlAxis = hand.rotation * sideLocal;
        foreach (var chain in fingers) Curl(chain, curlAxis, 110f, item, last.meshBounds);
        if (last.thumbCurl > 0f)
        {
            // Большой палец ложится поверх согнутых пальцев - на среднюю фалангу указательного/среднего,
            // как у сжатого кулака
            Vector3 itemCenter = item.TransformPoint(last.meshBounds.center);
            Vector3 itemAxis = hand.rotation * -sideLocal;
            Vector3 rest = Vector3.Lerp(fingers[0][1].position, fingers[1][1].position, last.thumbCurl * 0.6f);
            Vector3 outward = Vector3.ProjectOnPlane(rest - itemCenter, itemAxis).normalized;
            ReachThumb(rest + outward * fingerRadius * 1.6f);
            PushOut(thumb, new[] { hand.rotation * fingerLocal, hand.rotation * sideLocal, hand.rotation * palmLocal }, item, last.meshBounds);
        }
    }

    // Простой CCD: тянем кончик большого пальца к точке
    void ReachThumb(Vector3 targetPoint)
    {
        if (thumb[0] == null || thumb[1] == null || thumb[2] == null) return;
        for (int iter = 0; iter < 8; iter++)
            for (int s = 2; s >= 0; s--)
            {
                Transform bone = thumb[s];
                Vector3 toTip = Tip(thumb) - bone.position;
                Vector3 toTarget = targetPoint - bone.position;
                Quaternion rot = Quaternion.FromToRotation(toTip, toTarget);
                rot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                bone.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -20f, 20f), axis) * bone.rotation;
            }
    }

    void PushOut(Transform[] chain, Vector3[] axes, Transform item, Bounds b)
    {
        if (chain[0] == null) return;
        for (int s = 0; s < 2; s++)
        {
            Transform bone = chain[s];
            for (int i = 0; i < 30 && MinDistance(chain, s, item, b) < fingerRadius; i++)
            {
                float best = MinDistance(chain, s, item, b);
                Quaternion bestStep = Quaternion.identity;
                foreach (var axis in axes)
                    foreach (float sign in new[] { 1f, -1f })
                    {
                        Quaternion step = Quaternion.AngleAxis(sign * curlStep, axis);
                        bone.rotation = step * bone.rotation;
                        float d = MinDistance(chain, s, item, b);
                        bone.rotation = Quaternion.Inverse(step) * bone.rotation;
                        if (d > best) { best = d; bestStep = step; }
                    }
                if (bestStep == Quaternion.identity) break;
                bone.rotation = bestStep * bone.rotation;
            }
        }
    }

    Vector3 Tip(Transform[] chain)
    {
        Vector3 d = chain[2].position - chain[1].position;
        return chain[2].position + d * 0.8f;
    }

    // Сгибаем фаланги по очереди, пока следующие суставы/кончик не коснутся предмета
    void Curl(Transform[] chain, Vector3 axis, float maxAngle, Transform item, Bounds b)
    {
        if (chain[0] == null || chain[1] == null || chain[2] == null) return;

        // В какую сторону "сжимать": ту, где кончик приближается к предмету
        Vector3 center = item.TransformPoint(b.center);
        Quaternion probe = Quaternion.AngleAxis(5f, axis);
        Vector3 tip = Tip(chain);
        Vector3 tipProbe = chain[0].position + probe * (tip - chain[0].position);
        float sign = Vector3.Distance(tipProbe, center) < Vector3.Distance(tip, center) ? 1f : -1f;

        for (int s = 0; s < 3; s++)
        {
            Transform bone = chain[s];

            // Если фаланга уже внутри предмета - разгибаем
            for (float a = 0; a < 60f && MinDistance(chain, s, item, b) < fingerRadius; a += curlStep)
                bone.rotation = Quaternion.AngleAxis(-sign * curlStep, axis) * bone.rotation;

            for (float a = 0; a < maxAngle; a += curlStep)
            {
                Quaternion step = Quaternion.AngleAxis(sign * curlStep, axis);
                bone.rotation = step * bone.rotation;
                if (MinDistance(chain, s, item, b) < fingerRadius)
                {
                    bone.rotation = Quaternion.Inverse(step) * bone.rotation;
                    break;
                }
            }
        }
    }

    float MinDistance(Transform[] chain, int fromSegment, Transform item, Bounds b)
    {
        float min = float.MaxValue;
        for (int s = fromSegment + 1; s < 3; s++)
            min = Mathf.Min(min, BoxDistance(item, b, chain[s].position));
        Vector3 tip = Tip(chain);
        min = Mathf.Min(min, BoxDistance(item, b, tip));
        // середина фаланги, чтобы не "перешагнуть" угол бокса
        min = Mathf.Min(min, BoxDistance(item, b, (chain[2].position + tip) * 0.5f));
        return min;
    }
}
