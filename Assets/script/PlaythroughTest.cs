#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

// Автопрохождение всей игры в Play Mode: звонок -> камеры -> предметы -> 3 этаж -> сектант -> книга ->
// камни -> свечи -> кукла -> концовка. Действия вызываются напрямую (как будто игрок нажал E),
// после каждого шага проверяется, что квест перешёл дальше. Отчёт - в PlaythroughTest.Report.
public class PlaythroughTest : MonoBehaviour
{
    public static StringBuilder Report = new StringBuilder();
    public static bool Done;

    const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run()
    {
        Report.Clear();
        Done = false;
        new GameObject("Playthrough Test").AddComponent<PlaythroughTest>();
    }

    void Log(string s) { Report.AppendLine($"[{Time.time,6:F1}] {s}"); }

    QuestManager.Step Step => QuestManager.Instance.CurrentStep;

    IEnumerator Expect(QuestManager.Step step, float timeout, string what)
    {
        for (float t = 0f; t < timeout && Step < step; t += Time.deltaTime) yield return null;
        Log((Step >= step ? "OK   " : "FAIL ") + what + " -> шаг " + Step);
    }

    // Пролистываем диалог, как игрок кликами
    IEnumerator SkipDialogue()
    {
        var dm = DialogueManager.Instance;
        var next = typeof(DialogueManager).GetMethod("DisplayNextLine", NP);
        for (int i = 0; i < 60 && DialogueManager.IsDialogueActive; i++)
        {
            next.Invoke(dm, null);
            yield return new WaitForSeconds(0.15f);
        }
    }

    void Teleport(PlayerController pc, Vector3 p)
    {
        var cc = pc.GetComponent<CharacterController>();
        cc.enabled = false;
        pc.transform.position = p;
        cc.enabled = true;
    }

    PickupItem FindPickup(ItemType type, Vector3 near)
    {
        PickupItem best = null;
        foreach (var p in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            if (p.item != null && p.item.type == type && (best == null ||
                Vector3.Distance(p.transform.position, near) < Vector3.Distance(best.transform.position, near))) best = p;
        return best;
    }

    IEnumerator Start()
    {
        var splash = GameObject.Find("StudioSplash"); if (splash != null) Destroy(splash);
        var menu = FindAnyObjectByType<MainMenuController>();
        menu.StartGame();
        Log("Начать игру");

        PlayerController pc = null;
        for (float t = 0f; t < 20f; t += Time.deltaTime)
        {
            pc = FindAnyObjectByType<PlayerController>();
            if (pc != null && pc.enabled) break;
            yield return null;
        }
        Log(pc != null && pc.enabled ? "OK   игрок получил управление (после заставки)" : "FAIL игрок так и не получил управление");
        var inv = pc.GetComponent<Inventory>();
        var qm = QuestManager.Instance;

        // 1. Телефон
        for (float t = 0f; t < 10f && !qm.callPhone.IsRinging(); t += Time.deltaTime) yield return null;
        Log(qm.callPhone.IsRinging() ? "OK   телефон звонит" : "FAIL телефон не зазвонил");
        qm.callPhone.AnswerPhone();
        yield return null;
        yield return SkipDialogue();
        yield return Expect(QuestManager.Step.WatchCameras, 3f, "ответил на звонок");

        // 2. Камеры
        var tv = qm.tv;
        tv.apparitionChance = 0f;
        tv.Interact();
        var show = typeof(InteractableTV).GetMethod("ShowCamera", NP);
        var noise = typeof(InteractableTV).GetField("noiseUntil", NP);
        yield return new WaitForSeconds(2f);
        for (int i = 0; i < tv.CameraCount; i++)
        {
            show.Invoke(tv, new object[] { i });
            noise.SetValue(tv, 0f);
            yield return new WaitForSeconds(2.5f);   // с запасом на случайные помехи
        }
        tv.Interact();
        yield return Expect(QuestManager.Step.TakeItems, 3f, "просмотрел все камеры (" + tv.CamerasSeen + "/" + tv.CameraCount + ")");
        Log(pc.enabled ? "OK   после телевизора управление вернулось" : "FAIL после телевизора управление не вернулось");

        // 3. Фонарик и зажигалка
        var door = FindAnyObjectByType<QuestDoorLock>();
        Log(door != null && door.GetComponent<InteractableDoor>().IsLocked ? "OK   дверь вагончика заперта до предметов" : "WARN дверь вагончика не заперта");
        foreach (var type in new[] { ItemType.Flashlight, ItemType.Lighter })
        {
            var p = FindPickup(type, pc.transform.position);
            if (p == null) { Log("FAIL нет предмета " + type); continue; }
            p.Pickup(inv);
        }
        yield return Expect(QuestManager.Step.ThirdFloor, 3f, "взял фонарик и зажигалку");
        yield return null;
        Log(door != null && !door.GetComponent<InteractableDoor>().IsLocked ? "OK   дверь вагончика открылась" : "FAIL дверь вагончика осталась запертой");

        // 4. Третий этаж
        Teleport(pc, new Vector3(-3.5f, 12.1f, -5f));
        yield return Expect(QuestManager.Step.SecondFloor, 3f, "поднялся на 3 этаж");

        // 5. Сектант
        var npc = qm.addict;
        Teleport(pc, npc.transform.position + Vector3.right * 1.5f);
        npc.Interact();
        yield return null;
        yield return SkipDialogue();
        yield return Expect(QuestManager.Step.BringBook, 3f, "поговорил с сектантом");

        // 6. Книга
        var book = FindPickup(ItemType.Book, pc.transform.position);
        if (book != null) book.Pickup(inv); else Log("FAIL нет книги");
        npc.Interact();
        yield return null;
        yield return SkipDialogue();
        yield return Expect(QuestManager.Step.PlaceStones, 3f, "отдал книгу");

        // 7. Камни (на втором камне появляется бабка)
        var pent = qm.pentagram;
        var ghost = FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include).realGhost;
        int need = pent.stonesOnAltar.Length, got = 0;
        foreach (var s in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
        {
            if (got >= need || s.item == null || s.item.type != ItemType.Stone) continue;
            int before = inv.Count(s.item);
            s.Pickup(inv);
            if (inv.Count(s.item) > before) got++; else Log("FAIL камень не влез в инвентарь");
            if (got == 1)
            {
                // Выброс и повторный подбор не должны второй раз запускать события камня
                int collected = PickupItem.stonesCollected;
                bool dropped = PickupItem.Drop(s.item, pc.transform.position);
                inv.Remove(s.item, 1);
                bool back = s.gameObject.activeSelf;
                s.Pickup(inv);
                Log(dropped && back && !s.gameObject.activeSelf && inv.Count(s.item) == before + 1 && PickupItem.stonesCollected == collected
                    ? "OK   камень выброшен и подобран снова без повторного события"
                    : "FAIL выброс камня: stonesCollected " + PickupItem.stonesCollected + ", было " + collected);
            }
            yield return new WaitForSeconds(0.2f);
            if (got == 2)
            {
                yield return new WaitForSeconds(4.5f);   // тишина, затем скример
                Log(ghost.activeInHierarchy ? "OK   после второго камня появилась бабка" : "FAIL бабка не появилась после второго камня");
                // Чтобы тест не прервался поимкой, уводим её подальше
                ghost.GetComponent<GhostChase>().SendFarFrom(pc.transform.position);
                ghost.GetComponent<GhostChase>().enabled = false;
                ghost.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped = true;
            }
        }
        Log($"собрал камней: {got} из нужных {need}, в инвентаре {inv.Count(pent.stoneItem)}");
        Teleport(pc, pent.transform.position + Vector3.right * 1.5f);
        for (int i = 0; i < need; i++) pent.Interact(inv);
        yield return Expect(QuestManager.Step.LightCandles, 3f, "разложил камни");

        // 8. Свечи
        foreach (var c in FindObjectsByType<CandleInteract>(FindObjectsSortMode.None)) c.TryLight(inv);
        yield return Expect(QuestManager.Step.PlaceDoll, 3f, "зажёг свечи");

        // 9. Кукла и концовка
        var doll = FindPickup(ItemType.Doll, pc.transform.position);
        if (doll != null) doll.Pickup(inv); else Log("FAIL нет куклы");
        pent.Interact(inv);
        var screen = FindAnyObjectByType<GameCompleteScreen>(FindObjectsInactive.Include);
        var shown = typeof(GameCompleteScreen).GetField("shown", NP);
        for (float t = 0f; t < 40f && !(bool)shown.GetValue(screen); t += Time.deltaTime) yield return null;
        Log((bool)shown.GetValue(screen) ? "OK   концовка доиграла, экран «" + screen.title + "» / " + screen.endingLabel : "FAIL экран конца не появился");
        yield return Expect(QuestManager.Step.Done, 1f, "квест завершён");
        Done = true;
    }
}

#endif
