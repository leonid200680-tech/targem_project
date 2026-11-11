using System.Collections.Generic;
using UnityEngine;

// Ввод игрока через одну точку. В обычной игре - просто Input (клавиатура и мышь).
// Для записи прохождения (Tools > Trailer > Record Gameplay) вводом управляет бот: "жмёт" клавиши,
// "двигает" мышью и стиком - и игра реагирует на него ровно так же, как на живого игрока
// (шаги, покачивание камеры, руки, подсказки, скримеры). Пока бот активен, настоящий ввод игнорируется.
public static class BotInput
{
    public static bool Active;

    public static Vector2 Move;          // WASD: x - вбок, y - вперёд (-1..1)
    public static bool Run;             // Shift
    public static Vector2 Look;         // мышь: градусы за кадр (x - вправо, y - вверх)

    // Нажатия: клавиша -> кадр, в котором GetKeyDown вернёт true
    static readonly Dictionary<KeyCode, int> pressed = new Dictionary<KeyCode, int>();

    // Нажать клавишу (сработает в следующем кадре во всех скриптах)
    public static void Press(KeyCode key) => pressed[key] = Time.frameCount + 1;

    public static void Reset()
    {
        Move = Vector2.zero; Look = Vector2.zero; Run = false;
        pressed.Clear();
    }

    static bool BotDown(KeyCode key) => pressed.TryGetValue(key, out int f) && f == Time.frameCount;

    // В VR кнопки контроллеров приходят как те же клавиши (см. VRSupport)
    public static bool GetKeyDown(KeyCode key) => Active ? BotDown(key) : Input.GetKeyDown(key) || VRSupport.KeyDown(key);

    public static bool GetMouseButtonDown(int button) =>
        Active ? BotDown(KeyCode.Mouse0 + button) : Input.GetMouseButtonDown(button) || VRSupport.KeyDown(KeyCode.Mouse0 + button);

    public static bool GetKey(KeyCode key)
    {
        if (!Active) return Input.GetKey(key) || (key == KeyCode.LeftShift && VRSupport.Run);
        return (key == KeyCode.LeftShift && Run) || BotDown(key);
    }

    public static bool GetButtonDown(string button) => Active ? button == "Jump" && BotDown(KeyCode.Space)
        : Input.GetButtonDown(button) || (button == "Jump" && VRSupport.KeyDown(KeyCode.Space));

    public static float GetAxisRaw(string axis)
    {
        if (!Active) return Mathf.Clamp(Input.GetAxisRaw(axis) + VRAxis(axis), -1f, 1f);
        return axis == "Horizontal" ? Mathf.Round(Move.x) : axis == "Vertical" ? Mathf.Round(Move.y) : 0f;
    }

    public static float GetAxis(string axis)
    {
        float sens = Mathf.Max(0.01f, GameSettings.MouseSensitivity);
        if (!Active) return Input.GetAxis(axis) + VRAxis(axis) + (axis == "Mouse X" ? VRSupport.TurnDegrees / sens : 0f);
        switch (axis)
        {
            case "Mouse X": return Look.x / sens;
            case "Mouse Y": return Look.y / sens;
            case "Horizontal": return Move.x;
            case "Vertical": return Move.y;
        }
        return 0f;
    }

    static float VRAxis(string axis)
    {
        if (!VRSupport.Active) return 0f;
        switch (axis)
        {
            case "Horizontal": return VRSupport.Move.x;
            case "Vertical": return VRSupport.Move.y;
            case "Mouse ScrollWheel": return VRSupport.Scroll * 0.1f;
        }
        return 0f;
    }
}
