using TMPro;
using UnityEditor;
using UnityEngine;

// Tools > Level > Place Floor Signs: крупные надписи "1 ЭТАЖ", "2 ЭТАЖ", "3 ЭТАЖ" краской на стенах
// у всех трёх лестниц: со стороны коридора рядом с проёмом и внутри лестничной клетки на задней стене.
// Места находятся лучами по геометрии здания. Повторный запуск пересоздаёт таблички.
public static class FloorSigns
{
    const string RootName = "Floor Signs";
    const string FontPath = "Assets/Fonts/RubikDirt/RubikDirt SDF.asset";
    const string MaterialPath = "Assets/Fonts/RubikDirt/RubikDirt Wall Paint.mat";

    static readonly float[] FloorY = { 3.73f, 7.84f, 11.99f };
    // Лестницы: x от и до; все прилегают к задней стене (z от -15.5 до -9.3)
    static readonly Vector2[] Stairs = { new Vector2(-33.8f, -29.7f), new Vector2(-9.6f, -5.5f), new Vector2(22.7f, 26.3f) };
    const float StairFrontZ = -9.3f, BackWallZ = -15.5f;
    const float BuildingMinX = -34f, BuildingMaxX = 26.5f;

    [MenuItem("Tools/Level/Place Floor Signs")]
    public static void Place()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Floor Signs");

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var mat = PaintMaterial(font);
        int placed = 0;

        for (int f = 0; f < FloorY.Length; f++)
        {
            string text = (f + 1) + " ЭТАЖ";
            foreach (var s in Stairs)
            {
                float cx = (s.x + s.y) * 0.5f;

                // В коридоре: на стене рядом с проёмом лестницы (справа, слева, чуть дальше)
                bool done = false;
                foreach (float off in new[] { 0.9f, 1.4f, 2f, 2.6f })
                {
                    foreach (float x in new[] { s.y + off, s.x - off })
                    {
                        if (x < BuildingMinX + 0.3f || x > BuildingMaxX - 0.3f) continue;
                        foreach (float startZ in new[] { StairFrontZ + 1.2f, StairFrontZ + 2.5f })
                        {
                            var from = new Vector3(x, FloorY[f] + 1.75f, startZ);
                            if (TryWall(from, Vector3.back, 3f, StairFrontZ - 0.8f, StairFrontZ + 0.8f, out var hit))
                            {
                                MakeSign(root.transform, text, hit, 0.32f, font, mat);
                                placed++;
                                done = true;
                                break;
                            }
                        }
                        if (done) break;
                    }
                    if (done) break;
                }

                // На лестничной клетке: на задней стене, а если её закрывает пролёт - на боковой стене
                done = false;
                foreach (float dx in new[] { 0f, -1f, 1f, -1.5f, 1.5f })
                {
                    var from = new Vector3(cx + dx, FloorY[f] + 1.9f, BackWallZ + 4f);
                    if (TryWall(from, Vector3.back, 5f, BackWallZ - 0.6f, BackWallZ + 0.9f, out var hit))
                    {
                        MakeSign(root.transform, text, hit, 0.42f, font, mat);
                        placed++;
                        done = true;
                        break;
                    }
                }
                if (!done)
                {
                    // не у самого угла проёма, иначе надпись заходит за край стены
                    foreach (float z in new[] { -11.3f, -11.9f, -12.5f, -13.1f })
                    {
                        var from = new Vector3(cx, FloorY[f] + 1.8f, z);
                        if (TrySideWall(from, Vector3.left, s.x, out var hit) || TrySideWall(from, Vector3.right, s.y, out hit))
                        {
                            MakeSign(root.transform, text, hit, 0.36f, font, mat);
                            placed++;
                            break;
                        }
                    }
                }
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[FloorSigns] placed {placed}");
    }

    // Луч до стены, которая смотрит навстречу лучу и стоит в нужном диапазоне по z
    static bool TryWall(Vector3 from, Vector3 dir, float dist, float zMin, float zMax, out RaycastHit hit)
    {
        if (!Physics.Raycast(from, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.point.z < zMin || hit.point.z > zMax) return false;
        return Vector3.Dot(hit.normal, -dir) > 0.9f;
    }

    // Боковая стена лестничной клетки: луч вбок должен упереться почти в её край по x
    static bool TrySideWall(Vector3 from, Vector3 dir, float wallX, out RaycastHit hit)
    {
        if (!Physics.Raycast(from, dir, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (Mathf.Abs(hit.point.x - wallX) > 0.7f) return false;
        return Vector3.Dot(hit.normal, -dir) > 0.9f;
    }

    static void MakeSign(Transform parent, string text, RaycastHit hit, float height, TMP_FontAsset font, Material mat)
    {
        var go = new GameObject("Sign " + text);
        go.transform.SetParent(parent, false);
        go.transform.position = hit.point + hit.normal * 0.012f;
        go.transform.rotation = Quaternion.LookRotation(-hit.normal);   // текст читается со стороны нормали

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font;
        tmp.fontSharedMaterial = mat;
        tmp.text = text;
        tmp.fontSize = height * 10f;          // в мировых TMP 1 единица размера ~ 0.1 м высоты строки
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.color = new Color(0.82f, 0.8f, 0.74f);
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
        tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Материал "краска на стене": освещаемый (HDRP Lit), чтобы надпись не светилась в темноте
    static Material PaintMaterial(TMP_FontAsset font)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;
        mat = new Material(font.material);
        mat.shader = Shader.Find("TextMeshPro/SRP/TMP_SDF-HDRP LIT");
        mat.name = "RubikDirt Wall Paint";
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
