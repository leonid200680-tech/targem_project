using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Редкие пугалки, которые ломают привычные правила, но не наказывают игрока:
//  - ложная погоня: за спиной рык бабки и быстрые шаги, обернулся - никого, и дальше ничего не происходит;
//  - фонарик гаснет посреди заряда, в темноте рядом шаркают шаги и дыхание, свет возвращается - пусто;
//  - номер кабинета подменяется: пока игрок не видит, на соседней двери появляется тот же номер,
//    как только он отвернётся - табличка снова правильная.
// Каждая - 1-2 раза за игру, не чаще одной пугалки в пару минут и только внутри здания.
public class ScareDirector : MonoBehaviour
{
    public Bounds buildingZone = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));
    [Tooltip("Не чаще одной пугалки за столько секунд")]
    public float minGap = 100f;

    [Header("Ложная погоня")]
    public int falseChases = 2;
    public float falseChaseAfter = 200f;     // сек с начала игры

    [Header("Фонарик гаснет")]
    public int blackouts = 2;
    public float blackoutAfter = 140f;

    [Header("Номер кабинета")]
    public float numberSwapAfter = 160f;

    private PlayerController pc;
    private Flashlight flashlight;
    private GameObject granny;
    private AudioClip growl;
    private AudioClip[] steps;
    private AudioClip breath;
    private float lastScare = -999f;
    private float nextRoll;
    private bool busy;
    private int chasesDone, blackoutsDone;

    // Таблички с номерами: номер -> тексты (у каждого входа по табличке с двух сторон)
    private readonly Dictionary<int, List<TMP_Text>> plates = new Dictionary<int, List<TMP_Text>>();
    private bool swapDone;
    private int swapped = -1;
    private string swappedOriginal;
    private bool swapSeen;
    private float swapStart;

    void Start()
    {
        Rat.StareUsed = false;
        HideableCloset.PeekUsed = false;
        steps = new AudioClip[4];
        for (int i = 0; i < steps.Length; i++) steps[i] = GrannySounds.MakeStep(10 + i);
        breath = GrannySounds.MakeBreath();

        var hm = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        if (hm != null) granny = hm.realGhost;
        var gs = granny != null ? granny.GetComponent<GrannySounds>() : null;
        growl = gs != null && gs.growl != null ? gs.growl : GrannySounds.MakeMoan(7);

        var root = GameObject.Find("Room Numbers");
        if (root != null)
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (int.TryParse(t.text, out int n))
                {
                    if (!plates.TryGetValue(n, out var list)) plates[n] = list = new List<TMP_Text>();
                    list.Add(t);
                }
        nextRoll = Time.time + 10f;
    }

    float PlayTime => QuestManager.Instance != null ? QuestManager.Instance.PlayTime : 0f;

    // Можно пугать: игрок в здании, сам управляет, не в шкафу, не в диалоге, ничего другого не идёт
    bool Calm()
    {
        if (pc == null) { pc = FindAnyObjectByType<PlayerController>(); if (pc != null) flashlight = pc.GetComponent<Flashlight>(); }
        if (pc == null || !pc.enabled || busy) return false;
        if (HideableCloset.IsPlayerHidden || DialogueManager.IsDialogueActive || PauseMenu.GameIsPaused || GrannyJumpscare.IsPlaying) return false;
        if (!buildingZone.Contains(pc.transform.position)) return false;
        return Time.time - lastScare > minGap;
    }

    bool GrannyFar(float dist) =>
        granny == null || !granny.activeInHierarchy || Vector3.Distance(granny.transform.position, pc.transform.position) > dist;

    void Update()
    {
        UpdateNumberSwap();

        if (Time.time < nextRoll) return;
        nextRoll = Time.time + Random.Range(12f, 20f);
        if (!Calm()) return;

        var step = QuestManager.Instance != null ? QuestManager.Instance.CurrentStep : QuestManager.Step.AnswerPhone;

        if (blackoutsDone < blackouts && PlayTime > blackoutAfter && flashlight != null && flashlight.IsOn
            && flashlight.currentCharge > flashlight.maxCharge * 0.3f && GrannyFar(12f) && Random.value < 0.25f)
        {
            blackoutsDone++;
            StartCoroutine(Blackout());
        }
        else if (chasesDone < falseChases && PlayTime > falseChaseAfter && step >= QuestManager.Step.BringBook
            && GrannyFar(25f) && Random.value < 0.2f)
        {
            chasesDone++;
            StartCoroutine(FalseChase());
        }
    }

    AudioSource OneShotAt(Vector3 pos, float maxDist)
    {
        var s = new GameObject("Scare Sound").AddComponent<AudioSource>();
        s.transform.position = pos;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = maxDist;
        s.dopplerLevel = 0f;
        return s;
    }

    // Точка за спиной игрока на высоте пояса
    Vector3 Behind(float dist, float sideJitter)
    {
        Transform t = pc.transform;
        Vector3 back = -t.forward; back.y = 0f; back.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, back);
        return t.position + Vector3.up * 1f + back * dist + side * Random.Range(-sideJitter, sideJitter);
    }

    // ---------- Ложная погоня: только звук ----------
    IEnumerator FalseChase()
    {
        busy = true;
        lastScare = Time.time;

        var src = OneShotAt(Behind(11f, 2f), 30f);
        src.pitch = 0.9f;
        src.PlayOneShot(growl, 1f);
        yield return new WaitForSeconds(0.7f);

        // Быстрые шаги нагоняют за спиной: с 11 м до 2 м за 2 секунды
        float run = 2f, t = 0f, nextStep = 0f;
        int i = 0;
        while (t < run)
        {
            t += Time.deltaTime;
            src.transform.position = Behind(Mathf.Lerp(11f, 2f, t / run), 0f);
            if (t >= nextStep)
            {
                src.pitch = Random.Range(1.1f, 1.3f);
                src.PlayOneShot(steps[i++ % steps.Length], 1f);
                nextStep += 0.23f;
            }
            yield return null;
        }

        // И обрывается. Тишина.
        src.Stop();
        Destroy(src.gameObject, 0.1f);
        yield return new WaitForSeconds(3f);
        busy = false;
    }

    // ---------- Фонарик гаснет, рядом кто-то шаркает ----------
    IEnumerator Blackout()
    {
        busy = true;
        lastScare = Time.time;
        var spot = flashlight.spotLight;

        // Мигает и гаснет
        for (float t = 0f; t < 0.7f; t += Random.Range(0.04f, 0.12f))
        {
            spot.enabled = Random.value < 0.5f;
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }
        spot.enabled = false;

        yield return new WaitForSeconds(0.5f);
        var src = OneShotAt(Behind(2.3f, 1.2f), 12f);
        for (int i = 0; i < 3; i++)
        {
            src.pitch = Random.Range(0.85f, 0.95f);
            src.PlayOneShot(steps[i], 0.9f);
            src.transform.position += (pc.transform.position - src.transform.position).normalized * 0.25f;
            yield return new WaitForSeconds(0.6f);
        }
        src.pitch = 1f;
        src.clip = breath;
        src.time = 0.5f;
        src.volume = 0.8f;
        src.Play();
        yield return new WaitForSeconds(1.4f);
        src.Stop();
        Destroy(src.gameObject);

        // Свет возвращается - рядом никого
        for (float t = 0f; t < 0.4f; t += 0.08f)
        {
            spot.enabled = flashlight.IsOn && Random.value < 0.6f;
            yield return new WaitForSeconds(0.08f);
        }
        spot.enabled = flashlight.IsOn;
        busy = false;
    }

    // ---------- Номер кабинета ----------
    void UpdateNumberSwap()
    {
        if (swapDone || plates.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;

        if (swapped >= 0)
        {
            var list = plates[swapped];
            bool visible = false, near = false;
            foreach (var p in list)
            {
                if (!Visible(cam, p.transform)) continue;
                visible = true;
                if (Vector3.Distance(cam.transform.position, p.transform.position) < 4.5f) near = true;
            }
            if (near) swapSeen = true;
            // Вернуть, когда игрок отвернулся (или так и не дошёл)
            if (!visible && (swapSeen || Time.time - swapStart > 90f))
            {
                foreach (var p in list) p.text = swappedOriginal;
                swapped = -1;
                swapDone = true;
                busy = false;
            }
            return;
        }

        if (Time.frameCount % 15 != 0 || !Calm() || PlayTime < numberSwapAfter) return;
        var step = QuestManager.Instance != null ? QuestManager.Instance.CurrentStep : QuestManager.Step.AnswerPhone;
        if (step < QuestManager.Step.BringBook) return;

        // Игрок только что прошёл мимо таблички N; соседняя N+1 сейчас не видна - она станет тоже N
        foreach (var kv in plates)
        {
            int n = kv.Key;
            if (!plates.TryGetValue(n + 1, out var next)) continue;
            bool passing = false;
            foreach (var p in kv.Value)
                if (Vector3.Distance(p.transform.position, pc.transform.position + Vector3.up * 1.5f) < 2.2f) passing = true;
            if (!passing) continue;

            bool hidden = true;
            foreach (var p in next)
                if (Visible(cam, p.transform) || Vector3.Distance(p.transform.position, cam.transform.position) < 2.5f) hidden = false;
            if (!hidden) continue;

            swapped = n + 1;
            swappedOriginal = next[0].text;
            foreach (var p in next) p.text = n.ToString();
            swapSeen = false;
            swapStart = Time.time;
            lastScare = Time.time;
            busy = true;
            return;
        }
    }

    static bool Visible(Camera cam, Transform t)
    {
        Vector3 v = cam.WorldToViewportPoint(t.position);
        if (v.z <= 0f || v.z > 25f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
        Vector3 from = cam.transform.position;
        Vector3 to = t.position;
        Vector3 dir = to - from;
        // табличка висит на стене, а коллайдеры стен толще самих стен - последние полметра не проверяем
        return !Physics.Raycast(from, dir.normalized, dir.magnitude - 0.5f, ~0, QueryTriggerInteraction.Ignore);
    }
}
