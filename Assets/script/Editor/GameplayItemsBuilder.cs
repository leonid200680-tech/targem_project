using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Tools > Level > Build Gameplay Items: бутылки (бросать в бабку), линза для фонарика, лишние батарейки,
// модели камер наблюдения. Всё ставится по NavMesh и геометрии здания; повторный запуск пересобирает.
public static class GameplayItemsBuilder
{
    const string RootName = "Placed Items";
    const string ModelsDir = "Assets/models";

    static readonly float[] FloorY = { 3.73f, 7.84f, 11.99f };
    static readonly Rect Building = Rect.MinMaxRect(-33f, -14.8f, 25.5f, 4.6f);
    static readonly Rect Trailer = Rect.MinMaxRect(-27.2f, -37.9f, -21.6f, -34.8f);

    [MenuItem("Tools/Level/Build Gameplay Items")]
    public static void Build()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Gameplay Items");
        Random.InitState(1666);

        var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        var flashlight = player.GetComponent<Flashlight>();
        var inventoryUI = flashlight.inventoryUI;
        var cam = player.cameraTransform;

        // ---------- Бутылка ----------
        var bottleMat = Lit(ModelsDir + "/Bottle/BottleMat.mat", Color.white, 0.75f,
            ModelsDir + "/Bottle/textures/molotov_LP_DefaultMaterial_BaseColor.png", ModelsDir + "/Bottle/textures/molotov_LP_DefaultMaterial_Normal.png");
        var shardMat = Lit(ModelsDir + "/Bottle/GlassShard.mat", new Color(0.22f, 0.15f, 0.07f), 0.92f, null, null);
        var bottleFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Bottle/source/Beer_Bottle.fbx");
        const float bottleScale = 0.04f;   // в модели бутылка 6.4 м высотой

        var bottleItem = Item("Assets/Items/Bottle.asset", "Бутылка", ItemType.Bottle, 3,
            "Пыльная пивная бутылка. Если бросить в неё - замедлит. Ненадолго.", null);
        var lensItem = Item("Assets/Items/Lens.asset", "Линза", ItemType.Lens, 1,
            "Линза от прожектора. Если надеть на фонарик, он будет светить дальше.", null);

        // Снаряд
        string projPath = ModelsDir + "/Bottle/BottleProjectile.prefab";
        var proj = new GameObject("Bottle Projectile");
        AddModel(proj.transform, bottleFbx, bottleScale, bottleMat, false);
        var capsule = proj.AddComponent<CapsuleCollider>();
        capsule.radius = 0.035f; capsule.height = 0.26f; capsule.center = new Vector3(0f, 0.12f, 0f);
        var rb = proj.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var bp = proj.AddComponent<BottleProjectile>();
        bp.shardMaterial = shardMat;
        var projPrefab = PrefabUtility.SaveAsPrefabAsset(proj, projPath).GetComponent<BottleProjectile>();
        Object.DestroyImmediate(proj);

        // Бутылки, которые можно подобрать
        var bottleSpots = new List<Vector3>();
        bottleSpots.Add(TrailerSpot(new Vector3(-26.6f, 0f, -37.5f)));
        bottleSpots.AddRange(FloorSpots(0, 1, 11));
        bottleSpots.AddRange(FloorSpots(1, 2, 21));
        bottleSpots.AddRange(FloorSpots(2, 1, 31));
        int i = 0;
        foreach (var p in bottleSpots)
        {
            var go = new GameObject("Bottle Pickup " + (++i));
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            AddModel(go.transform, bottleFbx, bottleScale, bottleMat, false);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.08f; col.height = 0.3f; col.center = new Vector3(0f, 0.13f, 0f);
            var pick = go.AddComponent<PickupItem>();
            pick.item = bottleItem;
            pick.prompt = "Нажмите E, чтобы подобрать бутылку";
        }

        // Бутылка в руке
        var oldHolder = cam.Find("BottleHolder");
        if (oldHolder != null) Object.DestroyImmediate(oldHolder.gameObject);
        var holder = new GameObject("BottleHolder");
        holder.transform.SetParent(cam, false);
        var ik = Object.FindAnyObjectByType<PlayerHandsIK>(FindObjectsInactive.Include);
        // Место в кадре - рядом с зажигалкой
        var lighterHeld = ik.items[0];
        holder.transform.position = lighterHeld.item.position;
        holder.transform.rotation = cam.rotation;
        var handModel = AddModel(holder.transform, bottleFbx, bottleScale, bottleMat, true);
        // модель стоит вертикально, как в FBX (свой поворот FBX не трогаем)
        var handMesh = handModel.GetComponentInChildren<MeshFilter>();
        holder.SetActive(false);

        var throwComp = player.GetComponent<BottleThrow>() ?? player.gameObject.AddComponent<BottleThrow>();
        throwComp.bottleItem = bottleItem;
        throwComp.inventoryUI = inventoryUI;
        throwComp.holder = holder;
        throwComp.projectilePrefab = projPrefab;
        EditorUtility.SetDirty(throwComp);

        // Рука держит бутылку за горлышко (в PlayerHandsIK - третий предмет)
        var list = new List<PlayerHandsIK.HeldItem>(ik.items);
        list.RemoveAll(x => x.holder == null || x.holder.name == "BottleHolder");
        list.Add(new PlayerHandsIK.HeldItem
        {
            holder = holder,
            item = handMesh.transform,
            itemAxis = new Vector3(0f, 1f, 0.2f),
            longAxisLocal = LongAxis(handMesh.sharedMesh.bounds),
            gripAlong = 0.07f,
            round = true,
            thumbCurl = 0.7f,
        });
        ik.items = list.ToArray();
        EditorUtility.SetDirty(ik);

        // ---------- Линза ----------
        var lensFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/Lens/lens/lens.fbx");
        var holderMat = Lit(ModelsDir + "/Lens/LensHolder.mat", Color.white, 0.5f,
            ModelsDir + "/Lens/lens/textures/am_lasertubes_lenseholder.png", ModelsDir + "/Lens/lens/textures/am_lasertubes_lenseholder_norm.png");
        var glassMat = Lit(ModelsDir + "/Lens/LensGlass.mat", new Color(0.55f, 0.75f, 0.85f), 0.95f, null, null);
        const float lensScale = 0.12f;   // в модели 0.5 м -> 6 см

        var lensSpot = FloorSpots(2, 1, 77)[0];
        var lensPick = new GameObject("Lens Pickup");
        lensPick.transform.SetParent(root.transform, false);
        lensPick.transform.position = lensSpot;
        var lp = AddModel(lensPick.transform, lensFbx, lensScale, null, false);
        SetLensMaterials(lp, holderMat, glassMat);
        lp.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * lp.transform.localRotation;   // лежит плашмя
        Ground(lp, lensPick.transform);
        var lc = lensPick.AddComponent<SphereCollider>();
        lc.radius = 0.12f; lc.center = new Vector3(0f, 0.04f, 0f);
        var lensPickup = lensPick.AddComponent<PickupItem>();
        lensPickup.item = lensItem;
        lensPickup.prompt = "Нажмите E, чтобы взять линзу";

        // Линза на головке фонарика в руке
        var flHeld = ik.items[1].item;
        var oldLens = flHeld.Find("Lens On Flashlight");
        if (oldLens != null) Object.DestroyImmediate(oldLens.gameObject);
        var flBounds = flHeld.GetComponent<MeshFilter>().sharedMesh.bounds;
        var onFl = AddModel(flHeld, lensFbx, 1f, null, false);
        onFl.name = "Lens On Flashlight";
        SetLensMaterials(onFl, holderMat, glassMat);
        onFl.transform.localPosition = flBounds.center + Vector3.forward * (flBounds.extents.z + 0.001f);
        onFl.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * onFl.transform.localRotation;
        // размер: диаметр линзы = диаметр головки
        float headD = Mathf.Max(flBounds.size.x, flBounds.size.y);
        var lensMf = onFl.GetComponentInChildren<MeshFilter>();
        var lensSkin = onFl.GetComponentInChildren<SkinnedMeshRenderer>();
        var lensMeshBounds = lensMf != null ? lensMf.sharedMesh.bounds : lensSkin.sharedMesh.bounds;
        onFl.transform.localScale = Vector3.one * (headD * 1.05f / Mathf.Max(lensMeshBounds.size.y, lensMeshBounds.size.z));
        var lensComp = player.GetComponent<FlashlightLens>() ?? player.gameObject.AddComponent<FlashlightLens>();
        lensComp.lensItem = lensItem;
        lensComp.lensModel = onFl;
        EditorUtility.SetDirty(lensComp);

        // ---------- Батарейки ----------
        var battery = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PickupItem batteryTemplate = null;
        foreach (var b in battery) if (b.item != null && b.item.type == ItemType.Battery && b.transform.parent?.name != RootName) batteryTemplate = b;
        if (batteryTemplate != null)
        {
            var spots = new List<Vector3>();
            spots.Add(TrailerSpot(new Vector3(-24.5f, 0f, -37.6f)));
            spots.AddRange(FloorSpots(0, 2, 41));
            spots.AddRange(FloorSpots(1, 3, 51));
            spots.AddRange(FloorSpots(2, 2, 61));
            int k = 0;
            foreach (var p in spots)
            {
                var b = Object.Instantiate(batteryTemplate.gameObject, root.transform);
                b.name = "Battery Extra " + (++k);
                // модель батарейки - на полу: сдвигаем так, чтобы низ касался точки
                b.transform.rotation = Quaternion.Euler(batteryTemplate.transform.eulerAngles.x, Random.Range(0f, 360f), batteryTemplate.transform.eulerAngles.z);
                b.transform.position = p;
                var r = b.GetComponentInChildren<Renderer>();
                if (r != null) b.transform.position += Vector3.up * (p.y - r.bounds.min.y + 0.002f);
                b.GetComponent<PickupItem>().prompt = "Нажмите E, чтобы подобрать батарейку";
            }
        }

        // ---------- Камеры наблюдения ----------
        BuildCctvModels(root.transform);

        // ---------- Бабка замедляется от бутылок ----------
        var hm = Object.FindAnyObjectByType<HallucinationManager>(FindObjectsInactive.Include);
        if (hm != null && hm.realGhost != null && hm.realGhost.GetComponent<GrannySlow>() == null)
            hm.realGhost.AddComponent<GrannySlow>();

        // Иконки
        bottleItem.icon = RenderIcon(bottleFbx, bottleMat, null, "Assets/Icons/Bottle_icon.png", Quaternion.Euler(0f, 0f, -35f));
        lensItem.icon = RenderIcon(lensFbx, holderMat, glassMat, "Assets/Icons/Lens_icon.png", Quaternion.Euler(10f, 30f, 0f));
        EditorUtility.SetDirty(bottleItem);
        EditorUtility.SetDirty(lensItem);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Items] bottles {bottleSpots.Count}, lens at {lensSpot}");
    }

    // ---------- Места ----------

    // Точки на полу этажа у стен (там предметы лежат естественнее), подальше друг от друга
    static List<Vector3> FloorSpots(int floor, int count, int seed)
    {
        Random.InitState(seed);
        var result = new List<Vector3>();
        for (int tries = 0; tries < 2000 && result.Count < count; tries++)
        {
            // Сначала столы и коробки на виду; пол - только если их не хватило
            if (tries < 1600)
            {
                if (SurfaceSpot(floor, out var top))
                {
                    bool apart = true;
                    foreach (var r in result) if (Vector3.Distance(r, top) < 4f) apart = false;
                    foreach (var pi in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None)) if (Vector3.Distance(pi.transform.position, top) < 1f) apart = false;
                    if (apart) result.Add(top);
                }
                continue;
            }
            var p = new Vector3(Random.Range(Building.xMin, Building.xMax), FloorY[floor] + 0.5f, Random.Range(Building.yMin, Building.yMax));
            if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - FloorY[floor]) > 0.25f) continue;
            if (!NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas)) continue;
            Vector3 q = edge.position + edge.normal * 0.15f;
            if (!Physics.Raycast(q + Vector3.up * 0.6f, Vector3.down, out var ground, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            q = ground.point;
            if (Mathf.Abs(q.y - FloorY[floor]) > 0.25f) continue;
            // не в лестничных клетках
            if (q.z < -9f && (q.x < -29.5f || (q.x > -9.8f && q.x < -5.3f) || q.x > 22.5f)) continue;
            bool far = true;
            foreach (var r in result) if (Vector3.Distance(r, q) < 12f) far = false;
            foreach (var pi in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None)) if (Vector3.Distance(pi.transform.position, q) < 2f) far = false;
            if (far) result.Add(q);
        }
        return result;
    }

    // Случайная точка на столе, коробке или полке, которую видно с высоты глаз игрока
    static bool SurfaceSpot(int floor, out Vector3 point)
    {
        point = default;
        float x = Random.Range(Building.xMin, Building.xMax), z = Random.Range(Building.yMin, Building.yMax);
        if (z < -9f && (x < -29.5f || (x > -9.8f && x < -5.3f) || x > 22.5f)) return false;   // лестничные клетки
        var from = new Vector3(x, FloorY[floor] + 2.2f, z);
        if (!Physics.Raycast(from, Vector3.down, out var hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.97f) return false;
        float height = hit.point.y - FloorY[floor];
        if (height < 0.4f || height > 1.5f || hit.collider.GetComponentInParent<PickupItem>() != null) return false;
        string name = hit.collider.name.ToLower();
        bool furniture = false;
        foreach (var word in new[] { "стол", "table", "короб", "desk", "полк", "shelf", "box", "тумб" }) furniture |= name.Contains(word);
        if (!furniture) return false;
        foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            if (!Physics.Raycast(from + d * 0.12f, Vector3.down, out var side, 2.2f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(side.point.y - hit.point.y) > 0.01f) return false;
        if (!NavMesh.SamplePosition(new Vector3(x, FloorY[floor], z), out var nav, 1.1f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y - FloorY[floor]) > 0.3f) return false;
        if (Physics.Linecast(nav.position + Vector3.up * 1.6f, hit.point + Vector3.up * 0.1f, ~0, QueryTriggerInteraction.Ignore)) return false;
        point = hit.point;
        return true;
    }

    static Vector3 TrailerSpot(Vector3 near)
    {
        if (Physics.Raycast(near + Vector3.up * 2.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return near;
    }

    // ---------- Камеры наблюдения ----------

    // Корпус камеры с объективом, красным огоньком и кронштейном к стене или потолку
    static void BuildCctvModels(Transform root)
    {
        var body = Lit(ModelsDir + "/CCTVCamera/CamBody.mat", new Color(0.78f, 0.78f, 0.75f), 0.35f, null, null);
        var dark = Lit(ModelsDir + "/CCTVCamera/CamDark.mat", new Color(0.05f, 0.05f, 0.06f), 0.6f, null, null);
        var glass = Lit(ModelsDir + "/CCTVCamera/CamGlass.mat", new Color(0.02f, 0.03f, 0.05f), 0.97f, null, null);
        var led = Lit(ModelsDir + "/CCTVCamera/CamLed.mat", new Color(0.9f, 0.05f, 0.03f), 0.5f, null, null);
        led.EnableKeyword("_EMISSIVE_COLOR_MAP");
        led.SetColor("_EmissiveColor", new Color(1f, 0.05f, 0.02f) * 40f);
        led.SetFloat("_UseEmissiveIntensity", 0f);
        EditorUtility.SetDirty(led);

        foreach (var c in Object.FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var t = c.transform;
            var m = new GameObject("CCTV Model " + c.label).transform;
            m.SetParent(root, false);
            m.SetPositionAndRotation(t.position, t.rotation);
            // Всё - позади точки съёмки, чтобы корпус не попадал в кадр самой камеры
            Part(m, PrimitiveType.Cube, new Vector3(0f, 0.01f, -0.16f), new Vector3(0.11f, 0.1f, 0.26f), Vector3.zero, body, "Housing");
            Part(m, PrimitiveType.Cube, new Vector3(0f, 0.07f, -0.13f), new Vector3(0.14f, 0.012f, 0.32f), Vector3.zero, body, "Sunshield");
            Part(m, PrimitiveType.Cylinder, new Vector3(0f, 0.005f, -0.025f), new Vector3(0.07f, 0.012f, 0.07f), new Vector3(90f, 0f, 0f), dark, "Lens Ring");
            Part(m, PrimitiveType.Cylinder, new Vector3(0f, 0.005f, -0.018f), new Vector3(0.05f, 0.004f, 0.05f), new Vector3(90f, 0f, 0f), glass, "Lens");
            Part(m, PrimitiveType.Sphere, new Vector3(0.04f, -0.025f, -0.03f), Vector3.one * 0.012f, Vector3.zero, led, "Led");

            // Кронштейн: к потолку, если он близко, иначе к стене позади
            Vector3 back = m.position - m.forward * 0.3f;
            if (Physics.Raycast(back, Vector3.up, out var up, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                float h = up.distance;
                var arm = Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.025f, h * 0.5f, 0.025f), Vector3.zero, dark, "Bracket " + c.label);
                arm.position = back + Vector3.up * h * 0.5f;
                Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.02f, 0.1f), Vector3.zero, dark, "Mount " + c.label).position = up.point - Vector3.up * 0.01f;
            }
            else if (Physics.Raycast(back, -Vector3.ProjectOnPlane(m.forward, Vector3.up).normalized, out var wall, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 a = back, b = wall.point;
                var arm = Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.025f, Vector3.Distance(a, b) * 0.5f, 0.025f), Vector3.zero, dark, "Bracket " + c.label);
                arm.position = (a + b) * 0.5f;
                arm.up = (b - a).normalized;
                var plate = Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.1f, 0.12f, 0.02f), Vector3.zero, dark, "Mount " + c.label);
                plate.position = b + wall.normal * 0.01f;
                plate.rotation = Quaternion.LookRotation(wall.normal);
            }
        }
    }

    static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, string name)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g.transform;
    }

    // ---------- Общее ----------

    static GameObject AddModel(Transform parent, GameObject fbx, float scale, Material mat, bool handHeld)
    {
        var m = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        m.transform.SetParent(parent, false);
        m.transform.localScale *= scale;   // свой масштаб FBX сохраняем
        foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        if (mat != null) foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        if (handHeld) foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // У моделей смещён пивот: ставим низ модели в точку родителя, по центру
        var rs = m.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            m.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y, parent.position.z - b.center.z);
        }
        return m;
    }

    static void Ground(GameObject m, Transform parent)
    {
        var rs = m.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        m.transform.position += new Vector3(parent.position.x - b.center.x, parent.position.y - b.min.y + 0.002f, parent.position.z - b.center.z);
    }

    static void SetLensMaterials(GameObject lens, Material holder, Material glass)
    {
        foreach (var r in lens.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mats[i] != null && mats[i].name.Contains("lense.") ? glass : holder;
            r.sharedMaterials = mats;
        }
    }

    static Vector3 LongAxis(Bounds b)
    {
        Vector3 e = b.extents;
        if (e.x >= e.y && e.x >= e.z) return Vector3.right;
        return e.y >= e.z ? Vector3.up : Vector3.forward;
    }

    static Material Lit(string path, Color color, float smooth, string baseMap, string normalMap)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (baseMap != null) m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
        if (normalMap != null) { m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap)); m.EnableKeyword("_NORMALMAP"); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static ItemData Item(string path, string name, ItemType type, int stack, string desc, Sprite icon)
    {
        var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (it == null) { it = ScriptableObject.CreateInstance<ItemData>(); AssetDatabase.CreateAsset(it, path); }
        it.itemName = name; it.type = type; it.maxStack = stack; it.description = desc;
        if (icon != null) it.icon = icon;
        EditorUtility.SetDirty(it);
        return it;
    }

    // Иконка для инвентаря: модель на прозрачном фоне, сохраняется в PNG
    static Sprite RenderIcon(GameObject fbx, Material mat, Material mat2, string path, Quaternion rot)
    {
        var stage = new GameObject("IconStage");
        stage.transform.position = new Vector3(0f, -500f, 0f);
        var model = AddModel(stage.transform, fbx, 1f, mat, false);
        if (mat2 != null) SetLensMaterials(model, mat, mat2);
        model.transform.localRotation = rot * model.transform.localRotation;
        var rs = model.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        model.transform.position += stage.transform.position - b.center;
        float size = b.extents.magnitude;

        var camGo = new GameObject("IconCam");
        var cam = camGo.AddComponent<Camera>();
        var hd = camGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
        hd.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = new Color(0f, 0f, 0f, 0f);
        hd.volumeLayerMask = 0;
        cam.orthographic = true;
        cam.orthographicSize = size * 1.05f;
        cam.transform.position = stage.transform.position + Vector3.back * size * 4f;
        cam.nearClipPlane = 0.01f; cam.farClipPlane = size * 10f;
        var lightGo = new GameObject("IconLight");
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 6f;
        lightGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        lightGo.transform.rotation = Quaternion.Euler(30f, -30f, 0f);

        var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(stage);

        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
