using UnityEngine;
using UnityEngine.AI;

// Пауза не спасает: за полупрозрачным меню, пока игра стоит, вдалеке проходит бабка.
// Останавливается, смотрит на игрока и идёт дальше. Закрыл меню - никого. Безвредно, только копия модели.
public class PauseGranny : MonoBehaviour
{
    const int MaxPerGame = 2;
    const float WalkSpeed = 1.1f;

    public static int Shown;
    static PauseGranny current;

    GameObject body;
    Vector3 from, to;
    float travelled, length, hold = 1.4f;
    bool stopped;
    UnityEngine.UI.Image panel;
    Color panelColor;

    public static void TryShow()
    {
        if (Shown >= MaxPerGame || PickupItem.stonesCollected < 2 || Random.value > 0.6f) return;
        if (HideableCloset.IsPlayerHidden || GrannyJumpscare.IsPlaying || DialogueManager.IsDialogueActive || EvacuationPlan.IsOpen) return;
        var tv = FindAnyObjectByType<InteractableTV>();
        if (tv != null && tv.IsViewing) return;

        var cam = Camera.main;
        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        if (cam == null || hm == null || hm.realGhost == null) return;
        if (hm.realGhost.activeInHierarchy && Vector3.Distance(hm.realGhost.transform.position, cam.transform.position) < 12f) return;
        if (!FindPath(cam.transform, out Vector3 a, out Vector3 b)) return;

        var go = new GameObject("Pause Granny");
        current = go.AddComponent<PauseGranny>();
        current.Begin(hm.realGhost, a, b);
        Shown++;
    }

    public static void Hide()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
    }

    // Поперёк взгляда на расстоянии, а если коридор узкий - вдоль него к игроку
    static bool FindPath(Transform cam, out Vector3 a, out Vector3 b)
    {
        a = b = default;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        foreach (float d in new[] { 8f, 6.5f, 10f })
            foreach (float half in new[] { 2.5f, 1.5f })
                if (Usable(cam, cam.position + fwd * d - side * half, out a) && Usable(cam, cam.position + fwd * d + side * half, out b)) return true;
        return Usable(cam, cam.position + fwd * 10f, out a) && Usable(cam, cam.position + fwd * 5.5f, out b);
    }

    static bool Usable(Transform cam, Vector3 p, out Vector3 point)
    {
        point = default;
        float floorY = cam.position.y - 1.7f;
        if (!NavMesh.SamplePosition(new Vector3(p.x, floorY, p.z), out var hit, 1.2f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floorY) > 0.6f) return false;
        if (Physics.Linecast(cam.position, hit.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) return false;
        point = hit.position;
        return true;
    }

    void Begin(GameObject source, Vector3 a, Vector3 b)
    {
        from = a; to = b;
        length = Vector3.Distance(a, b);
        body = GrannyJumpscare.MakeCopy(source);
        body.name = "Pause Granny Body";
        body.transform.SetPositionAndRotation(a, Quaternion.LookRotation(b - a));
        body.SetActive(true);
        var anim = body.GetComponent<Animator>();
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;   // игра на паузе, а она идёт
        anim.speed = 1f;
        anim.Play("Blend Tree", 0, 0f);
        anim.SetFloat("Speed", WalkSpeed);

        // Фон меню почти чёрный: приоткрываем его и чуть подсвечиваем её, иначе не разглядеть
        var pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        panel = pause != null ? pause.pauseMenuPanel.GetComponent<UnityEngine.UI.Image>() : null;
        if (panel != null)
        {
            panelColor = panel.color;
            panel.color = new Color(panelColor.r, panelColor.g, panelColor.b, Mathf.Min(panelColor.a, 0.55f));
        }
        var lightGo = new GameObject("Pause Granny Light");
        lightGo.transform.SetParent(body.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.1f, 0.7f);
        var glow = lightGo.AddComponent<Light>();
        lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        glow.type = LightType.Point;
        glow.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        glow.intensity = 350f;
        glow.range = 5f;
        glow.color = new Color(1f, 0.85f, 0.75f);
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        if (body == null) return;
        var anim = body.GetComponent<Animator>();
        float dt = Time.unscaledDeltaTime;

        // На середине пути останавливается и смотрит на игрока
        if (!stopped && travelled >= length * 0.5f) stopped = true;
        if (stopped && hold > 0f)
        {
            hold -= dt;
            anim.SetFloat("Speed", 0f);
            Vector3 look = Camera.main != null ? Camera.main.transform.position - body.transform.position : body.transform.forward;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, Quaternion.LookRotation(look), 260f * dt);
            return;
        }

        anim.SetFloat("Speed", WalkSpeed);
        body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, Quaternion.LookRotation(to - from), 260f * dt);
        travelled = Mathf.Min(length, travelled + WalkSpeed * dt);
        body.transform.position = Vector3.Lerp(from, to, travelled / length);
        if (travelled >= length) anim.SetFloat("Speed", 0f);
    }

    void OnDestroy()
    {
        if (body != null) Destroy(body);
        if (panel != null) panel.color = panelColor;
    }
}
