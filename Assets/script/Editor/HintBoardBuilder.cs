using TMPro;
using UnityEditor;
using UnityEngine;

// Tools > Level > Build Hint Board: пробковая доска следователя в вагончике с пришпиленными записками,
// написанными от руки: управление и механики игры. Повторный запуск пересобирает доску.
public static class HintBoardBuilder
{
    const string RootName = "Hint Board";
    const string Dir = "Assets/models/Board";
    // Задняя стена вагончика (смотрит внутрь, на +z)
    static readonly Vector3 WallPoint = new Vector3(-25.2f, 1.65f, -38.16f);
    static readonly Vector3 WallNormal = Vector3.forward;

    struct Note
    {
        public Vector2 pos;      // по доске, м (x вправо, y вверх от центра)
        public Vector2 size;
        public float angle;
        public string text;
        public float fontSize;
        public Note(float x, float y, float w, float h, float a, float fs, string t) { pos = new Vector2(x, y); size = new Vector2(w, h); angle = a; fontSize = fs; text = t; }
    }

    [MenuItem("Tools/Level/Build Hint Board")]
    public static void Build()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Hint Board");
        root.transform.SetPositionAndRotation(WallPoint + WallNormal * 0.03f, Quaternion.LookRotation(-WallNormal));

        // Модель: тонкая сторона (локальная x) - к стене
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/Доска/Доска.fbx"));
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = Quaternion.Euler(90f, -90f, 0f);   // пробковой стороной в комнату
        var boardRenderer = model.GetComponentInChildren<Renderer>();
        boardRenderer.sharedMaterial = BoardMaterial();
        // Центр доски - в центре точки на стене (у модели смещён пивот)
        model.transform.position += root.transform.position - boardRenderer.bounds.center;

        var font = HandFont();
        var paperMat = Lit("NotePaper", new Color(0.86f, 0.82f, 0.72f), 0.1f);
        var paperDark = Lit("NotePaperOld", new Color(0.74f, 0.68f, 0.55f), 0.1f);
        var pinMat = Lit("NotePin", new Color(0.55f, 0.04f, 0.03f), 0.6f);
        var ink = TextMaterial(font, "NoteInk", new Color(0.1f, 0.09f, 0.12f));

        // Доска 2.1 x 1.16 м. Свободные места - по краям, поверх снимков
        var notes = new[]
        {
            new Note(-0.72f, 0.33f, 0.52f, 0.36f, -3f, 0.42f,
                "<b>УПРАВЛЕНИЕ</b>\nWASD — идти, SHIFT — бежать\nПРОБЕЛ — прыжок\nE — взять / открыть / спрятаться\n1–5, колесо — что в руке"),
            new Note(-0.72f, -0.15f, 0.52f, 0.4f, 2f, 0.42f,
                "<b>ЛКМ</b> — фонарик / зажигалка\nили бросить бутылку\n<b>R</b> — сменить батарейку\n<b>ESC</b> — пауза\nбатарейки садятся — ищи запасные!"),
            new Note(0.05f, -0.4f, 0.48f, 0.26f, -2f, 0.4f,
                "Шкафы — укрытие.\nВнутри она тебя <u>не видит</u>.\nE — залезть, E — вылезти."),
            new Note(0.72f, 0.36f, 0.5f, 0.3f, 4f, 0.4f,
                "Бутылкой в неё —\nона <b>замедлится</b>.\nНенадолго."),
            new Note(0.72f, -0.08f, 0.5f, 0.32f, -4f, 0.4f,
                "Где-то в здании <b>линза</b>.\nНаденешь на фонарь —\nсветит дальше."),
            new Note(0.66f, -0.44f, 0.46f, 0.22f, 3f, 0.38f,
                "Планы эвакуации —\nу лестниц. Смотри по E."),
            new Note(0.05f, 0.42f, 0.44f, 0.22f, 1f, 0.42f,
                "<b>ЕСЛИ ОНА ПОЙМАЕТ —\nТЫ ТЕРЯЕШЬ РАССУДОК.\nЕГО ХВАТИТ НА ТРИ РАЗА.</b>"),
        };

        int i = 0;
        foreach (var n in notes)
        {
            var note = new GameObject("Note " + (++i));
            note.transform.SetParent(root.transform, false);
            note.transform.localPosition = new Vector3(n.pos.x, n.pos.y, -0.025f - i * 0.0015f);
            note.transform.localRotation = Quaternion.Euler(0f, 0f, n.angle);

            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            paper.name = "Paper";
            paper.transform.SetParent(note.transform, false);
            paper.transform.localScale = new Vector3(n.size.x, n.size.y, 1f);
            paper.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? paperDark : paperMat;

            var pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(pin.GetComponent<Collider>());
            pin.name = "Pin";
            pin.transform.SetParent(note.transform, false);
            pin.transform.localPosition = new Vector3(0f, n.size.y * 0.5f - 0.025f, -0.006f);
            pin.transform.localScale = Vector3.one * 0.018f;
            pin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;

            var t = new GameObject("Text").AddComponent<TextMeshPro>();
            t.transform.SetParent(note.transform, false);
            t.transform.localPosition = new Vector3(0f, -0.01f, -0.002f);
            t.font = font;
            t.fontSharedMaterial = ink;
            t.text = n.text;
            t.fontSize = n.fontSize;
            t.color = new Color(0.12f, 0.1f, 0.12f);
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(n.size.x - 0.04f, n.size.y - 0.04f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.15f;
            t.fontSizeMax = n.fontSize;
        }

        // Надпись маркером прямо по доске
        var scrawl = new GameObject("Scrawl").AddComponent<TextMeshPro>();
        scrawl.transform.SetParent(root.transform, false);
        scrawl.transform.localPosition = new Vector3(0.05f, 0.12f, -0.03f);
        scrawl.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        scrawl.font = font;
        scrawl.fontSharedMaterial = TextMaterial(font, "MarkerInk", new Color(0.55f, 0.03f, 0.02f));
        scrawl.text = "НЕ ВЕРЬ ГЛАЗАМ";
        scrawl.fontSize = 1.1f;
        scrawl.color = new Color(0.6f, 0.03f, 0.02f);
        scrawl.alignment = TextAlignmentOptions.Center;
        scrawl.textWrappingMode = TextWrappingModes.NoWrap;
        scrawl.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }

    static Material BoardMaterial()
    {
        string path = Dir + "/BoardMat.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
        string tex = Dir + "/Доска/Textures/DefaultMaterial_";
        m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Base_color.png"));
        m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Normal_OpenGL.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", 0.2f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Lit(string name, Color c, float smooth)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static TMP_FontAsset HandFont()
    {
        string path = "Assets/Fonts/Caveat/Caveat SDF.asset";
        var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (f != null) return f;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Caveat/Caveat.ttf");
        f = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        f.name = "Caveat SDF";
        AssetDatabase.CreateAsset(f, path);
        f.material.name = "Caveat SDF Material";
        AssetDatabase.AddObjectToAsset(f.material, f);
        foreach (var tex in f.atlasTextures) { tex.name = "Caveat SDF Atlas"; AssetDatabase.AddObjectToAsset(tex, f); }
        // Все нужные буквы сразу в атлас, чтобы в билде не было пустых квадратов
        f.TryAddCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюяABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?—–-/()«»\"'");
        EditorUtility.SetDirty(f);
        AssetDatabase.SaveAssets();
        return f;
    }

    // Освещаемый текст (чернила на бумаге, а не светящиеся буквы)
    // Освещаемый шейдер TMP не берёт цвет из вершин - цвет задаётся в материале
    static Material TextMaterial(TMP_FontAsset font, string name, Color color)
    {
        string path = "Assets/Fonts/Caveat/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(font.material) { shader = Shader.Find("TextMeshPro/SRP/TMP_SDF-HDRP LIT"), name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_FaceColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }
}
