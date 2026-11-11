using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;

// VR-режим (шлем через OpenXR). Если шлема нет, скрипт ничего не делает, и игра идёт с клавиатуры и мыши.
// Игровую логику не переписываем:
//  - контроллеры "жмут" те же клавиши через BotInput (A = E, курок = ЛКМ и т.д.);
//  - голова шлема крутит и двигает игровую камеру, тело игрока поворачивается рывками правым стиком;
//  - предметы в руках (фонарик, зажигалка, бутылка) переезжают в правую руку, фонарик светит туда, куда она указывает;
//  - экранные меню и HUD становятся панелью перед глазами, по кнопкам меню жмут лучом из руки (курком).
[DefaultExecutionOrder(-10000)]
public class VRSupport : MonoBehaviour
{
    public static bool Active { get; private set; }

    // Ввод контроллеров для BotInput
    public static Vector2 Move;          // левый стик
    public static bool Run;              // левый grip (держать)
    public static float TurnDegrees;     // рывок поворота в этом кадре, градусы
    public static float Scroll;          // правый стик вверх/вниз: соседний слот инвентаря
    static readonly HashSet<KeyCode> down = new HashSet<KeyCode>();
    public static bool KeyDown(KeyCode key) => Active && down.Contains(key);
    public static bool AnyDown => Active && down.Count > 0;

    const float SnapAngle = 30f;
    const float UiDistance = 1.6f, UiWidth = 2f;  // HUD: панель 2 м шириной в 1,6 м перед глазами
    const float MaxLean = 0.3f;                    // насколько голова может уйти от тела (наклон), м

    // Голова и руки в пространстве трекинга
    Vector3 hmdPos; Quaternion hmdRot = Quaternion.identity;
    Vector3 originPos; Quaternion originYaw = Quaternion.identity; bool hasOrigin;
    Transform space, leftHand, rightHand, uiAnchor;
    LineRenderer laser;

    // Игрок
    PlayerController player;
    Transform playerCam;
    Vector3 eyeLocal;
    bool handsMoved;

    // Не-игровые камеры (меню, катсцены) двигаем только на время отрисовки
    Camera borrowed; Vector3 borrowedPos; Quaternion borrowedRot;

    readonly Dictionary<string, bool> prev = new Dictionary<string, bool>();
    readonly HashSet<Canvas> converted = new HashSet<Canvas>();
    float nextCanvasScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => new GameObject("VR Support").AddComponent<VRSupport>();

    IEnumerator Start()
    {
        // OpenXR поднимает шлем в первые кадры; ждём до 3 секунд
        for (float t = 0; t < 3f && !XRSettings.isDeviceActive; t += Time.unscaledDeltaTime) yield return null;
        if (!XRSettings.isDeviceActive) { Destroy(gameObject); yield break; }

        Active = true;
        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        foreach (var s in inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);

        space = new GameObject("VR Tracking Space").transform;
        leftHand = MakeHand("Left Hand");
        rightHand = MakeHand("Right Hand");
        uiAnchor = new GameObject("VR UI").transform;

        laser = rightHand.gameObject.AddComponent<LineRenderer>();
        laser.useWorldSpace = false;
        laser.positionCount = 2;
        laser.SetPositions(new[] { Vector3.zero, Vector3.forward * 3f });
        laser.widthMultiplier = 0.004f;
        var laserShader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Sprites/Default");
        laser.material = new Material(laserShader);
        laser.material.SetColor("_UnlitColor", new Color(1f, 0.2f, 0.15f));
        laser.startColor = laser.endColor = new Color(1f, 0.2f, 0.15f);

        var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length > 0 && players[0].cameraTransform != null)
        {
            player = players[0];
            playerCam = player.cameraTransform;
            eyeLocal = playerCam.localPosition;
        }

        var module = FindFirstObjectByType<InputSystemUIInputModule>();
        if (module != null) module.xrTrackingOrigin = space;

        Application.onBeforeRender += BeforeRender;
        StartCoroutine(RestoreAfterFrame());
        Debug.Log("[VR] Шлем найден, VR-режим включён");
    }

    void OnDestroy()
    {
        if (!Active) return;
        Application.onBeforeRender -= BeforeRender;
        Active = false;
    }

    Transform MakeHand(string name)
    {
        var hand = new GameObject(name).transform;
        hand.SetParent(space, false);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.name = "Controller";
        ball.transform.SetParent(hand, false);
        ball.transform.localScale = new Vector3(0.05f, 0.05f, 0.09f);
        return hand;
    }

    // ---------- Ввод ----------

    void Update()
    {
        if (!Active) return;
        down.Clear();
        TurnDegrees = 0f; Scroll = 0f;

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        var l = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var r = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var p) || head.TryGetFeatureValue(CommonUsages.devicePosition, out p)) hmdPos = p;
        if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var q) || head.TryGetFeatureValue(CommonUsages.deviceRotation, out q)) hmdRot = q;
        if (!hasOrigin && head.isValid) Recenter();

        l.TryGetFeatureValue(CommonUsages.primary2DAxis, out var ls);
        r.TryGetFeatureValue(CommonUsages.primary2DAxis, out var rs);
        Move = ls.magnitude > 0.2f ? ls : Vector2.zero;
        l.TryGetFeatureValue(CommonUsages.gripButton, out Run);

        Button(r, CommonUsages.triggerButton, KeyCode.Mouse0);    // фонарик, бросок, зажигалка, далее в диалоге
        Button(r, CommonUsages.primaryButton, KeyCode.E);         // A - взаимодействовать
        Button(r, CommonUsages.secondaryButton, KeyCode.R);       // B - сменить батарейку
        Button(r, CommonUsages.primary2DAxisClick, KeyCode.Space); // клик правым стиком - прыжок
        Button(l, CommonUsages.triggerButton, KeyCode.F);         // левый курок - фонарик
        Button(l, CommonUsages.primaryButton, KeyCode.G);         // X - выбросить предмет
        Button(l, CommonUsages.secondaryButton, KeyCode.Escape);  // Y - пауза
        Button(l, CommonUsages.menuButton, KeyCode.Escape);

        // Правый стик: рывок влево/вправо поворачивает тело (и листает камеры на мониторе), вверх/вниз - слоты
        if (Flick("rx+", rs.x)) { TurnDegrees += SnapAngle; down.Add(KeyCode.RightArrow); }
        if (Flick("rx-", -rs.x)) { TurnDegrees -= SnapAngle; down.Add(KeyCode.LeftArrow); }
        if (Flick("ry+", rs.y)) Scroll = 1f;
        if (Flick("ry-", -rs.y)) Scroll = -1f;
        if (Flick("ls", l.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool lc) && lc ? 1f : 0f)) Recenter();

        if (Time.unscaledTime >= nextCanvasScan) { nextCanvasScan = Time.unscaledTime + 0.1f; ConvertCanvases(); }
    }

    void Button(InputDevice dev, InputFeatureUsage<bool> usage, KeyCode key)
    {
        dev.TryGetFeatureValue(usage, out bool on);
        string id = dev.characteristics + usage.name;
        prev.TryGetValue(id, out bool was);
        prev[id] = on;
        if (on && !was) down.Add(key);
    }

    bool Flick(string id, float v)
    {
        prev.TryGetValue(id, out bool was);
        bool on = was ? v > 0.3f : v > 0.7f;
        prev[id] = on;
        return on && !was;
    }

    void Recenter()
    {
        originPos = hmdPos;
        originYaw = Quaternion.Euler(0f, hmdRot.eulerAngles.y, 0f);
        hasOrigin = true;
    }

    // ---------- Голова, руки, камеры ----------

    void LateUpdate()
    {
        if (!Active) return;
        RestoreBorrowed();
        ApplyPose();
    }

    void BeforeRender()
    {
        if (!Active) return;
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var p)) hmdPos = p;
        if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var q)) hmdRot = q;
        ApplyPose();
    }

    void ApplyPose()
    {
        // Наклон головы допускаем, а дальше тело "догоняет": сдвигаем начало трекинга
        Vector3 off = hmdPos - originPos; off.y = 0f;
        if (off.magnitude > MaxLean) originPos += off * (1f - MaxLean / off.magnitude);

        Camera cam = ViewCamera();
        if (cam == null) return;

        Vector3 basePos; Quaternion yaw;
        bool isPlayer = playerCam != null && cam.transform == playerCam;
        if (isPlayer)
        {
            basePos = player.transform.TransformPoint(eyeLocal);
            yaw = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
        }
        else
        {
            if (borrowed != cam) { RestoreBorrowed(); borrowed = cam; borrowedPos = cam.transform.position; borrowedRot = cam.transform.rotation; }
            basePos = borrowedPos;
            yaw = Quaternion.Euler(0f, borrowedRot.eulerAngles.y, 0f);
        }

        space.rotation = yaw * Quaternion.Inverse(originYaw);
        space.position = basePos - space.rotation * originPos;
        cam.transform.SetPositionAndRotation(space.TransformPoint(hmdPos), space.rotation * hmdRot);

        PoseHand(XRNode.LeftHand, leftHand);
        PoseHand(XRNode.RightHand, rightHand);
        if (isPlayer) MoveHeldItems();
        UpdateUi(cam);
    }

    // Камера, которую сейчас видит игрок: включённая, рисует на экран, с наибольшей глубиной
    Camera ViewCamera()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
            if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
        return best;
    }

    void RestoreBorrowed()
    {
        if (borrowed != null) borrowed.transform.SetPositionAndRotation(borrowedPos, borrowedRot);
        borrowed = null;
    }

    IEnumerator RestoreAfterFrame()
    {
        var wait = new WaitForEndOfFrame();
        while (true) { yield return wait; RestoreBorrowed(); }
    }

    void PoseHand(XRNode node, Transform hand)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        bool ok = dev.TryGetFeatureValue(CommonUsages.devicePosition, out var p) & dev.TryGetFeatureValue(CommonUsages.deviceRotation, out var q);
        hand.gameObject.SetActive(ok);
        if (ok) { hand.localPosition = p; hand.localRotation = q; }
    }

    // Фонарик, зажигалка и бутылка переезжают из-под камеры в правую руку (один раз)
    void MoveHeldItems()
    {
        var ik = player.GetComponentInChildren<PlayerHandsIK>(true);
        if (!handsMoved)
        {
            handsMoved = true;
            if (ik != null)
            {
                ik.enabled = false; // рука модели больше не держит предметы: их держит контроллер
                foreach (var r in ik.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            foreach (Transform holder in new List<Transform>(playerCam.Cast<Transform>()))
            {
                if (!holder.name.EndsWith("Holder") || holder.childCount == 0) continue;
                Transform item = holder.GetChild(0);
                Vector3 axis = Vector3.forward;
                if (ik != null) foreach (var it in ik.items) if (it.item == item) axis = it.longAxisLocal;
                holder.SetParent(rightHand, false);
                holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity;
                // Длинная ось предмета (стекло фонарика, верх зажигалки) - вдоль руки, сам предмет - в ладони
                item.rotation = Quaternion.FromToRotation(item.rotation * axis, rightHand.forward) * item.rotation;
                item.position = rightHand.position;
            }
        }
        // Свет фонарика бьёт туда, куда указывает рука
        if (ik != null) foreach (var it in ik.items) if (it.aimAtView != null) it.aimAtView.rotation = rightHand.rotation;
    }

    // ---------- Интерфейс ----------

    void ConvertCanvases()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || converted.Contains(c) || c.renderMode == RenderMode.WorldSpace) continue;
            converted.Add(c);
            var scaler = c.GetComponent<UnityEngine.UI.CanvasScaler>();
            Vector2 size = scaler != null && scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize
                ? scaler.referenceResolution : new Vector2(Screen.width, Screen.height);
            c.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)c.transform;
            rt.SetParent(uiAnchor, false);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * (UiWidth / size.x);
            rt.localRotation = Quaternion.identity;
            rt.localPosition = new Vector3(0f, 0f, -0.002f * c.sortingOrder); // что выше по порядку, то ближе
            if (c.GetComponent<TrackedDeviceRaycaster>() == null) c.gameObject.AddComponent<TrackedDeviceRaycaster>();
        }
    }

    void UpdateUi(Camera cam)
    {
        foreach (var c in converted) if (c != null) c.worldCamera = cam;
        // Панель плавно догоняет взгляд по горизонтали и стоит на уровне глаз
        Transform h = cam.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(h.forward, Vector3.up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = h.up;
        Quaternion want = Quaternion.LookRotation(fwd.normalized);
        uiAnchor.rotation = Quaternion.Slerp(uiAnchor.rotation, want, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        uiAnchor.position = h.position + uiAnchor.forward * UiDistance;

        // Луч для меню виден, только когда игре нужен курсор
        laser.enabled = Cursor.visible || player == null || !player.gameObject.activeInHierarchy;
    }

    // Подсказки "Нажмите E" -> кнопки контроллера
    public static string Prompt(string text)
    {
        if (!Active || string.IsNullOrEmpty(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\wА-Яа-яЁё])(E|G|R|F|ЛКМ)(?![\wА-Яа-яЁё])", m =>
            m.Value == "E" ? "A" : m.Value == "G" ? "X" : m.Value == "R" ? "B" : "курок");
    }
}
