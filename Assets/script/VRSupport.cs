using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

// VR-режим (шлем через OpenXR: SteamVR, Meta Link). Если шлема нет, скрипт ничего не делает,
// и игра идёт с клавиатуры и мыши. Игровую логику не переписываем:
//  - контроллеры "жмут" те же клавиши через BotInput (A = E, курок = ЛКМ и т.д.);
//  - голова шлема крутит и двигает игровую камеру, тело игрока поворачивается рывками правым стиком;
//  - предметы в руках (фонарик, зажигалка, бутылка) переезжают в правую руку, фонарик светит туда, куда она указывает;
//  - экранные меню и HUD становятся панелью перед глазами, по кнопкам меню жмут лучом из руки (курком).
// Контроллеры читаем через Input System (основной путь OpenXR), старый XR API - запасной.
// Если руки не отслеживаются, они висят перед игроком, чтобы фонарик всё равно был виден и светил.
[DefaultExecutionOrder(-10000)]
public class VRSupport : MonoBehaviour
{
    public static bool Active { get; private set; }

    // Симулятор шлема в редакторе (VRSimulator): голову задаёт мышь, а не трекинг
    public static bool Simulated;
    public static Quaternion SimHeadRot = Quaternion.identity;

    // Ввод контроллеров для BotInput
    public static Vector2 Move;          // левый стик
    public static bool Run;              // левый grip (держать)
    public static float TurnDegrees;     // рывок поворота в этом кадре, градусы
    public static float Scroll;          // правый стик вверх/вниз: соседний слот инвентаря
    static readonly HashSet<KeyCode> down = new HashSet<KeyCode>();
    public static bool KeyDown(KeyCode key) => Active && down.Contains(key);
    public static bool AnyDown => Active && down.Count > 0;

    const float SnapAngle = 30f;
    // HUD: панель 0,9 м шириной в 0,7 м перед глазами (около 65 градусов обзора).
    // Ближе стен вагончика и коридоров, иначе меню проваливается в стену
    const float UiDistance = 0.7f, UiWidth = 0.9f;
    const float MaxLean = 0.3f;                    // насколько голова может уйти от тела (наклон), м

    // Голова и руки в пространстве трекинга
    Vector3 hmdPos; Quaternion hmdRot = Quaternion.identity;
    Vector3 originPos; Quaternion originYaw = Quaternion.identity; bool hasOrigin;
    Transform space, leftHand, rightHand, uiAnchor;
    LineRenderer laser;
    GameObject dot;                 // точка, куда упирается луч
    InputAction clickAct;           // курок любой руки - нажать кнопку меню
    UnityEngine.UI.Selectable hovered;
    UnityEngine.UI.Slider dragged;

    // Игрок текущей сцены
    PlayerController player;
    Transform playerCam;
    Vector3 eyeLocal;
    bool handsMoved;

    // Не-игровые камеры (меню, катсцены) двигаем только на время отрисовки
    Camera borrowed; Vector3 borrowedPos; Quaternion borrowedRot;

    readonly Dictionary<string, bool> prev = new Dictionary<string, bool>();
    readonly HashSet<Canvas> converted = new HashSet<Canvas>();
    float nextCanvasScan;

    // Действия контроллеров (Input System)
    class Hand
    {
        public InputAction pos, rot, tracked;
        public Hand(string side)
        {
            pos = Act(InputActionType.Value, $"<XRController>{{{side}}}/devicePosition");
            rot = Act(InputActionType.Value, $"<XRController>{{{side}}}/deviceRotation");
            tracked = Act(InputActionType.Button, $"<XRController>{{{side}}}/isTracked");
        }
    }
    Hand inL, inR;
    InputAction moveAct, turnAct, runAct, recenterAct;
    readonly List<(InputAction act, KeyCode key)> buttons = new List<(InputAction, KeyCode)>();

    static InputAction Act(InputActionType type, params string[] paths)
    {
        var a = new InputAction(type: type);
        foreach (var p in paths) a.AddBinding(p);
        a.Enable();
        return a;
    }
    static string L(string usage) => "<XRController>{LeftHand}/{" + usage + "}";
    static string R(string usage) => "<XRController>{RightHand}/{" + usage + "}";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("VR Support");
        DontDestroyOnLoad(go); // перезапуск уровня из паузы не должен выключать VR
        go.AddComponent<VRSupport>();
    }

    IEnumerator Start()
    {
        // OpenXR поднимает шлем в первые кадры; ждём до 5 секунд
        for (float t = 0; t < 5f && !XRSettings.isDeviceActive; t += Time.unscaledDeltaTime) yield return null;
        if (!XRSettings.isDeviceActive) { Destroy(gameObject); yield break; }

        Active = true;
#if UNITY_EDITOR
        Simulated = VRSimulator.IsMockRuntime;
#endif
        // Окно игры на мониторе часто без фокуса (игрок в шлеме) - ввод всё равно нужен
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground = true;

        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        foreach (var s in inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);

        inL = new Hand("LeftHand");
        inR = new Hand("RightHand");
        moveAct = Act(InputActionType.Value, L("Primary2DAxis"));
        turnAct = Act(InputActionType.Value, R("Primary2DAxis"));
        runAct = Act(InputActionType.Button, L("GripButton"));
        recenterAct = Act(InputActionType.Button, L("Primary2DAxisClick"));
        buttons.Add((Act(InputActionType.Button, R("TriggerButton")), KeyCode.Mouse0));      // фонарик, бросок, зажигалка, далее в диалоге
        buttons.Add((Act(InputActionType.Button, R("PrimaryButton"), R("MenuButton")), KeyCode.E)); // A (на Vive - меню) - взаимодействовать
        buttons.Add((Act(InputActionType.Button, R("SecondaryButton")), KeyCode.R));        // B - сменить батарейку
        buttons.Add((Act(InputActionType.Button, R("Primary2DAxisClick")), KeyCode.Space)); // клик правым стиком - прыжок
        buttons.Add((Act(InputActionType.Button, L("TriggerButton")), KeyCode.F));          // левый курок - фонарик
        buttons.Add((Act(InputActionType.Button, L("PrimaryButton")), KeyCode.G));          // X - выбросить предмет
        buttons.Add((Act(InputActionType.Button, L("SecondaryButton"), L("MenuButton")), KeyCode.Escape)); // Y / меню - пауза

        space = new GameObject("VR Tracking Space").transform;
        space.SetParent(transform, false);
        leftHand = MakeHand("Left Hand");
        rightHand = MakeHand("Right Hand");

        laser = rightHand.gameObject.AddComponent<LineRenderer>();
        laser.useWorldSpace = false;
        laser.positionCount = 2;
        laser.SetPositions(new[] { Vector3.zero, Vector3.forward * 3f });
        laser.widthMultiplier = 0.006f;
        laser.material = Mat(new Color(1f, 0.2f, 0.15f), unlit: true);
        laser.startColor = laser.endColor = new Color(1f, 0.2f, 0.15f);
        dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(dot.GetComponent<Collider>());
        dot.name = "Pointer Dot";
        dot.transform.SetParent(transform, false);
        dot.transform.localScale = Vector3.one * 0.012f;
        dot.GetComponent<Renderer>().sharedMaterial = Mat(new Color(1f, 0.85f, 0.7f), unlit: true);
        dot.SetActive(false);
        clickAct = Act(InputActionType.Button, R("TriggerButton"), L("TriggerButton"));

        // Камера главного меню рисует только слой UI: руки, луч и точку кладём туда же, иначе в меню их не видно
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            foreach (var t in new[] { leftHand, rightHand, dot.transform })
                foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = uiLayer;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupScene();

        Application.onBeforeRender += BeforeRender;
        StartCoroutine(RestoreAfterFrame());
        Debug.Log("[VR] Шлем найден, VR-режим включён");
#if UNITY_EDITOR
        if (VRSimulator.IsMockRuntime) gameObject.AddComponent<VRSimulator>();
#endif
    }

    void OnDestroy()
    {
        if (!Active) return;
        Application.onBeforeRender -= BeforeRender;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Active = false;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode) => SetupScene();

    // Всё, что привязано к сцене: игрок, панель интерфейса, предметы в руке
    void SetupScene()
    {
        // Предметы из прошлой сцены (их holder'ы жили в руке) уничтожаем
        foreach (var hand in new[] { leftHand, rightHand })
            foreach (Transform c in hand.Cast<Transform>().ToList())
                if (c.name != "Controller") Destroy(c.gameObject);
        converted.Clear();
        hovered = null; dragged = null;
        handsMoved = false;
        player = null; playerCam = null;

        uiAnchor = new GameObject("VR UI").transform; // живёт в сцене: вместе с ней уйдут и её меню

        var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length > 0 && players[0].cameraTransform != null)
        {
            player = players[0];
            playerCam = player.cameraTransform;
            eyeLocal = playerCam.localPosition;
        }

        var module = FindFirstObjectByType<InputSystemUIInputModule>();
        if (module != null)
        {
            // Меню в VR жмёт наш луч (UpdatePointer). Стандартный модуль UI от контроллеров отключаем,
            // иначе курок нажимал бы ещё и то, что под мышью на мониторе, или кнопку дважды
            module.xrTrackingOrigin = space;
            module.trackedDevicePosition = null;
            module.trackedDeviceOrientation = null;
            var click = module.leftClick != null ? module.leftClick.action : null;
            if (click != null)
                for (int i = 0; i < click.bindings.Count; i++)
                    if (click.bindings[i].path.Contains("XRController") || (Simulated && click.bindings[i].path.Contains("Mouse")))
                        click.ApplyBindingOverride(i, ""); // в симуляторе мышь - это курок, клик жмёт луч
        }
    }

    // Материал под HDRP: стандартный материал примитивов в билде HDRP не рисуется
    static Material Mat(Color c, bool unlit)
    {
        var sh = Shader.Find(unlit ? "HDRP/Unlit" : "HDRP/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.color = c;
        return m;
    }

    Transform MakeHand(string name)
    {
        var hand = new GameObject(name).transform;
        hand.SetParent(space, false);
        // Простая модель контроллера: рукоять + кольцо-подсветка, чтобы руку было видно в темноте
        var grip = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(grip.GetComponent<Collider>());
        grip.name = "Controller";
        grip.transform.SetParent(hand, false);
        grip.transform.localScale = new Vector3(0.04f, 0.06f, 0.04f);
        grip.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        grip.transform.localPosition = new Vector3(0f, -0.01f, -0.03f);
        grip.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.12f, 0.12f, 0.13f), unlit: false);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Glow";
        ring.transform.SetParent(grip.transform, false);
        ring.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
        ring.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        ring.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.6f, 0.08f, 0.05f), unlit: true);
        return hand;
    }

    // ---------- Ввод ----------

    void Update()
    {
        if (!Active) return;
        down.Clear();
        TurnDegrees = 0f; Scroll = 0f;

        ReadHead();
        if (!hasOrigin && (hmdPos != Vector3.zero || hmdRot != Quaternion.identity)) Recenter();

        Vector2 ls = Stick(moveAct), rs = Stick(turnAct);
        bool run = Held(runAct);
        // Запасной путь: старый XR API (если Input System не увидел контроллер)
        var l = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var r = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (ls == Vector2.zero) l.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out ls);
        if (rs == Vector2.zero) r.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out rs);
        if (!run) l.TryGetFeatureValue(XRCommonUsages.gripButton, out run);
        Move = ls.magnitude > 0.2f ? ls : Vector2.zero;
        Run = run;

        for (int i = 0; i < buttons.Count; i++) if (Pressed(buttons[i].act, "b" + i)) down.Add(buttons[i].key);
        // Старый API - только если Input System контроллеров не видит, иначе одно нажатие сработало бы дважды
        bool seen = inL.pos.controls.Count > 0 || inR.pos.controls.Count > 0;
        if (seen) l = r = default;
        Legacy(r, XRCommonUsages.triggerButton, KeyCode.Mouse0);
        Legacy(r, XRCommonUsages.primaryButton, KeyCode.E);
        Legacy(r, XRCommonUsages.secondaryButton, KeyCode.R);
        Legacy(r, XRCommonUsages.primary2DAxisClick, KeyCode.Space);
        Legacy(l, XRCommonUsages.triggerButton, KeyCode.F);
        Legacy(l, XRCommonUsages.primaryButton, KeyCode.G);
        Legacy(l, XRCommonUsages.secondaryButton, KeyCode.Escape);
        Legacy(l, XRCommonUsages.menuButton, KeyCode.Escape);

        // Правый стик: рывок влево/вправо поворачивает тело (и листает камеры на мониторе), вверх/вниз - слоты
        if (Flick("rx+", rs.x)) { TurnDegrees += SnapAngle; down.Add(KeyCode.RightArrow); }
        if (Flick("rx-", -rs.x)) { TurnDegrees -= SnapAngle; down.Add(KeyCode.LeftArrow); }
        if (Flick("ry+", rs.y)) Scroll = 1f;
        if (Flick("ry-", -rs.y)) Scroll = -1f;
        if (Pressed(recenterAct, "recenter")) Recenter();

        if (Time.unscaledTime >= nextCanvasScan) { nextCanvasScan = Time.unscaledTime + 0.1f; ConvertCanvases(); }
    }

    void Legacy(UnityEngine.XR.InputDevice dev, InputFeatureUsage<bool> usage, KeyCode key)
    {
        if (!dev.isValid) return;
        dev.TryGetFeatureValue(usage, out bool on);
        string id = dev.characteristics + usage.name;
        prev.TryGetValue(id, out bool was);
        prev[id] = on;
        if (on && !was) down.Add(key);
    }

    // Момент нажатия ловим сами по IsPressed: WasPressedThisFrame теряет нажатие, если ввод
    // обработан в другом цикле обновления (окно без фокуса, редактор)
    // Зажата ли кнопка: читаем сами кнопки всех контроллеров, привязанных к действию.
    // Состояние действия не годится: оно не видит изменений, записанных напрямую (симулятор шлема)
    static bool Held(InputAction a)
    {
        foreach (var c in a.controls)
            if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.isPressed) return true;
        return false;
    }

    // Стик с наибольшим отклонением среди контроллеров действия (так же напрямую, без состояния действия)
    static Vector2 Stick(InputAction a)
    {
        Vector2 best = Vector2.zero;
        foreach (var c in a.controls)
            if (c is UnityEngine.InputSystem.Controls.Vector2Control v && v.ReadValue().sqrMagnitude > best.sqrMagnitude) best = v.ReadValue();
        return best;
    }

    bool Pressed(InputAction a, string id)
    {
        bool on = Held(a);
        prev.TryGetValue(id, out bool was);
        prev[id] = on;
        return on && !was;
    }

    bool Flick(string id, float v)
    {
        prev.TryGetValue(id, out bool was);
        bool on = was ? v > 0.3f : v > 0.7f;
        prev[id] = on;
        return on && !was;
    }

    void ReadHead()
    {
        if (Simulated) { hmdPos = Vector3.zero; hmdRot = SimHeadRot; return; }
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (head.TryGetFeatureValue(XRCommonUsages.centerEyePosition, out var p) || head.TryGetFeatureValue(XRCommonUsages.devicePosition, out p)) hmdPos = p;
        if (head.TryGetFeatureValue(XRCommonUsages.centerEyeRotation, out var q) || head.TryGetFeatureValue(XRCommonUsages.deviceRotation, out q)) hmdRot = q;
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
        UpdatePointer(); // только здесь, раз за кадр: иначе одно нажатие курка сработало бы дважды
    }

    void BeforeRender()
    {
        if (!Active) return;
        ReadHead();
        ApplyPose();
    }

    void ApplyPose()
    {
        // Наклон головы допускаем, а дальше тело "догоняет": сдвигаем начало трекинга
        Vector3 off = hmdPos - originPos; off.y = 0f;
        if (off.magnitude > MaxLean) originPos += off * (1f - MaxLean / off.magnitude);

        Camera cam = ViewCamera();
        if (cam == null) return;
        // Руки и луч в 20-40 см от глаз: камера меню с отсечением 0,3 м их бы обрезала
        if (cam.nearClipPlane > 0.05f) cam.nearClipPlane = 0.05f;

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

        PoseHand(inL, XRNode.LeftHand, leftHand, -1f);
        PoseHand(inR, XRNode.RightHand, rightHand, 1f);
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

    void PoseHand(Hand input, XRNode node, Transform hand, float side)
    {
        // 1) Input System (OpenXR). Контроллеров одной руки может быть несколько (разные профили) -
        //    берём первый, у которого есть живая поза
        foreach (var c in input.pos.controls)
        {
            if (!(c is UnityEngine.InputSystem.Controls.Vector3Control pc)) continue;
            Vector3 p = pc.ReadValue();
            if (p == Vector3.zero) continue;
            var rc = c.device.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("deviceRotation");
            hand.localPosition = p;
            hand.localRotation = rc != null ? Normalize(rc.ReadValue()) : Quaternion.identity;
            return;
        }
        // 2) Старый XR API
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (dev.isValid && dev.TryGetFeatureValue(XRCommonUsages.devicePosition, out var lp) && dev.TryGetFeatureValue(XRCommonUsages.deviceRotation, out var lq) && lp != Vector3.zero)
        {
            hand.localPosition = lp; hand.localRotation = lq;
            return;
        }
        // 3) Контроллер не отслеживается: рука перед игроком у пояса, смотрит туда же, куда голова
        Quaternion headYaw = Quaternion.Euler(0f, hmdRot.eulerAngles.y, 0f);
        hand.localPosition = hmdPos + headYaw * new Vector3(0.18f * side, -0.25f, 0.4f);
        hand.localRotation = hmdRot;
    }

    static Quaternion Normalize(Quaternion q)
    {
        float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        return m > 1e-4f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
    }

    // Фонарик, зажигалка и бутылка переезжают из-под камеры в правую руку (один раз на сцену)
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
            foreach (Transform holder in playerCam.Cast<Transform>().ToList())
            {
                if (!holder.name.EndsWith("Holder") || holder.childCount == 0) continue;
                Transform item = holder.GetChild(0);
                Vector3 axis = Vector3.forward;
                if (ik != null) foreach (var it in ik.items) if (it.item == item) axis = it.longAxisLocal;
                holder.SetParent(rightHand, true); // true: сохранить мировой размер (у игрока свой масштаб)
                holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity;
                // Длинная ось предмета (стекло фонарика, верх зажигалки) - вдоль руки, сам предмет - в ладони
                item.rotation = Quaternion.FromToRotation(item.rotation * axis, rightHand.forward) * item.rotation;
                item.position = rightHand.position + rightHand.forward * 0.04f;
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
            if (c.gameObject.scene.name == null || c.gameObject.scene.name == "DontDestroyOnLoad") continue;
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
    }

    // ---------- Луч-указатель для меню ----------
    // Луч из правой руки; кнопка под ним подсвечивается, курок (любой руки) её нажимает,
    // слайдер (громкость, чувствительность) тянется, пока курок зажат.
    void UpdatePointer()
    {
        // Меню открыто, когда игре нужен курсор: главное меню, пауза, настройки, финальные экраны
        bool menu = Cursor.visible || PauseMenu.GameIsPaused || player == null || !player.gameObject.activeInHierarchy;
        laser.enabled = menu;
        dot.SetActive(false);
        if (!menu) { SetHover(null); dragged = null; return; }

        var ray = new Ray(rightHand.position, rightHand.forward);
        UnityEngine.UI.Selectable best = null;
        float bestDist = 5f;
        Vector3 bestHit = Vector3.zero;
        float wallDist = 5f; // если луч упёрся в панель без кнопок - точка на панели
        foreach (var c in converted)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            if (!new Plane(c.transform.forward, c.transform.position).Raycast(ray, out float d) || d > bestDist + 0.01f) continue;
            Vector3 hit = ray.GetPoint(d);
            var crt = (RectTransform)c.transform;
            if (crt.rect.Contains((Vector2)crt.InverseTransformPoint(hit))) wallDist = Mathf.Min(wallDist, d);
            foreach (var s in c.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if (!s.IsInteractable() || !s.isActiveAndEnabled) continue;
                var rt = (RectTransform)s.transform;
                if (!rt.rect.Contains((Vector2)rt.InverseTransformPoint(hit))) continue;
                // позже в иерархии = нарисовано поверх: при равной дальности берём его
                if (best == null || d <= bestDist + 0.001f) { best = s; bestDist = d; bestHit = hit; }
            }
        }

        float len = best != null ? bestDist : wallDist < 5f ? wallDist : 3f;
        laser.SetPosition(1, Vector3.forward * len);
        if (best != null || wallDist < 5f)
        {
            dot.SetActive(true);
            dot.transform.position = best != null ? bestHit : ray.GetPoint(wallDist);
        }

        if (dragged == null) SetHover(best);
        if (Pressed(clickAct, "click") && best != null)
        {
            if (best is UnityEngine.UI.Slider sl) dragged = sl;
            else UnityEngine.EventSystems.ExecuteEvents.Execute(best.gameObject, Ped(), UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        if (!Held(clickAct)) dragged = null;
        if (dragged != null)
        {
            var rt = (RectTransform)dragged.transform;
            if (new Plane(rt.forward, rt.position).Raycast(ray, out float d))
            {
                Vector2 local = rt.InverseTransformPoint(ray.GetPoint(d));
                bool horizontal = dragged.direction == UnityEngine.UI.Slider.Direction.LeftToRight || dragged.direction == UnityEngine.UI.Slider.Direction.RightToLeft;
                float t = horizontal ? Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x) : Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y);
                if (dragged.direction == UnityEngine.UI.Slider.Direction.RightToLeft || dragged.direction == UnityEngine.UI.Slider.Direction.TopToBottom) t = 1f - t;
                dragged.normalizedValue = t;
            }
        }
    }

    UnityEngine.EventSystems.PointerEventData Ped() =>
        new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };

    void SetHover(UnityEngine.UI.Selectable s)
    {
        if (s == hovered) return;
        if (hovered != null) UnityEngine.EventSystems.ExecuteEvents.Execute(hovered.gameObject, Ped(), UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
        hovered = s;
        if (s != null) UnityEngine.EventSystems.ExecuteEvents.Execute(s.gameObject, Ped(), UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
    }

    // Подсказки "Нажмите E" -> кнопки контроллера
    public static string Prompt(string text)
    {
        if (!Active || string.IsNullOrEmpty(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\wА-Яа-яЁё])(E|G|R|F|ЛКМ)(?![\wА-Яа-яЁё])", m =>
            m.Value == "E" ? "A" : m.Value == "G" ? "X" : m.Value == "R" ? "B" : "курок");
    }
}
