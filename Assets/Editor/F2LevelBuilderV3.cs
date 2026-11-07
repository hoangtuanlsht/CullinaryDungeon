// Dựng lại toàn bộ tầng 2 (bản v3: 16 phòng lớn). Chạy: Tools > Culinaria > Build Floor 2 (v3).
// Dữ liệu sinh sẵn ở Assets/BackGround/SecondFloor/F2Data (tiles + objects), ảnh nền ở F2Rooms3.
// Địa hình rừng dùng main_lev_buildA (cỏ), tháp/phòng đá dùng gạch xám dungeontilesetfull,
// đạo cụ đá cổ dùng tileset.png (đuốc có hiệu ứng, cửa sổ, giá sách...).
// Bản cũ không bị xoá: các nhóm cũ được đổi tên thành *_old và tắt đi (chạy lại sẽ thay bản _old trước đó).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class F2LevelBuilderV3
{
    const string DataDir = "Assets/BackGround/SecondFloor/F2Data/";
    const string BgDir = "Assets/BackGround/SecondFloor/F2Rooms3/";
    const string DunTile = "Assets/TileMap/dungeontilesetfull 1_{0}.asset";
    const string ForestSheet = "Assets/Resouces/main_lev_buildA.png";
    const string ForestTileDir = "Assets/TileMap/F2_Forest";
    const string PropSheet = "Assets/Resouces/tileset.png";
    const string BushSprite = "Assets/Resouces/Background/background6.png";
    const string RoomPrefab = "Assets/Prefab/Room.prefab";
    const string EnemyDir = "Assets/Prefab/Enemy/";
    const string HealthBarPrefab = "Assets/Prefab/Canvas-HealthBar.prefab";

    static readonly string[] Groups = { "______Room_____", "Grid_F2", "F2_Background", "______EnemyF2______", "F2_Markers", "F2_Props" };

    static string sortLayer = "Default";
    static int baseOrder = 0;
    static int groundLayer = 6;

    [MenuItem("Tools/Culinaria/Build Floor 2 (v3)")]
    public static void Build()
    {
        // lấy layer + sorting từ tilemap đất hiện có (nếu còn)
        var g = GameObject.Find("Grid_F2/F2_Ground");
        if (g != null)
        {
            groundLayer = g.layer;
            var tr = g.GetComponent<TilemapRenderer>();
            if (tr != null) { sortLayer = tr.sortingLayerName; baseOrder = tr.sortingOrder; }
        }

        BackupOld();
        var lines = File.ReadAllLines(DataDir + "f2_v3_objects.txt");
        BuildRooms(lines);
        int missing = BuildTiles();
        BuildBackgrounds(lines);
        BuildMarkersAndProps(lines);
        int enemies = BuildEnemies(lines);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[F2 v3] Xong: 16 phòng, {enemies} quái. Tile thiếu: {missing}");
    }

    // ---------------------------------------------------------------- backup
    static void BackupOld()
    {
        foreach (var name in Groups)
        {
            // giữ bản _old đầu tiên (bản trước khi dựng v3); các lần chạy sau chỉ thay bản hiện tại
            var cur = FindRoot(name);
            if (cur == null) continue;
            if (FindRoot(name + "_old") != null) { Undo.DestroyObjectImmediate(cur); continue; }
            Undo.RecordObject(cur, "Backup F2");
            cur.name = name + "_old";
            cur.SetActive(false);
        }
    }

    static GameObject FindRoot(string name)
    {
        return EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(o => o.name == name);
    }

    static GameObject NewRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build F2 v3");
        return go;
    }

    // ---------------------------------------------------------------- rooms
    static void BuildRooms(string[] lines)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefab);
        var root = NewRoot("______Room_____");
        foreach (var l in lines.Where(s => s.StartsWith("ROOM ")))
        {
            var p = l.Split(' ');
            string id = p[1]; float x = F(p[2]), y = F(p[3]), w = F(p[4]), h = F(p[5]);
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            room.name = id;
            room.transform.position = new Vector3(x, y, 0);

            var poly = room.GetComponent<PolygonCollider2D>();
            poly.offset = Vector2.zero;
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(0, 0), new Vector2(0, h), new Vector2(w, h), new Vector2(w, 0) });

            var rc = room.GetComponent<Room>();
            if (rc != null)
            {
                var so = new SerializedObject(rc);
                var pid = so.FindProperty("roomId");
                if (pid != null) pid.stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var vcam = room.transform.Find("VCam_Room");
            if (vcam != null) vcam.localPosition = new Vector3(w / 2f, h / 2f, -10f);
        }
    }

    // ---------------------------------------------------------------- tiles
    static int BuildTiles()
    {
        var grid = NewRoot("Grid_F2");
        grid.AddComponent<Grid>();
        var maps = new Dictionary<string, Tilemap>
        {
            { "F2_DecorBack", MakeLayer(grid, "F2_DecorBack", 0, baseOrder - 1) },
            { "F2_Swamp", MakeLayer(grid, "F2_Swamp", 0, baseOrder) },
            { "F2_Ground", MakeLayer(grid, "F2_Ground", groundLayer, baseOrder) },
            { "F2_Platform", MakeLayer(grid, "F2_Platform", groundLayer, baseOrder) },
            { "F2_DecorFront", MakeLayer(grid, "F2_DecorFront", 0, baseOrder + 1) },
        };
        AddSolidCollider(maps["F2_Ground"].gameObject, false);
        AddSolidCollider(maps["F2_Platform"].gameObject, true);
        maps["F2_Swamp"].gameObject.tag = "DeadZone";
        maps["F2_Swamp"].gameObject.AddComponent<TilemapCollider2D>().isTrigger = true;

        var forestSprites = AssetDatabase.LoadAllAssetsAtPath(ForestSheet)
            .Concat(AssetDatabase.LoadAllAssetsAtPath("Assets/Resouces/main_lev_buildB.png"))
            .OfType<Sprite>().GroupBy(s => s.name).ToDictionary(gr => gr.Key, gr => gr.First());
        if (!AssetDatabase.IsValidFolder(ForestTileDir)) AssetDatabase.CreateFolder("Assets/TileMap", "F2_Forest");
        var cache = new Dictionary<string, TileBase>();
        int missing = 0;

        var pos = new Dictionary<string, List<Vector3Int>>();
        var til = new Dictionary<string, List<TileBase>>();
        string layer = null;
        foreach (var l in File.ReadAllLines(DataDir + "f2_v3_tiles.txt"))
        {
            if (l.Length == 0) continue;
            if (l.StartsWith("L ")) { layer = l.Substring(2); pos[layer] = new List<Vector3Int>(); til[layer] = new List<TileBase>(); continue; }
            var p = l.Split(' ');
            int x = int.Parse(p[0]), y = int.Parse(p[1]), n = int.Parse(p[2]);
            var t = GetTile(p[3], forestSprites, cache, ref missing);
            if (t == null) continue;
            for (int i = 0; i < n; i++) { pos[layer].Add(new Vector3Int(x + i, y, 0)); til[layer].Add(t); }
        }
        AssetDatabase.SaveAssets();
        foreach (var kv in pos)
        {
            maps[kv.Key].SetTiles(kv.Value.ToArray(), til[kv.Key].ToArray());
            maps[kv.Key].CompressBounds();
        }
        return missing;
    }

    static TileBase GetTile(string key, Dictionary<string, Sprite> forest, Dictionary<string, TileBase> cache, ref int missing)
    {
        if (cache.TryGetValue(key, out var t)) return t;
        if (key.StartsWith("D:"))
            t = AssetDatabase.LoadAssetAtPath<TileBase>(string.Format(DunTile, key.Substring(2)));
        else
        {
            string sn = key.Substring(2);
            string path = ForestTileDir + "/" + sn + ".asset";
            t = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (t == null && forest.TryGetValue(sn, out var sp))
            {
                var nt = ScriptableObject.CreateInstance<Tile>();
                nt.sprite = sp;
                nt.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(nt, path);
                t = nt;
            }
        }
        if (t == null) { missing++; Debug.LogWarning("[F2 v3] Thiếu tile " + key); }
        cache[key] = t;
        return t;
    }

    static Tilemap MakeLayer(GameObject grid, string name, int layer, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);
        go.layer = layer;
        var r = go.GetComponent<TilemapRenderer>();
        r.sortingLayerName = sortLayer;
        r.sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    static void AddSolidCollider(GameObject go, bool oneWay)
    {
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var tc = go.AddComponent<TilemapCollider2D>();
#if UNITY_2023_1_OR_NEWER
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
        tc.usedByComposite = true;
#endif
        var comp = go.AddComponent<CompositeCollider2D>();
        if (oneWay)
        {
            var eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = 160f;
            comp.usedByEffector = true;
        }
    }

    // ---------------------------------------------------------------- backgrounds
    static void BuildBackgrounds(string[] lines)
    {
        var root = NewRoot("F2_Background");
        foreach (var l in lines.Where(s => s.StartsWith("BG ")))
        {
            var p = l.Split(' ');
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(BgDir + p[1] + ".png");
            if (sp == null) { Debug.LogWarning("[F2 v3] Thiếu ảnh nền " + p[1]); continue; }
            float t = F(p[6]);
            Fit(root.transform, "BG_" + p[1].Replace("_bg", ""), sp, F(p[2]), F(p[3]), F(p[4]), F(p[5]),
                new Color(t, t, t, 1f), baseOrder - 20, false);
        }

        // lớp bụi cây tối (background6) chạy dọc chân các phòng rừng, tạo chiều sâu
        var bush = AssetDatabase.LoadAssetAtPath<Sprite>(BushSprite);
        if (bush == null) return;
        var bushRoot = new GameObject("BushLayer");
        bushRoot.transform.SetParent(root.transform);
        float aspect = bush.bounds.size.x / bush.bounds.size.y;
        foreach (var l in lines.Where(s => s.StartsWith("BUSH ")))
        {
            var p = l.Split(' ');
            float x0 = F(p[1]), y0 = F(p[2]), w = F(p[3]);
            float bh = 16f, bw = bh * aspect;
            int n = Mathf.CeilToInt(w / bw);
            float step = w / n;                              // giãn nhẹ cho khít phòng
            for (int i = 0; i < n; i++)
                Fit(bushRoot.transform, "Bush", bush, x0 + i * step, y0, step, bh,
                    new Color(0.75f, 0.85f, 0.75f, 0.9f), baseOrder - 15, i % 2 == 1);
        }
    }

    static GameObject Fit(Transform parent, string name, Sprite s, float x, float y, float w, float h, Color c, int order, bool flip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.color = c; sr.flipX = flip;
        sr.sortingLayerName = sortLayer; sr.sortingOrder = order;
        var size = s.bounds.size;
        go.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        go.transform.position = new Vector3(x + w / 2f, y + h / 2f, 0f) - Vector3.Scale(s.bounds.center, go.transform.localScale);
        return go;
    }

    // ---------------------------------------------------------------- markers + props
    static void BuildMarkersAndProps(string[] lines)
    {
        var mk = NewRoot("F2_Markers");
        foreach (var l in lines.Where(s => s.StartsWith("MARK ")))
        {
            var p = l.Split(' ');
            var m = new GameObject(p[1]);
            m.transform.SetParent(mk.transform);
            m.transform.position = new Vector3(F(p[2]), F(p[3]), 0);
        }

        var sprites = AssetDatabase.LoadAllAssetsAtPath(PropSheet).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite S(string n) => sprites.TryGetValue(n, out var s) ? s : null;
        var torchFrames = new[] { S("g_torch0"), S("g_torch1"), S("g_torch2"), S("g_torch3") }.Where(s => s != null).ToArray();
        var root = NewRoot("F2_Props");
        int k = 0;
        foreach (var l in lines.Where(s => s.StartsWith("PROP ")))
        {
            var p = l.Split(' ');
            string kind = p[1]; float x = F(p[2]), y = F(p[3]);
            Sprite sp = kind switch
            {
                "torch" => torchFrames.FirstOrDefault(),
                "window" => S("g_archWindow"),
                "bookshelf" => S("g_bookshelf" + (k++ % 4)),
                "candles" => S("g_floorCandles"),
                "pillar" => S("g_pillar"),
                "statue" => S("g_statue1"),
                _ => null
            };
            if (sp == null) { Debug.LogWarning("[F2 v3] Thiếu sprite đạo cụ " + kind); continue; }
            var go = new GameObject(kind);
            go.transform.SetParent(root.transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingLayerName = sortLayer; sr.sortingOrder = baseOrder - 2;
            // đáy sprite đặt đúng mặt đất (y), căn giữa ô x
            go.transform.position = new Vector3(x + 0.5f - sp.bounds.center.x, y - sp.bounds.min.y, 0f);
            if (kind == "torch" && torchFrames.Length > 1)
                go.AddComponent<SpriteFrameAnimator>().Setup(torchFrames, 8f);
        }
    }

    // ---------------------------------------------------------------- enemies
    static int BuildEnemies(string[] lines)
    {
        var root = NewRoot("______EnemyF2______");
        var hbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefab);
        int i = 0;
        foreach (var l in lines.Where(s => s.StartsWith("ENEMY ")))
        {
            var p = l.Split(' ');
            string room = p[1], type = p[2];
            float x = F(p[3]), feet = F(p[4]), wl = F(p[5]), wr = F(p[6]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDir + type + ".prefab");
            if (prefab == null) { Debug.LogWarning("[F2 v3] Thiếu prefab " + type); continue; }
            i++;
            var group = new GameObject($"{room}_{type}_{i:00}");
            group.transform.SetParent(root.transform);

            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            enemy.name = type;
            enemy.transform.position = new Vector3(x, feet + 1f, 0f);

            if (hbPrefab != null)
            {
                var hb = (GameObject)PrefabUtility.InstantiatePrefab(hbPrefab, group.transform);
                hb.name = "HealthBar_" + type;
                hb.transform.position = enemy.transform.position + Vector3.up;
                var ch = enemy.GetComponent<Character>();
                if (ch != null)
                {
                    var so = new SerializedObject(ch);
                    var prop = so.FindProperty("healthBar");
                    if (prop != null) { prop.objectReferenceValue = hb.GetComponent<HealthBar>(); so.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }
            MakeWall(group.transform, "EnemyWall_L", wl, feet);
            MakeWall(group.transform, "EnemyWall_R", wr, feet);
        }
        return i;
    }

    static void MakeWall(Transform parent, string name, float x, float feetY)
    {
        var w = new GameObject(name);
        w.tag = "EnemyWall";
        w.transform.SetParent(parent);
        w.transform.position = new Vector3(x, feetY + 1.5f, 0f);
        var col = w.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.5f, 3f);
    }

    static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
#endif
