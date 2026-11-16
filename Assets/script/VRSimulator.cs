#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

// Симулятор VR-шлема для проверки без шлема (только в редакторе).
// Включается в меню Tools > VR > Симулятор шлема: OpenXR запускает свой Mock Runtime, игра думает,
// что шлем подключён, а этот скрипт подставляет два контроллера Quest и управляет ими мышью:
//  - ПКМ + мышь - повернуть голову (в игре наклон головы - просто мышью вверх/вниз);
//  - правая рука держится справа внизу и указывает туда, куда смотришь (луч в центр взгляда);
//  - ЛКМ - курок правой руки (нажать кнопку меню, фонарик, бросок);
//  - остальное как на клавиатуре: WASD, E, R, G, F, Esc, Shift, Space.
[DefaultExecutionOrder(-20000)] // раньше VRSupport: в этом кадре он уже видит наши контроллеры
public class VRSimulator : MonoBehaviour
{
    public static bool IsMockRuntime
    {
        get
        {
            string n = UnityEngine.XR.OpenXR.OpenXRRuntime.name ?? "";
            return n.IndexOf("Mock", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    InputDevice left, right;
    float yaw, pitch;
    bool help = true;

    void Start()
    {
        left = AddController("Sim Left Controller", CommonUsages.LeftHand);
        right = AddController("Sim Right Controller", CommonUsages.RightHand);
        Debug.Log("[VR] Симулятор шлема: ПКМ+мышь - голова, ЛКМ - курок, F1 - подсказка");
    }

    static InputDevice AddController(string name, InternedString usage)
    {
        var d = InputSystem.AddDevice("OculusTouchController", name);
        InputSystem.SetDeviceUsage(d, usage);
        return d;
    }

    void OnDestroy()
    {
        if (left != null) InputSystem.RemoveDevice(left);
        if (right != null) InputSystem.RemoveDevice(right);
        VRSupport.Simulated = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) help = !help;

        // Голова. В игре тело и так поворачивает мышь (как на ПК), поэтому там мышь вверх/вниз - наклон головы.
        // В меню игрока нет - поворачиваем голову целиком, пока зажата ПКМ.
        bool inGame = Cursor.lockState == CursorLockMode.Locked;
        float mx = Input.GetAxis("Mouse X") * 2f, my = Input.GetAxis("Mouse Y") * 2f;
        if (inGame) pitch = Mathf.Clamp(pitch - my, -80f, 80f);
        else if (Input.GetMouseButton(1)) { yaw += mx; pitch = Mathf.Clamp(pitch - my, -80f, 80f); }
        if (inGame) yaw = Mathf.MoveTowards(yaw, 0f, 180f * Time.unscaledDeltaTime); // тело уже повернулось - голова прямо
        Quaternion head = Quaternion.Euler(pitch, yaw, 0f);
        VRSupport.SimHeadRot = head;

        // Руки: справа и слева внизу; правая указывает в центр взгляда (туда же идёт луч меню)
        Quaternion headYaw = Quaternion.Euler(0f, yaw, 0f);
        Vector3 rPos = headYaw * new Vector3(0.18f, -0.22f, 0.32f);
        Vector3 lPos = headYaw * new Vector3(-0.18f, -0.22f, 0.32f);
        // В меню луч сходится в центр взгляда на панели (0,7 м), в игре - фонарик светит в точку в 3 м
        Vector3 target = head * Vector3.forward * (inGame ? 3f : 0.7f);
        Quaternion rRot = Quaternion.LookRotation(target - rPos);
        Quaternion lRot = Quaternion.LookRotation(target - lPos);

        // Значения пишем каждый кадр: редактор без фокуса может обнулить устройство
        Write(right, rPos, rRot, Input.GetMouseButton(0));
        Write(left, lPos, lRot, false);
    }

    static void Write(InputDevice d, Vector3 pos, Quaternion rot, bool trigger)
    {
        InputState.Change((Vector3Control)d["devicePosition"], pos);
        InputState.Change((QuaternionControl)d["deviceRotation"], rot);
        InputState.Change((ButtonControl)d["triggerPressed"], trigger ? 1f : 0f);
        InputState.Change((AxisControl)d["trigger"], trigger ? 1f : 0f);
        InputState.Change((ButtonControl)d["isTracked"], 1f);
    }

    void OnGUI()
    {
        if (!help) return;
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.Box(new Rect(10, 10, 330, 118),
            "Симулятор VR-шлема (F1 - скрыть)\n" +
            "Меню: ПКМ + мышь - голова, ЛКМ - нажать лучом\n" +
            "Игра: мышь - поворот и наклон головы\n" +
            "ЛКМ - курок правой руки (фонарик)\n" +
            "WASD, Shift, E, R, G, F, Esc - как на ПК\n" +
            "Выключить: Tools > VR > Симулятор шлема");
    }
}
#endif
