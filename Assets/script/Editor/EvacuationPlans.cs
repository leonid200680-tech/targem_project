using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Tools > Level > Place Evacuation Plans: рисует план каждого этажа по геометрии здания
// (стены, мебель, лестницы и выход - зелёным) и вешает "План эвакуации" на стены коридора у лестниц.
// На плане на стене красная точка "вы здесь"; по E план открывается на весь экран с номерами кабинетов
// (берутся из табличек Tools > Level > Place Room Numbers - их лучше расставить раньше).
public static class EvacuationPlans
{
    const string RootName = "Evacuation Plans";
    const string Folder = "Assets/UI/EvacuationPlans";
    const string TitleFontPath = "Assets/Fonts/CormorantSC/CormorantSC-Bold SDF.asset";

    static readonly float[] FloorY = { 3.73f, 7.84f, 11.99f };
    static readonly Rect Building = Rect.MinMaxRect(-34.2f, -15.8f, 26.8f, 5.7f);   // x, z с наружными стенами
    static readonly Vector2[] Stairs = { new Vector2(-33.8f, -29.7f), new Vector2(-9.6f, -5.5f), new Vector2(22.7f, 26.3f) };
    static readonly Vector3 MainExit = new Vector3(-3.5f, 3.73f, 5.3f);   // входная дверь, 1 этаж
    const float StairMinZ = -15.5f, StairMaxZ = -9.3f;
    const float Res = 0.1f;   // метров на клетку плана
    const int Px = 2;         // пикселей на клетку

    static readonly Color Paper = new Color(0.93f, 0.91f, 0.84f);
    static readonly Color WallColor = new Color(0.16f, 0.16f, 0.18f);
    static readonly Color FurnitureColor = new Color(0.76f, 0.74f, 0.69f);
    static readonly Color Green = new Color(0.18f, 0.6f, 0.24f);

    [MenuItem("Tools/Level/Place Evacuation Plans")]
    public static void Place()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/UI", "EvacuationPlans");
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Evacuation Plans");

        var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        var titleMat = LitTextMaterial(titleFont);
        var frameMat = LitMaterial("PlanFrame", new Color(0.08f, 0.3f, 0.14f), 0.4f);
        var dotMat = LitMaterial("PlanDot", new Color(0.85f, 0.05f, 0.03f), 0.3f);
        var rooms = CollectRooms();
        int placed = 0;

        for (int f = 0; f < FloorY.Length; f++)
        {
            var tex = DrawFloor(f);
            var mapMat = LitMaterial("Floor" + (f + 1), Color.white, 0.15f);
            mapMat.SetTexture("_BaseColorMap", tex);
            mapMat.SetTextureScale("_BaseColorMap", new Vector2(-1f, -1f));   // поворот на 180°, см. BuildPlan
            mapMat.SetTextureOffset("_BaseColorMap", new Vector2(1f, 1f));

            // Номера кабинетов этого этажа в координатах плана
            var names = new List<string>();
            var positions = new List<Vector2>();
            foreach (var r in rooms)
                if (Mathf.Abs(r.Value.y - (FloorY[f] + 1.75f)) < 0.5f)
                { names.Add(r.Key); positions.Add(ToUV(r.Value)); }

            foreach (var s in Stairs)
            {
                if (!FindSpot(f, s, out var hit)) continue;
                var go = BuildPlan(root.transform, hit, f, tex, mapMat, frameMat, dotMat, titleFont, titleMat);
                var plan = go.GetComponent<EvacuationPlan>();
                plan.roomNames = names.ToArray();
                plan.roomPositions = positions.ToArray();
                placed++;
            }
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[EvacuationPlans] placed {placed}");
    }

    static Vector2 ToUV(Vector3 world) =>
        new Vector2((world.x - Building.xMin) / Building.width, (world.z - Building.yMin) / Building.height);

    // Номер кабинета -> место таблички (берём первую из пары)
    static Dictionary<string, Vector3> CollectRooms()
    {
        var result = new Dictionary<string, Vector3>();
        var root = GameObject.Find("Room Numbers");
        if (root == null) return result;
        foreach (Transform t in root.transform)
        {
            string name = t.name.Replace("Room ", "");
            if (!result.ContainsKey(name)) result[name] = t.position;
        }
        return result;
    }

    // ---------- Рисунок этажа ----------

    static bool Solid(float x, float y, float z) =>
        Physics.CheckBox(new Vector3(x, y, z), new Vector3(Res * 0.5f, 0.05f, Res * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

    static Texture2D DrawFloor(int f)
    {
        float floor = FloorY[f];
        int cw = Mathf.CeilToInt(Building.width / Res), ch = Mathf.CeilToInt(Building.height / Res);
        int w = cw * Px, h = ch * Px;
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Paper;

        for (int cz = 0; cz < ch; cz++)
            for (int cx = 0; cx < cw; cx++)
            {
                float x = Building.xMin + (cx + 0.5f) * Res, z = Building.yMin + (cz + 0.5f) * Res;
                Color c = Paper;
                bool inStairs = false;
                foreach (var s in Stairs) if (x > s.x && x < s.y && z > StairMinZ && z < StairMaxZ) inStairs = true;

                if (inStairs) c = ((cx + cz) / 3) % 3 == 0 ? Color.white : Green;   // зелёная штриховка
                else if (Solid(x, floor + 2.2f, z) && Solid(x, floor + 3.1f, z)) c = WallColor;
                else if (Solid(x, floor + 0.8f, z)) c = FurnitureColor;
                Fill(px, w, cx * Px, cz * Px, Px, Px, c);
            }

        // Стены лестничных клеток поверх штриховки - тёмной рамкой
        foreach (var s in Stairs)
        {
            int x0 = Mathf.RoundToInt((s.x - Building.xMin) / Res) * Px, x1 = Mathf.RoundToInt((s.y - Building.xMin) / Res) * Px;
            int z0 = Mathf.RoundToInt((StairMinZ - Building.yMin) / Res) * Px, z1 = Mathf.RoundToInt((StairMaxZ - Building.yMin) / Res) * Px;
            Frame(px, w, h, x0, z0, x1, z1, Green, 3);
        }

        // Выход на улицу (только 1 этаж): зелёная стрелка наружу
        if (f == 0)
        {
            int ex = Mathf.RoundToInt((MainExit.x - Building.xMin) / Res) * Px;
            int ez = Mathf.RoundToInt((MainExit.z - Building.yMin) / Res) * Px;
            Arrow(px, w, h, ex, ez - 80, 50, Green);   // остриё упирается в дверь
        }

        Frame(px, w, h, 0, 0, w - 1, h - 1, WallColor, 4);

        var tex = new Texture2D(w, h, TextureFormat.RGB24, true);
        tex.SetPixels(px);
        tex.Apply();
        string path = $"{Folder}/Floor{f + 1}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 2048;
        imp.npotScale = TextureImporterNPOTScale.None;   // не ужимать до 1024x512 - иначе план растягивается
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Fill(Color[] px, int w, int x, int y, int sw, int sh, Color c)
    {
        for (int j = 0; j < sh; j++)
            for (int i = 0; i < sw; i++)
                px[(y + j) * w + x + i] = c;
    }

    static void Frame(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c, int t)
    {
        for (int x = x0; x <= x1; x++)
            for (int k = 0; k < t; k++) { Set(px, w, h, x, y0 + k, c); Set(px, w, h, x, y1 - k, c); }
        for (int y = y0; y <= y1; y++)
            for (int k = 0; k < t; k++) { Set(px, w, h, x0 + k, y, c); Set(px, w, h, x1 - k, y, c); }
    }

    // Стрелка вверх (наружу, к +z): древко и наконечник
    static void Arrow(Color[] px, int w, int h, int x, int y, int len, Color c)
    {
        for (int j = 0; j < len; j++)
            for (int i = -4; i <= 4; i++) Set(px, w, h, x + i, y + j, c);
        for (int j = 0; j < 22; j++)
            for (int i = -(22 - j); i <= 22 - j; i++) Set(px, w, h, x + i, y + len + j, c);
    }

    static void Set(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c;
    }

    // ---------- Место на стене ----------

    // Стена коридора рядом с лестницей, подальше от надписи этажа; табличке нужно ровное место 1.2 x 0.7 м
    static bool FindSpot(int f, Vector2 s, out RaycastHit hit)
    {
        hit = default;
        float y = FloorY[f] + 1.5f;
        foreach (float off in new[] { 2.4f, 3f, 3.6f, 4.2f })
            foreach (float x in new[] { s.y + off, s.x - off })
            {
                if (x < Building.xMin + 1f || x > Building.xMax - 1f) continue;
                foreach (float startZ in new[] { StairMaxZ + 1.2f, StairMaxZ + 2.5f })
                {
                    var from = new Vector3(x, y, startZ);
                    if (!Physics.Raycast(from, Vector3.back, out hit, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.z < StairMaxZ - 0.8f || hit.point.z > StairMaxZ + 0.8f || hit.normal.z < 0.9f) continue;
                    if (Flat(from, hit.distance) && ClearSides(hit)) return true;
                }
            }
        return false;
    }

    static bool Flat(Vector3 from, float dist)
    {
        foreach (var d in new[] { new Vector3(-0.6f, 0, 0), new Vector3(0.6f, 0, 0), new Vector3(0, 0.35f, 0), new Vector3(0, -0.35f, 0) })
        {
            if (!Physics.Raycast(from + d, Vector3.back, out var h, 3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(h.distance - dist) > 0.05f) return false;
        }
        return true;
    }

    // Перед стеной по бокам от таблички ничего не торчит (перпендикулярные перегородки, шкафы)
    static bool ClearSides(RaycastHit hit)
    {
        Vector3 side = Vector3.Cross(Vector3.up, hit.normal);
        foreach (float dy in new[] { -0.3f, 0f, 0.3f })
        {
            Vector3 p = hit.point + hit.normal * 0.06f + Vector3.up * dy;
            if (Physics.Raycast(p, side, 0.75f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.Raycast(p, -side, 0.75f, ~0, QueryTriggerInteraction.Ignore)) return false;
        }
        return true;
    }

    // ---------- Табличка ----------

    static GameObject BuildPlan(Transform root, RaycastHit hit, int f, Texture2D tex, Material mapMat, Material frameMat,
        Material dotMat, TMP_FontAsset font, Material titleMat)
    {
        const float width = 1.1f;
        float mapH = width * tex.height / tex.width;
        float height = mapH + 0.2f;

        var go = new GameObject($"Evacuation Plan {f + 1}");
        go.transform.SetParent(root, false);
        go.transform.position = hit.point + hit.normal * 0.015f;
        go.transform.rotation = Quaternion.LookRotation(-hit.normal);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(width + 0.06f, height + 0.06f, 0.03f);

        var plan = go.AddComponent<EvacuationPlan>();
        plan.planTexture = tex;
        plan.floor = f + 1;
        plan.youAreHere = ToUV(hit.point);
        plan.rotate180 = true;

        Quad(go.transform, "Frame", Vector3.zero, new Vector2(width + 0.06f, height + 0.06f), frameMat);
        Quad(go.transform, "Paper", new Vector3(0, 0, -0.002f), new Vector2(width, height), LitMaterial("PlanPaper", Paper, 0.15f));
        Vector3 mapCenter = new Vector3(0f, -height * 0.5f + mapH * 0.5f + 0.02f, -0.004f);
        Quad(go.transform, "Map", mapCenter, new Vector2(width - 0.04f, mapH - 0.04f), mapMat);

        // "Вы здесь". Все планы висят на стенах, смотрящих на +z, и смотрящий стоит лицом к -z:
        // карта повёрнута на 180°, чтобы то, что впереди, было на плане вверху (материал карты тоже перевёрнут)
        Vector2 uv = plan.youAreHere;
        Quad(go.transform, "You Are Here", mapCenter + new Vector3(-(uv.x - 0.5f) * (width - 0.04f), -(uv.y - 0.5f) * (mapH - 0.04f), -0.002f),
            new Vector2(0.035f, 0.035f), dotMat);

        var title = new GameObject("Title");
        title.transform.SetParent(go.transform, false);
        title.transform.localPosition = new Vector3(0f, height * 0.5f - 0.08f, -0.004f);
        var tmp = title.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = titleMat;
        tmp.text = $"ПЛАН ЭВАКУАЦИИ  •  {f + 1} ЭТАЖ";
        tmp.fontSize = 0.55f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.color = new Color(0.1f, 0.42f, 0.17f);
        tmp.rectTransform.sizeDelta = new Vector2(width, 0.12f);
        return go;
    }

    static GameObject Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return q;
    }

    static Material LitMaterial(string name, Color color, float smooth)
    {
        string path = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LitTextMaterial(TMP_FontAsset font)
    {
        string path = $"{Folder}/PlanTitle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(font.material) { shader = Shader.Find("TextMeshPro/SRP/TMP_SDF-HDRP LIT") };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
