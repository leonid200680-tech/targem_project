#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Запись полного прохождения "без комментариев", как в роликах по хоррорам: игру ведёт бот через BotInput
// (ходит, светит фонариком, осматривается, жмёт E, листает камеры, прячется в шкаф), всё остальное - сама игра:
// звуки, HUD, подсказки, скримеры, концовка. Запуск: Tools > Trailer > Record Gameplay (или Preview Gameplay без записи).
[DefaultExecutionOrder(-100)]
public class GameplayDirector : MonoBehaviour
{
    public bool Finished { get; private set; }
    public System.Text.StringBuilder Log = new System.Text.StringBuilder();

    PlayerController pc;
    Inventory inv;
    QuestManager qm;
    Transform cam;
    CanvasGroup cardGroup;
    TextMeshProUGUI cardTitle, cardSub;
    Image cardBack;

    // Куда смотреть/идти: бот поворачивает "мышью" с ограниченной скоростью и лёгким дрожанием руки
    float yawSpeed = 150f, pitchSpeed = 90f;
    Vector3? lookTarget;
    float lookYaw, lookPitch;          // цель, если lookTarget не задан
    bool freeLook;                     // смотреть по углам lookYaw/lookPitch
    float noiseT;

    public TMP_FontAsset titleFont, textFont;
    public Material titleMaterial, textMaterial;

    public static int SkipTo;   // отладка: 1 - начать со второго этажа

    public void Begin() => StartCoroutine(Run());

    void Note(string s) { Log.AppendLine($"[{Time.time,6:F1}] {s}"); Debug.Log("[Gameplay] " + s); }

    // ================= Сценарий =================

    IEnumerator Run()
    {
        BotInput.Reset();
        BuildCard();
        yield return Card("КОНТОРА", "Полное прохождение  ·  Без комментариев", 4.5f);

        // Главное меню: пара секунд, потом "Начать игру"
        var splash = GameObject.Find("StudioSplash"); if (splash != null) Destroy(splash);
        var menu = FindAnyObjectByType<MainMenuController>();
        yield return FadeCard(1f, 0f, 1.2f);
        yield return new WaitForSeconds(3f);
        menu.StartGame();
        Note("Начать игру");

        for (float t = 0f; t < 30f; t += Time.deltaTime)
        {
            pc = FindAnyObjectByType<PlayerController>();
            if (pc != null && pc.enabled) break;
            yield return null;
        }
        inv = pc.GetComponent<Inventory>();
        qm = QuestManager.Instance;
        cam = pc.cameraTransform;
        BotInput.Active = true;
        HoldLook();

        var pent = qm.pentagram;
        var npc = qm.addict;
        if (SkipTo >= 1)
        {
            // Отладка маршрута: сразу ко второму этажу (в записи не используется)
            for (float t = 0f; t < 10f && !qm.callPhone.IsRinging(); t += Time.deltaTime) yield return null;
            qm.callPhone.AnswerPhone();
            yield return null;
            var dmNext = typeof(DialogueManager).GetMethod("DisplayNextLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 60 && DialogueManager.IsDialogueActive; i++) { dmNext.Invoke(DialogueManager.Instance, null); yield return new WaitForSeconds(0.1f); }
            FindPickup(ItemType.Flashlight).Pickup(inv);
            FindPickup(ItemType.Lighter).Pickup(inv);
            qm.SetQuestStep((int)QuestManager.Step.SecondFloor);
            Teleport(new Vector3(-3.5f, 12.1f, -5f));
            yield return new WaitForSeconds(0.5f);
            yield return SelectItem(ItemType.Flashlight);
            wantLight = true;
            HoldLook();
            if (SkipTo >= 2)
            {
                npc.currentState = NPCDialogueController.NPCState.BookReturned;
                qm.SetQuestStep((int)QuestManager.Step.PlaceStones);
                Teleport(new Vector3(2f, 7.95f, -3f));
                yield return new WaitForSeconds(0.5f);
                goto Stones;
            }
            goto SecondFloor;
        }

        // ---------- Вагончик ----------
        yield return Idle(1.5f);
        yield return LookAround(3f, 50f);
        for (float t = 0f; t < 10f && !qm.callPhone.IsRinging(); t += Time.deltaTime) yield return null;
        yield return Idle(1.2f);                                  // прислушивается к вибрации
        yield return Use(qm.callPhone, qm.callPhone.transform.position);
        yield return Dialogue();
        Note("телефон: " + qm.CurrentStep);

        yield return Idle(0.8f);
        yield return Use(qm.tv, qm.tv.transform.position + Vector3.up * 0.15f);
        yield return WatchCameras();
        Note("камеры: " + qm.CurrentStep);

        // Фонарик, зажигалка, доска с подсказками
        yield return Idle(0.6f);
        yield return Use(FindPickup(ItemType.Flashlight), null);
        yield return Use(FindPickup(ItemType.Lighter), null);
        var board = GameObject.Find("Hint Board");
        if (board != null)
        {
            yield return LookAtFor(board.transform.position + Vector3.up * 0.1f, 1.5f);
            yield return Idle(3.5f);
        }
        Note("предметы: " + qm.CurrentStep);

        // На улицу: к двери вагончика и наружу
        var door = FindAnyObjectByType<QuestDoorLock>().GetComponent<InteractableDoor>();
        Vector3 doorPos = door.GetComponent<Renderer>().bounds.center;
        yield return Go(new Vector3(doorPos.x, pc.transform.position.y, doorPos.z - 0.8f));
        if (!door.IsOpen) yield return Use(door, doorPos);
        yield return Idle(0.6f);
        yield return Go(new Vector3(doorPos.x, pc.transform.position.y, doorPos.z + 1.6f));

        // Ночь, генератор, качели. Включаем фонарик и идём к конторе
        yield return SelectItem(ItemType.Flashlight);
        yield return Idle(0.4f);
        wantLight = true;
        yield return LookAround(4f, 70f);
        yield return WalkTo(new Vector3(-6.4f, 3.8f, -9.5f));    // к лестнице первого этажа через вход
        Note("в здании: " + qm.CurrentStep);

        // ---------- Третий этаж ----------
        yield return WalkTo(new Vector3(-3.5f, 12.1f, -5f));
        yield return LookAround(3f, 60f);
        yield return WalkTo(pent.transform.position + new Vector3(-2.2f, 0f, 1.6f));
        yield return LookAtFor(pent.transform.position, 1.2f);
        yield return Idle(2.5f);
        Note("3 этаж: " + qm.CurrentStep);

        // ---------- Второй этаж: сектант ----------
        SecondFloor:
        yield return WalkTo(new Vector3(-9.31f, 7.9f, 0.4f));    // по пути - батарейка
        yield return Use(FindPickup(ItemType.Battery, new Vector3(-9.31f, 7.88f, 1.19f)), null);
        yield return EnterCultistRoom();
        yield return Use(npc, npc.transform.position + Vector3.up * 1.2f);
        yield return Dialogue();
        Note("сектант: " + qm.CurrentStep);
        yield return LeaveCultistRoom();

        // ---------- Книга ----------
        var book = FindPickup(ItemType.Book);
        yield return WalkTo(book.transform.position, stopDist: 1.3f);
        yield return Use(book, null);
        yield return LookAround(2f, 40f);
        yield return EnterCultistRoom();
        yield return Use(npc, npc.transform.position + Vector3.up * 1.2f);
        yield return Dialogue();
        Note("книга: " + qm.CurrentStep);
        yield return LeaveCultistRoom();

        // ---------- Камни. На втором - скример и бабка: бежим в шкаф ----------
        Stones:
        yield return Stone(new Vector3(7.95f, 7.85f, -4.77f));
        yield return Stone(new Vector3(-24.93f, 7.84f, -11.93f));
        yield return HideFromGranny();
        yield return Stone(new Vector3(-17.83f, 4.72f, -5.16f));
        yield return Stone(new Vector3(-25.21f, 11.99f, -9.76f));
        yield return Stone(new Vector3(-27.42f, 12.80f, -13.39f));
        Note("камни: " + inv.Count(pent.stoneItem));

        // ---------- Ритуал: камни, свечи, кукла ----------
        yield return WalkTo(pent.transform.position + new Vector3(-1.8f, 0f, 1.2f));
        for (int i = 0; i < 6 && qm.CurrentStep == QuestManager.Step.PlaceStones; i++)
        {
            yield return Use(pent, pent.transform.position, keepPosition: true);
            yield return Idle(0.5f);
        }
        Note("разложил: " + qm.CurrentStep);
        wantLight = false;
        yield return SelectItem(ItemType.Lighter);
        BotInput.Press(KeyCode.Mouse0);                          // зажигалку - в руку и зажечь
        yield return Idle(0.6f);
        foreach (var c in Ordered(FindObjectsByType<CandleInteract>(FindObjectsSortMode.None)))
        {
            if (c.isLit) continue;
            yield return Use(c, null, nearDist: 1.3f);
            yield return Idle(0.4f);
        }
        Note("свечи: " + qm.CurrentStep);
        BotInput.Press(KeyCode.Mouse0);                          // потушить зажигалку
        yield return Idle(0.4f);
        yield return SelectItem(ItemType.Flashlight);
        wantLight = true;

        var doll = FindPickup(ItemType.Doll);
        if (doll != null)
        {
            yield return EnterCultistRoom();
            yield return Use(doll, null);
            yield return LeaveCultistRoom();
            yield return WalkTo(pent.transform.position + new Vector3(-1.8f, 0f, 1.2f));
            yield return SelectItem(ItemType.Doll);
            yield return Use(pent, pent.transform.position, keepPosition: true);
        }
        Note("кукла: " + qm.CurrentStep);

        // ---------- Концовка ----------
        BotInput.Reset();
        var screen = FindAnyObjectByType<GameCompleteScreen>(FindObjectsInactive.Include);
        var shown = typeof(GameCompleteScreen).GetField("shown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        for (float t = 0f; t < 60f && !(bool)shown.GetValue(screen); t += Time.deltaTime) yield return null;
        Note("концовка показана");
        yield return new WaitForSeconds(9f);
        yield return FadeCard(0f, 1f, 2.5f, onlyBlack: true);
        yield return new WaitForSeconds(1f);
        BotInput.Active = false;
        Finished = true;
    }

    IEnumerator WatchCameras()
    {
        yield return new WaitForSeconds(2.6f);                   // "нет сигнала" при включении
        for (int i = 0; i < qm.tv.CameraCount + 1; i++)
        {
            yield return new WaitForSeconds(i == 2 ? 5f : 3.2f);   // на камере с двойником задерживаемся
            BotInput.Press(KeyCode.D);
        }
        yield return new WaitForSeconds(3f);   // дольше блокировки выхода на время пугалки
        BotInput.Press(KeyCode.E);
        yield return new WaitForSeconds(1f);
        HoldLook();
    }

    IEnumerator Stone(Vector3 at)
    {
        PickupItem best = null;
        foreach (var p in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            if (p.item != null && p.item.type == ItemType.Stone && p.gameObject.activeInHierarchy && !p.transform.IsChildOf(qm.pentagram.transform)
                && (best == null || Vector3.Distance(p.transform.position, at) < Vector3.Distance(best.transform.position, at))) best = p;
        if (best == null) { Note("камень не найден у " + at); yield break; }
        yield return WalkTo(best.transform.position, stopDist: 1.3f);
        yield return Use(best, null);
        yield return Idle(0.5f);
    }

    // После второго камня: скример в лицо, бабка впереди - разворот, бег, ближайший шкаф
    IEnumerator HideFromGranny()
    {
        yield return new WaitForSeconds(0.3f);
        while (GrannyJumpscare.IsPlaying) yield return null;
        yield return new WaitForSeconds(0.4f);
        var hm = FindAnyObjectByType<HallucinationManager>();
        var granny = hm != null ? hm.realGhost : null;
        if (granny == null || !granny.activeInHierarchy) yield break;

        yield return LookAtFor(granny.transform.position + Vector3.up * 1.5f, 0.9f);
        yield return Idle(0.5f);

        HideableCloset best = null;
        foreach (var c in FindObjectsByType<HideableCloset>(FindObjectsSortMode.None))
        {
            if (Mathf.Abs(c.transform.position.y - pc.transform.position.y) > 1.5f) continue;
            float d = Vector3.Distance(c.transform.position, pc.transform.position) - 0.4f * Vector3.Distance(c.transform.position, granny.transform.position);
            if (best == null || d < Vector3.Distance(best.transform.position, pc.transform.position) - 0.4f * Vector3.Distance(best.transform.position, granny.transform.position)) best = c;
        }
        if (best == null) yield break;
        Note("бежит к шкафу " + best.name);
        yield return WalkTo(best.exitPoint != null ? best.exitPoint.position : best.transform.position, run: true, stopDist: 0.5f);
        yield return Use(best, best.transform.position + Vector3.up * 1.2f, keepPosition: true);

        // Сидим, пока бабка не уйдёт (она может подойти к решётке)
        var chase = granny.GetComponent<GhostChase>();
        yield return new WaitForSeconds(2f);
        for (float t = 0f; t < 25f; t += Time.deltaTime)
        {
            BotInput.Look = new Vector2(Mathf.Sin(Time.time * 0.6f) * 0.15f, Mathf.Sin(Time.time * 0.45f) * 0.08f);
            if (t > 6f && !chase.Scripted && Vector3.Distance(granny.transform.position, pc.transform.position) > 12f) break;
            yield return null;
        }
        BotInput.Look = Vector2.zero;
        yield return new WaitForSeconds(1.5f);
        BotInput.Press(KeyCode.E);
        yield return new WaitForSeconds(1.2f);
        HoldLook();
        yield return LookAround(2.5f, 70f);
    }

    // ================= Действия =================

    // Подойти, навести взгляд и нажать E. Если луч не попал (что-то загораживает) - действие вызывается напрямую.
    IEnumerator Use(Component target, Vector3? aim, bool keepPosition = false, float nearDist = 1.6f)
    {
        if (target == null) yield break;
        var col = target.GetComponentInChildren<Collider>();
        Vector3 point = aim ?? (col != null ? col.bounds.center : target.transform.position);

        if (!keepPosition && Flat(point - pc.transform.position).magnitude > nearDist)
            yield return WalkTo(point, stopDist: nearDist * 0.8f);

        yield return LookAtFor(point, 0.7f);
        bool hit = false;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit h, 3f) && h.collider.transform.IsChildOf(target.transform.root) &&
                (h.collider.transform == target.transform || h.collider.transform.IsChildOf(target.transform) || target.transform.IsChildOf(h.collider.transform)))
            { hit = true; break; }
            yield return null;
        }
        yield return new WaitForSeconds(0.35f);   // подсказка "Нажмите E" успевает появиться
        if (hit) BotInput.Press(KeyCode.E);
        else
        {
            Note("E напрямую: " + target.name);
            switch (target)
            {
                case PickupItem p: p.Pickup(inv); break;
                case PhoneController ph: ph.AnswerPhone(); break;
                case InteractableTV tv: tv.Interact(); break;
                case NPCDialogueController n: n.Interact(); break;
                case Pentagram pg: pg.Interact(inv); break;
                case CandleInteract c: c.TryLight(inv); break;
                case InteractableDoor d: d.Interact(); break;
                case HideableCloset hc: hc.Interact(); break;
            }
        }
        yield return null;
        yield return null;
    }

    IEnumerator Dialogue()
    {
        for (float t = 0f; t < 2f && !DialogueManager.IsDialogueActive; t += Time.deltaTime) yield return null;
        var dm = DialogueManager.Instance;
        var typing = typeof(DialogueManager).GetField("isTyping", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        string lastText = null; float shownAt = 0f;
        while (DialogueManager.IsDialogueActive)
        {
            string text = dm.dialogueText.text;
            if (text != lastText) { lastText = text; shownAt = Time.time; }
            bool typed = !(bool)typing.GetValue(dm);
            float readTime = 1.4f + text.Length * 0.045f;
            bool voice = dm.voiceSource != null && dm.voiceSource.isPlaying;
            if (typed && Time.time - shownAt > readTime && (!voice || Time.time - shownAt > readTime + 6f))
            {
                BotInput.Press(KeyCode.Mouse0);
                yield return new WaitForSeconds(0.3f);
            }
            yield return null;
        }
        HoldLook();
    }

    // Восточное крыло 2 этажа (комната сектанта) с остальным зданием связано только восточной лестницей,
    // а проходы в крыле уже, чем навмеш (он рассчитан на бабку). Охранник уже - проходит по точкам,
    // найденным поиском пути по проходимости его капсулы.
    static readonly Vector3[] eastWing =
    {
        new Vector3(24.00f, 7.85f, -9.50f), new Vector3(22.75f, 7.85f, -6.25f), new Vector3(23.50f, 7.85f, -1.50f),
        new Vector3(25.25f, 7.85f, -1.25f), new Vector3(25.25f, 7.85f, -0.25f), new Vector3(20.50f, 7.85f, -0.25f),
        new Vector3(18.00f, 7.85f, -4.00f), new Vector3(15.00f, 7.85f, -0.50f),
    };

    IEnumerator EnterCultistRoom()
    {
        yield return WalkTo(new Vector3(24f, 7.85f, -10f));       // по восточной лестнице
        for (int i = 0; i < eastWing.Length; i++) yield return Go(eastWing[i]);
    }

    IEnumerator LeaveCultistRoom()
    {
        for (int i = eastWing.Length - 2; i >= 0; i--) yield return Go(eastWing[i]);
    }

    // Фонарик: держим включённым, когда сел - меняем батарейку (R), если батареек нет - тихо подзаряжаем
    bool wantLight;
    float nextLightFix;
    void KeepLight()
    {
        if (!wantLight || !pc.enabled || DialogueManager.IsDialogueActive || HideableCloset.IsPlayerHidden || Time.time < nextLightFix) return;
        var fl = pc.GetComponent<Flashlight>();
        if (fl == null || fl.handModel == null || !fl.handModel.activeSelf) return;   // фонарик не в руке
        if (fl.currentCharge < 5f)
        {
            if (fl.batteryItem != null && inv.Has(fl.batteryItem)) BotInput.Press(KeyCode.R);
            else fl.currentCharge = fl.maxCharge * 0.6f;
            nextLightFix = Time.time + 0.8f;
        }
        else if (!fl.IsOn) { BotInput.Press(KeyCode.Mouse0); nextLightFix = Time.time + 1.2f; }
    }

    IEnumerator SelectItem(ItemType type)
    {
        for (int i = 0; i < inv.slots.Length; i++)
            if (inv.slots[i].item != null && inv.slots[i].item.type == type)
            {
                BotInput.Press(KeyCode.Alpha1 + i);
                yield return new WaitForSeconds(0.5f);
                yield break;
            }
    }

    // ================= Движение =================

    // Идти по навмешу к точке. Пока идёт - смотрит по ходу пути (на лестнице - вверх/вниз), открывает двери,
    // при погоне бабки рядом - бежит. Если застрял - пересчитывает путь, в крайнем случае чуть подталкивается.
    IEnumerator WalkTo(Vector3 target, bool run = false, float stopDist = 0.6f)
    {
        var path = new NavMeshPath();
        Vector3 goal = target;
        // навмеш только если он рядом по высоте (в вагончике его нет - под полом лежит навмеш земли)
        if (!InTrailer(target) && NavMesh.SamplePosition(target, out NavMeshHit gh, 2f, NavMesh.AllAreas)) goal = gh.position;
        bool onMesh = true;

        float repath = 0f, stuckT = 0f, total = 0f;
        int nudges = 0;
        float sidestep = 0f, sideDir = 1f;
        Vector3 lastPos = pc.transform.position;
        List<Vector3> pts = new List<Vector3>();
        while (total < 90f)
        {
            float dt = Time.deltaTime;
            total += dt;
            if (!pc.enabled || DialogueManager.IsDialogueActive) { BotInput.Move = Vector2.zero; yield return null; continue; }

            Vector3 feet = pc.transform.position;
            if (Flat(goal - feet).magnitude < stopDist && Mathf.Abs(goal.y - feet.y) < 1.6f) break;

            repath -= dt;
            if (repath <= 0f)
            {
                repath = 0.5f;
                pts.Clear();
                Vector3 start = feet;
                // на улице навмеш местами на 1-2 м ниже/выше земли, а в вагончике его нет вовсе
                NavMeshHit sh = default;
                onMesh = !InTrailer(feet) && NavMesh.SamplePosition(feet + Vector3.up * 0.2f, out sh, 2.2f, NavMesh.AllAreas);
                if (onMesh) start = sh.position;
                if (onMesh && NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, path) && path.corners.Length > 1)
                {
                    pts.AddRange(path.corners);
                    // цель за краем навмеша (камень у стены, на столе) - идём до последней достижимой точки
                    Vector3 end = path.corners[path.corners.Length - 1];
                    if (path.status == NavMeshPathStatus.PathPartial && Flat(end - target).magnitude < 3f && Mathf.Abs(end.y - target.y) < 1.6f) goal = end;
                }
                else { pts.Add(feet); pts.Add(goal); }
            }

            Vector3 aim = Lookahead(pts, feet, 1.4f);
            Vector3 to = Flat(aim - feet);
            float wantYaw = Quaternion.LookRotation(to.sqrMagnitude > 1e-4f ? to : pc.transform.forward).eulerAngles.y;
            float err = Mathf.DeltaAngle(pc.transform.eulerAngles.y, wantYaw);

            // Взгляд по ходу пути, на высоте глаз (на лестнице - наклон по ступеням)
            // (на улице навмеш не совпадает с землёй - там смотрим ровно, от своих глаз)
            Vector3 eye = InBuilding(feet) ? aim + Vector3.up * 1.45f : new Vector3(aim.x, feet.y + 1.5f, aim.z);
            lookTarget = eye + to.normalized * 2f;

            // Бабка близко - бежать
            bool danger = GrannyNear(10f);
            BotInput.Run = (run || danger) && Mathf.Abs(err) < 30f;
            BotInput.Move = Mathf.Abs(err) < 55f ? new Vector2(0f, 1f) : Vector2.zero;
            if (sidestep > 0f) { sidestep -= dt; BotInput.Move = new Vector2(sideDir, 1f); }

            OpenDoorAhead();

            // Застрял
            if ((feet - lastPos).magnitude < 0.05f * Mathf.Max(1f, dt * 60f) && BotInput.Move.y > 0f) stuckT += dt; else stuckT = 0f;
            lastPos = feet;
            if (stuckT > 1.2f && !onMesh) break;   // без навмеша (в вагончике) - упёрся в мебель, значит пришёл
            if (stuckT > 1.2f && nudges >= 3 && Flat(goal - feet).magnitude < 2.2f && Mathf.Abs(goal.y - feet.y) < 1.6f) break;   // упёрся в стол у цели
            if (stuckT > 1.2f)
            {
                nudges++;
                stuckT = 0f; repath = 0f;
                if (nudges <= 3)
                {
                    // упёрся (коробка, угол) - обходим боком, как игрок
                    sideDir = nudges % 2 == 1 ? 1f : -1f;
                    sidestep = 0.8f;
                }
                else if (nudges > 5 && onMesh && pts.Count > 1 && InBuilding(feet))
                {
                    // совсем застрял (край лестницы) - переносимся на пару метров вперёд по пути
                    Vector3 p = Lookahead(pts, feet, 2.2f);
                    Teleport(p + Vector3.up * 0.15f);
                    nudges = 0;
                }
                else
                {
                    // не вышло - сдвигаемся вбок на свободное место (высоту оставляем свою: навмеш на улице не совпадает с землёй)
                    Vector3 side = Vector3.Cross(Vector3.up, to.normalized);
                    foreach (float sgn in new[] { 1f, -1f })
                    {
                        Vector3 p = feet + side * sgn * 0.6f + to.normalized * 0.3f;
                        if (!Physics.CheckCapsule(p + Vector3.up * 0.65f, p + Vector3.up * 1.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                        { Teleport(new Vector3(p.x, feet.y + 0.1f, p.z)); break; }
                    }
                }
                Note("обходит препятствие у " + feet);
            }
            yield return null;
        }
        BotInput.Move = Vector2.zero;
        BotInput.Run = false;
        if (total >= 90f) Note("не дошёл до " + target);
        yield return new WaitForSeconds(0.15f);
    }

    // Прямой шаг без навмеша (внутри вагончика)
    IEnumerator Go(Vector3 target)
    {
        for (float t = 0f; t < 10f; t += Time.deltaTime)
        {
            Vector3 to = Flat(target - pc.transform.position);
            if (to.magnitude < 0.25f) break;
            float wantYaw = Quaternion.LookRotation(to).eulerAngles.y;
            float err = Mathf.DeltaAngle(pc.transform.eulerAngles.y, wantYaw);
            lookTarget = target + Vector3.up * 1.5f + to.normalized * 2f;
            BotInput.Move = Mathf.Abs(err) < 40f ? new Vector2(0f, 1f) : Vector2.zero;
            OpenDoorAhead();
            yield return null;
        }
        BotInput.Move = Vector2.zero;
        yield return new WaitForSeconds(0.1f);
    }

    float nextDoorTry;
    void OpenDoorAhead()
    {
        if (Time.time < nextDoorTry) return;
        if (Physics.Raycast(cam.position, Flat(cam.forward).normalized, out RaycastHit h, 1.8f) && h.collider.TryGetComponent(out InteractableDoor d)
            && !d.IsOpen && !d.IsLocked && d.GetComponentInParent<HideableCloset>() == null)
        {
            // смотрим чуть на дверь - и E
            BotInput.Press(KeyCode.E);
            nextDoorTry = Time.time + 1.5f;
            Note("открыл дверь " + d.name);
        }
    }

    bool GrannyNear(float dist)
    {
        var hm = FindAnyObjectByType<HallucinationManager>();
        var g = hm != null ? hm.realGhost : null;
        return g != null && g.activeInHierarchy && Vector3.Distance(g.transform.position, pc.transform.position) < dist;
    }

    static Vector3 Lookahead(List<Vector3> pts, Vector3 from, float ahead)
    {
        // ближайший отрезок пути, от него - на ahead метров вперёд
        int seg = 0; float best = float.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p = ClosestOnSegment(pts[i], pts[i + 1], from);
            float d = (p - from).sqrMagnitude;
            if (d < best) { best = d; seg = i; }
        }
        Vector3 cur = ClosestOnSegment(pts[seg], pts[Mathf.Min(seg + 1, pts.Count - 1)], from);
        float left = ahead;
        for (int i = seg + 1; i < pts.Count; i++)
        {
            float l = Vector3.Distance(cur, pts[i]);
            if (l >= left) return Vector3.MoveTowards(cur, pts[i], left);
            left -= l; cur = pts[i];
        }
        return pts[pts.Count - 1];
    }

    static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static readonly Bounds building = new Bounds(new Vector3(-3.5f, 9.75f, -5.5f), new Vector3(62f, 12.5f, 22.4f));
    static bool InBuilding(Vector3 p) => building.Contains(p);

    // Внутри вагончика (пол на 1.2 м выше земли, навмеша нет)
    static bool InTrailer(Vector3 p) => p.x > -27.8f && p.x < -20.8f && p.z > -38.8f && p.z < -34.3f && p.y > 0.7f;

    void Teleport(Vector3 p)
    {
        var cc = pc.GetComponent<CharacterController>();
        cc.enabled = false; pc.transform.position = p; cc.enabled = true;
    }

    // ================= Взгляд =================

    void HoldLook()
    {
        lookTarget = null;
        freeLook = true;
        if (pc != null) { lookYaw = pc.transform.eulerAngles.y; lookPitch = pc.Pitch; }
    }

    IEnumerator LookAtFor(Vector3 point, float time)
    {
        lookTarget = point;
        BotInput.Move = Vector2.zero;
        yield return new WaitForSeconds(time);
    }

    IEnumerator Idle(float time)
    {
        BotInput.Move = Vector2.zero;
        yield return new WaitForSeconds(time);
    }

    // Осмотреться: влево, вправо, вернуться (как настороженный игрок)
    IEnumerator LookAround(float time, float amp)
    {
        BotInput.Move = Vector2.zero;
        float baseYaw = pc.transform.eulerAngles.y, basePitch = pc.Pitch;
        lookTarget = null; freeLook = true;
        float side = Random.value < 0.5f ? -1f : 1f;
        float[] yaws = { side * amp, -side * amp * 0.8f, 0f };
        foreach (float y in yaws)
        {
            lookYaw = baseYaw + y;
            lookPitch = basePitch + Random.Range(-6f, 4f);
            yield return new WaitForSeconds(time / yaws.Length);
        }
        lookYaw = baseYaw; lookPitch = basePitch;
    }

    void Update()
    {
        if (!BotInput.Active || pc == null || cam == null) return;
        if (!pc.enabled) { BotInput.Look = BotInput.Look * 0.5f; return; }
        KeepLight();

        float curYaw = pc.transform.eulerAngles.y, curPitch = pc.Pitch;
        float wantYaw = curYaw, wantPitch = curPitch;
        if (lookTarget.HasValue)
        {
            Vector3 d = lookTarget.Value - cam.position;
            if (d.sqrMagnitude > 0.01f)
            {
                wantYaw = Quaternion.LookRotation(Flat(d).sqrMagnitude > 1e-4f ? Flat(d) : pc.transform.forward).eulerAngles.y;
                wantPitch = -Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg;
            }
        }
        else if (freeLook) { wantYaw = lookYaw; wantPitch = lookPitch; }

        // плавно, с ограничением скорости, как мышь в руке
        float dy = Mathf.DeltaAngle(curYaw, wantYaw);
        float dp = wantPitch - curPitch;
        float stepY = Mathf.Clamp(dy * 6f * Time.deltaTime, -yawSpeed * Time.deltaTime, yawSpeed * Time.deltaTime);
        float stepP = Mathf.Clamp(dp * 5f * Time.deltaTime, -pitchSpeed * Time.deltaTime, pitchSpeed * Time.deltaTime);

        // лёгкое дрожание руки
        noiseT += Time.deltaTime;
        stepY += (Mathf.PerlinNoise(noiseT * 0.6f, 1.3f) - 0.5f) * 0.12f;
        stepP += (Mathf.PerlinNoise(2.7f, noiseT * 0.5f) - 0.5f) * 0.08f;

        BotInput.Look = new Vector2(stepY, -stepP);
    }

    // ================= Поиск =================

    PickupItem FindPickup(ItemType type, Vector3? near = null)
    {
        Vector3 at = near ?? pc.transform.position;
        PickupItem best = null;
        foreach (var p in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            if (p.item != null && p.item.type == type && p.gameObject.activeInHierarchy && p.GetComponentInParent<PlayerController>() == null &&
                (best == null || Vector3.Distance(p.transform.position, at) < Vector3.Distance(best.transform.position, at))) best = p;
        return best;
    }

    // Свечи по кругу, начиная с ближайшей
    IEnumerable<CandleInteract> Ordered(CandleInteract[] list)
    {
        var left = new List<CandleInteract>(list);
        Vector3 from = pc.transform.position;
        while (left.Count > 0)
        {
            left.Sort((a, b) => Vector3.Distance(a.transform.position, from).CompareTo(Vector3.Distance(b.transform.position, from)));
            var c = left[0]; left.RemoveAt(0);
            from = c.transform.position;
            yield return c;
        }
    }

    // ================= Титры =================

    void BuildCard()
    {
        var go = new GameObject("Gameplay Card");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        cardGroup = go.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;

        cardBack = new GameObject("Black").AddComponent<Image>();
        cardBack.transform.SetParent(go.transform, false);
        cardBack.color = Color.black;
        Stretch(cardBack.rectTransform);

        cardTitle = Text(go.transform, titleFont, titleMaterial, 150, new Color(0.62f, 0.05f, 0.05f), new Vector2(0, 40));
        cardSub = Text(go.transform, textFont, textMaterial, 40, new Color(0.75f, 0.72f, 0.68f), new Vector2(0, -80));
    }

    static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, Material mat, float size, Color color, Vector2 pos)
    {
        var t = new GameObject("Text").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        if (font != null) t.font = font;
        if (mat != null) t.fontSharedMaterial = mat;
        t.fontSize = size; t.color = color; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(1800, 220);
        t.rectTransform.anchoredPosition = pos;
        return t;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    IEnumerator Card(string title, string sub, float time)
    {
        cardTitle.text = title; cardSub.text = sub;
        cardGroup.alpha = 1f;
        cardTitle.alpha = 0f; cardSub.alpha = 0f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((time - t) / 1f);
            cardTitle.alpha = a; cardSub.alpha = Mathf.Clamp01((t - 0.6f) / 1.2f) * Mathf.Clamp01((time - t) / 1f);
            yield return null;
        }
        cardTitle.alpha = cardSub.alpha = 0f;
    }

    IEnumerator FadeCard(float from, float to, float time, bool onlyBlack = false)
    {
        if (onlyBlack) { cardTitle.text = cardSub.text = ""; }
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            cardGroup.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        cardGroup.alpha = to;
    }

    void OnDestroy() { BotInput.Active = false; BotInput.Reset(); }
}
#endif
