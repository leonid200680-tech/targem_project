using UnityEngine;
using UnityEngine.Serialization;

// Правая рука модели держит выбранный предмет (зажигалку / фонарик) сбоку.
// Предмет "приклеен" к ладони, а пальцы сгибаются, пока не упрутся в его габариты,
// поэтому ни предмет, ни пальцы не проходят друг сквозь друга.
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
        [FormerlySerializedAs("grip")] public Transform item;

        [Tooltip("Куда смотрят выпрямленные пальцы (в осях камеры)")]
        public Vector3 fingerDirection = Vector3.down;
        [Tooltip("Куда смотрит ладонь, то есть в сторону предмета (в осях камеры)")]
        public Vector3 palmNormal = Vector3.left;
        [Tooltip("Точка хвата: смещение от центра предмета в метрах (в осях камеры)")]
        public Vector3 gripPoint;
        [Tooltip("Доворот предмета в руке вокруг его центра (градусы, в осях камеры)")]
        public Vector3 itemEuler;
        [Tooltip("Круглый предмет (фонарик): считать цилиндром, а не коробкой")]
        public bool round;
        [Tooltip("Насколько сгибать большой палец (0 - не трогать)")]
        [Range(0, 1)] public float thumbCurl = 0.6f;
        [Tooltip("Куда положить кончик большого пальца: смещение от точки хвата в метрах (в осях камеры). Ноль - просто сгибать")]
        public Vector3 thumbTarget;

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
    public float palmSkin = 0.012f;    // от кости до поверхности ладони, м
    public float fingerRadius = 0.008f; // толщина пальца, м
    public float curlStep = 3f;

    [Header("Наклон тела к взгляду")]
    [Range(0, 1)] public float bodyLookWeight = 0.3f;

    private Animator animator;
    private Transform hand, middleProximal;
    private Transform[][] fingers; // указательный, средний, безымянный, мизинец
    private Transform[] thumb;
    private Vector3 fingerLocal, palmLocal, sideLocal; // оси кисти в её собственном пространстве

    private HeldItem current;
    private HeldItem last;
    private float weight;
    private Quaternion desiredHandRot;
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

    // Матрица предмета в "кадровом" положении (как его расставили в holder)
    Matrix4x4 RestMatrix(HeldItem it)
    {
        Transform parent = it.item.parent;
        Matrix4x4 m = parent.localToWorldMatrix * Matrix4x4.TRS(it.restLocalPos, it.restLocalRot, it.item.localScale);
        if (it.itemEuler == Vector3.zero) return m;

        // доворачиваем вокруг центра предмета, оси - камеры
        Vector3 c = m.MultiplyPoint(it.meshBounds.center);
        Quaternion cam = cameraTransform.rotation;
        Quaternion r = cam * Quaternion.Euler(it.itemEuler) * Quaternion.Inverse(cam);
        return Matrix4x4.TRS(c, r, Vector3.one) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one) * m;
    }

    Quaternion DesiredHandRotation(HeldItem it)
    {
        Quaternion cam = cameraTransform.rotation;
        Vector3 f = cam * it.fingerDirection.normalized;
        Vector3 n = cam * it.palmNormal.normalized;
        return Quaternion.LookRotation(f, n) * Quaternion.Inverse(Quaternion.LookRotation(fingerLocal, palmLocal));
    }

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

    // Половина толщины предмета вдоль направления dir
    float HalfThickness(Matrix4x4 m, Bounds b, Vector3 dir)
    {
        if (last != null && last.round)
        {
            Cylinder(m, b, out _, out Vector3 halfAxis, out float radius);
            return radius + Mathf.Abs(Vector3.Dot(dir, halfAxis.normalized)) * halfAxis.magnitude;
        }
        Vector3 e = b.extents;
        return Mathf.Abs(Vector3.Dot(dir, m.MultiplyVector(new Vector3(e.x, 0, 0))))
             + Mathf.Abs(Vector3.Dot(dir, m.MultiplyVector(new Vector3(0, e.y, 0))))
             + Mathf.Abs(Vector3.Dot(dir, m.MultiplyVector(new Vector3(0, 0, e.z))));
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

        // Локоть вниз и наружу, как у человека, который держит что-то перед собой
        Vector3 shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
        animator.SetIKHintPosition(AvatarIKHint.RightElbow,
            shoulder + cameraTransform.rotation * new Vector3(0.3f, -0.45f, 0.05f));

        // Где должна оказаться кисть, чтобы ладонь легла на предмет в его кадровом положении
        desiredHandRot = DesiredHandRotation(last);
        ikGoalRot = desiredHandRot * Quaternion.Inverse(goalToBone);
        animator.SetIKRotation(AvatarIKGoal.RightHand, ikGoalRot);
        Matrix4x4 m = RestMatrix(last);
        Vector3 n = desiredHandRot * palmLocal;
        Vector3 grip = m.MultiplyPoint(last.meshBounds.center) + cameraTransform.rotation * last.gripPoint;
        Vector3 palmSurface = grip - n * HalfThickness(m, last.meshBounds, n);
        Vector3 palmCenterFromWrist = desiredHandRot * (Quaternion.Inverse(hand.rotation) * ((middleProximal.position - hand.position) * 0.5f)) + n * palmSkin;
        animator.SetIKPosition(AvatarIKGoal.RightHand, palmSurface - palmCenterFromWrist);
    }

    void LateUpdate()
    {
        if (last == null || cameraTransform == null) return;
        float w = W;
        if (w <= 0f) return;

        // Калибровка: при полном весе IK кисть повёрнута ровно как цель, остаток - смещение рига
        if (w > 0.99f)
            goalToBone = Quaternion.Slerp(goalToBone, Quaternion.Inverse(ikGoalRot) * hand.rotation, 0.5f);

        desiredHandRot = DesiredHandRotation(last);
        hand.rotation = Quaternion.Slerp(hand.rotation, desiredHandRot, w);

        if (current == null) return; // предмет уже убран, рука просто опускается

        // Предмет жёстко привязан к ладони
        Matrix4x4 rest = RestMatrix(last);
        Quaternion restRot = rest.rotation;
        Quaternion relRot = Quaternion.Inverse(desiredHandRot) * restRot;
        Transform item = last.item;
        item.rotation = hand.rotation * relRot;

        Vector3 n = hand.rotation * palmLocal;
        Vector3 palmSurface = hand.position + (middleProximal.position - hand.position) * 0.5f + n * palmSkin;
        Vector3 gripOffset = hand.rotation * (Quaternion.Inverse(desiredHandRot) * (cameraTransform.rotation * last.gripPoint));
        Vector3 gripNow = item.TransformPoint(last.meshBounds.center) + gripOffset;
        Matrix4x4 m = item.localToWorldMatrix;
        Vector3 target = palmSurface + n * HalfThickness(m, last.meshBounds, n);
        item.position += target - gripNow;

        // Пальцы обхватывают предмет
        Vector3 curlAxis = hand.rotation * sideLocal;
        foreach (var chain in fingers) Curl(chain, curlAxis, 100f, item, last.meshBounds);
        if (last.thumbTarget != Vector3.zero)
            ReachThumb(target + hand.rotation * (Quaternion.Inverse(desiredHandRot) * (cameraTransform.rotation * last.thumbTarget)));
        else if (last.thumbCurl > 0f)
            Curl(thumb, hand.rotation * fingerLocal, 70f * last.thumbCurl, item, last.meshBounds);

        // Большой палец гнётся сложнее остальных: если он всё же задел предмет - выталкиваем
        PushOut(thumb, new[] { hand.rotation * fingerLocal, hand.rotation * sideLocal, hand.rotation * palmLocal }, item, last.meshBounds);
    }

    // Простой CCD: тянем кончик большого пальца к точке на предмете
    void ReachThumb(Vector3 targetPoint)
    {
        if (thumb[0] == null || thumb[1] == null || thumb[2] == null) return;
        for (int iter = 0; iter < 6; iter++)
            for (int s = 2; s >= 0; s--)
            {
                Transform bone = thumb[s];
                Vector3 toTip = Tip(thumb) - bone.position;
                Vector3 toTarget = targetPoint - bone.position;
                Quaternion rot = Quaternion.FromToRotation(toTip, toTarget);
                rot.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                bone.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -25f, 25f), axis) * bone.rotation;
            }
    }

    void PushOut(Transform[] chain, Vector3[] axes, Transform item, Bounds b)
    {
        if (chain[0] == null) return;
        // сначала двигаем основание пальца, потом среднюю фалангу
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
