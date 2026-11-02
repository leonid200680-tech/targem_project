using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// Tools > Level > Place Room Numbers: таблички с номерами кабинетов (101, 102... 201...) у входов.
// Входы ищутся по геометрии: проём шириной 0.75-2 м в тонкой стене, по обе стороны которого можно пройти.
// Номер = этаж * 100 + порядковый номер входа на этаже (слева направо). Табличка вешается на стену
// рядом с проёмом с обеих сторон. Повторный запуск пересоздаёт таблички.
public static class RoomNumbers
{
    const string RootName = "Room Numbers";
    const string FontPath = "Assets/Fonts/RubikDirt/RubikDirt SDF.asset";
    const string PaintPath = "Assets/Fonts/RubikDirt/RubikDirt Wall Paint.mat";
    const string PlatePath = "Assets/material/RoomPlate.mat";

    static readonly float[] FloorY = { 3.73f, 7.84f, 11.99f };
    static readonly Rect Building = Rect.MinMaxRect(-33.6f, -15.2f, 26.1f, 5.0f);   // x, z внутри стен
    static readonly Vector2[] Stairs = { new Vector2(-33.8f, -29.7f), new Vector2(-9.6f, -5.5f), new Vector2(22.7f, 26.3f) };
    const float Cell = 0.25f;

    struct Doorway { public Vector3 center; public Vector3 along; public float halfWidth; }

    [MenuItem("Tools/Level/Place Room Numbers")]
    public static void Place()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Room Numbers");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var paint = AssetDatabase.LoadAssetAtPath<Material>(PaintPath);
        var plate = PlateMaterial();
        int total = 0;

        for (int f = 0; f < FloorY.Length; f++)
        {
            var doors = FindDoorways(FloorY[f]);
            doors.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            int index = 0;   // номера без пропусков: считаем только входы, где табличка встала
            foreach (var d in doors)
            {
                string number = ((f + 1) * 100 + index + 1).ToString();
                if (PlacePlates(root.transform, d, FloorY[f], number, font, paint, plate)) { index++; total++; }
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[RoomNumbers] rooms: {total}");
    }

    static bool Solid(Vector3 p, float y) =>
        Physics.CheckBox(new Vector3(p.x, y, p.z), new Vector3(Cell * 0.45f, 0.05f, Cell * 0.45f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

    // Сквозной проход: свободно на высоте пояса и над головой
    static bool Free(Vector3 p, float floor) => !Solid(p, floor + 1.0f) && !Solid(p, floor + 2.2f);

    static List<Doorway> FindDoorways(float floor)
    {
        var result = new List<Doorway>();
        float wy = floor + 2.2f;   // выше мебели: здесь видны только стены и перегородки

        // Стены, идущие вдоль x (сканируем строки по z) и вдоль z (сканируем столбцы по x)
        for (int axis = 0; axis < 2; axis++)
        {
            Vector3 along = axis == 0 ? Vector3.right : Vector3.forward;
            Vector3 across = axis == 0 ? Vector3.forward : Vector3.right;
            float aMin = axis == 0 ? Building.xMin : Building.yMin, aMax = axis == 0 ? Building.xMax : Building.yMax;
            float cMin = axis == 0 ? Building.yMin : Building.xMin, cMax = axis == 0 ? Building.yMax : Building.xMax;

            for (float c = cMin + 0.5f; c <= cMax - 0.5f; c += Cell)
            {
                int n = Mathf.FloorToInt((aMax - aMin) / Cell);
                var wall = new bool[n];
                // Стена - это то, что доходит почти до потолка (мебель столько не достаёт)
                for (int i = 0; i < n; i++)
                {
                    var p = along * (aMin + i * Cell) + across * c;
                    wall[i] = Solid(p, wy) && Solid(p, floor + 3.1f);
                }

                int k = 0;
                while (k < n)
                {
                    if (wall[k]) { k++; continue; }
                    int gapStart = k;
                    while (k < n && !wall[k]) k++;
                    int gapEnd = k;   // [gapStart, gapEnd)
                    int width = gapEnd - gapStart;
                    if (gapStart == 0 || gapEnd >= n || width < 3 || width > 8) continue;
                    if (!RunOf(wall, gapStart - 1, -1, 5) || !RunOf(wall, gapEnd, 1, 5)) continue;   // хотя бы по 1.25 м стены с обеих сторон

                    float mid = aMin + (gapStart + gapEnd - 1) * 0.5f * Cell;
                    Vector3 center = along * mid + across * c;
                    // Тонкая стена: по обе стороны от неё (и в самом проёме) можно пройти
                    if (!Free(center, floor) || !Free(center + across * 0.8f, floor) || !Free(center - across * 0.8f, floor)) continue;
                    if (InStairs(center) || NearPerimeter(center)) continue;

                    result.Add(new Doorway { center = new Vector3(center.x, floor, center.z), along = along, halfWidth = width * 0.5f * Cell });
                }
            }
        }

        // Одна и та же дверь находится в нескольких соседних строках - оставляем по одной
        var merged = new List<Doorway>();
        foreach (var d in result)
        {
            bool dup = false;
            foreach (var m in merged)
                if (m.along == d.along && Vector3.Distance(m.center, d.center) < 1.2f) { dup = true; break; }
            if (!dup) merged.Add(d);
        }
        return merged;
    }

    static bool RunOf(bool[] wall, int from, int step, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int j = from + i * step;
            if (j < 0 || j >= wall.Length || !wall[j]) return false;
        }
        return true;
    }

    static bool InStairs(Vector3 p)
    {
        foreach (var s in Stairs)
            if (p.x > s.x - 0.5f && p.x < s.y + 0.5f && p.z < -8.8f) return true;
        return false;
    }

    static bool NearPerimeter(Vector3 p) =>
        p.x < Building.xMin + 0.6f || p.x > Building.xMax - 0.6f || p.z < Building.yMin + 0.6f || p.z > Building.yMax - 0.6f;

    // Табличка на стене справа от проёма, с обеих сторон стены
    static bool PlacePlates(Transform root, Doorway d, float floor, string number, TMP_FontAsset font, Material paint, Material plate)
    {
        bool placed = false;
        Vector3 across = Vector3.Cross(Vector3.up, d.along);
        foreach (float side in new[] { 1f, -1f })
        {
            // Со стороны, куда смотрит табличка, "справа" - это своя сторона проёма
            Vector3 right = d.along * side;
            Vector3 spot = d.center + right * (d.halfWidth + 0.45f) + Vector3.up * 1.75f;
            Vector3 from = spot + across * side * 1.2f;
            if (!Physics.Raycast(from, -across * side, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Vector3.Dot(hit.normal, across * side) < 0.9f) continue;

            var go = new GameObject("Room " + number);
            go.transform.SetParent(root, false);
            go.transform.position = hit.point + hit.normal * 0.01f;
            go.transform.rotation = Quaternion.LookRotation(-hit.normal);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Plate";
            quad.transform.SetParent(go.transform, false);
            quad.transform.localScale = new Vector3(0.4f, 0.22f, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = plate;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var textGo = new GameObject("Number");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSharedMaterial = paint;
            tmp.text = number;
            tmp.fontSize = 1.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.color = new Color(0.86f, 0.84f, 0.78f);
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.22f);
            tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            placed = true;
        }
        return placed;
    }

    // Тёмная облезлая табличка
    static Material PlateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PlatePath);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("HDRP/Lit"));
        mat.SetColor("_BaseColor", new Color(0.09f, 0.11f, 0.13f));
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_Metallic", 0.3f);
        AssetDatabase.CreateAsset(mat, PlatePath);
        return mat;
    }
}
