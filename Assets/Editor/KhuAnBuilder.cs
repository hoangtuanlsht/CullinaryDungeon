using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Greybox các khu ẩn chứa Mảnh Sự Thật (tài liệu "Thiet-Ke-Khu-An-Manh-Su-That.md").
///  - Mỗi scene: mọi thứ mới nằm dưới root "______KhuAn_____" (xóa và dựng lại mỗi lần chạy).
///  - Chỉ SỬA tilemap có sẵn ở đúng các ô cần đục (đường hầm, lỗ sàn, lòng phòng ẩn). Không chạy lại builder tầng,
///    không đụng RoomGlow / TunnelGlow / RoomGate.
///  - Sao lưu scene một lần trước lần dựng đầu: Assets/Scenes/Backup/<scene>_truocKhuAn.unity
///  - Phòng ẩn đặt ngoài mọi phòng có sẵn; nếu khung phòng ẩn đè lên phòng cũ thì BỎ QUA tầng đó và báo.
/// Chạy: Tools > Culinaria > Khu An > Build tat ca khu an. Chạy lại được.
public static class KhuAnBuilder
{
    const string RootName = "______KhuAn_____";
    const string TilePath = "Assets/BackGround/SixthFloor/F6_Greybox/F6_Block.asset";
    const float BgDepth = 18.7f;
    static Tile tile; static Sprite blockSprite; static Material unlit;
    static readonly List<string> log = new List<string>();

    // ===================== NGỮ CẢNH MỘT TẦNG =====================
    class Ctx
    {
        public Scene scene; public string tag; public Color tint;
        public List<Tilemap> solidMaps = new List<Tilemap>();
        public List<Tilemap> theirMaps = new List<Tilemap>();
        public List<(string name, Bounds b)> theirRooms = new List<(string, Bounds)>();
        public HashSet<Vector2Int> carve = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> fill = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> plat = new HashSet<Vector2Int>();
        public HashSet<Vector2Int> dead = new HashSet<Vector2Int>();
        public List<(string id, RectInt r)> rooms = new List<(string, RectInt)>();
        public List<System.Action<Transform>> build = new List<System.Action<Transform>>();
        public bool failed;
        public Transform root;
    }

    static Ctx Begin(Scene s, string tag, Color tint)
    {
        var c = new Ctx { scene = s, tag = tag, tint = tint };
        foreach (var r in s.GetRootGameObjects())
        {
            if (r.name == RootName) continue;
            foreach (var tm in r.GetComponentsInChildren<Tilemap>(false))
            {
                var col = tm.GetComponent<TilemapCollider2D>();
                if (col != null && !col.isTrigger) c.theirMaps.Add(tm);
            }
            foreach (var room in r.GetComponentsInChildren<global::Room>(false))
            {
                var col = room.GetComponent<Collider2D>();
                if (col != null) c.theirRooms.Add((room.name, col.bounds));
            }
        }
        c.solidMaps = c.theirMaps.Where(t => !t.TryGetComponent<PlatformEffector2D>(out _) && !t.name.Contains("Plat")).ToList();
        ResetFromBackup(c);
        return c;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    /// Trước khi dựng lại: trả mọi tilemap va chạm của tầng trong vùng khu ẩn cũ (+20 ô) về đúng bản sao lưu trước khu ẩn,
    /// để chỗ đục sai ở lần trước được lấp lại và tường do bước trang trí chuyển sang bị gỡ. Lần dựng đầu (chưa có khu ẩn) thì bỏ qua.
    static void ResetFromBackup(Ctx c)
    {
        var root = c.scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        var roomsT = root != null ? root.transform.Find("Rooms") : null;
        if (roomsT == null || roomsT.childCount == 0) return;
        string bk = $"Assets/Scenes/Backup/{c.scene.name}_truocKhuAn.unity";
        if (!System.IO.File.Exists(bk)) { log.Add($"[{c.tag}] Không có bản sao lưu để trả vùng cũ"); return; }
        Bounds bb = roomsT.GetChild(0).GetComponent<Collider2D>().bounds;
        foreach (Transform t in roomsT) bb.Encapsulate(t.GetComponent<Collider2D>().bounds);
        var region = new RectInt(Mathf.FloorToInt(bb.min.x) - 20, Mathf.FloorToInt(bb.min.y) - 20, Mathf.CeilToInt(bb.size.x) + 40, Mathf.CeilToInt(bb.size.y) + 40);
        var active = SceneManager.GetActiveScene();
        var b = EditorSceneManager.OpenScene(bk, OpenSceneMode.Additive);
        int n = 0;
        try
        {
            var src = b.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).ToList();
            foreach (var tm in c.theirMaps)
            {
                string path = PathOf(tm.transform);
                var s = src.FirstOrDefault(t => PathOf(t.transform) == path);
                if (s == null) continue;
                for (int x = region.xMin; x < region.xMax; x++)
                    for (int y = region.yMin; y < region.yMax; y++)
                    {
                        var p = new Vector3Int(x, y, 0);
                        var t0 = s.GetTile(p); var t1 = tm.GetTile(p);
                        if (t0 == null && t1 == null) continue;
                        if (t0 == null) { tm.SetTile(p, null); n++; continue; }
                        var m0 = s.GetTransformMatrix(p); var c0 = s.GetColor(p);
                        if (t1 == t0 && tm.GetTransformMatrix(p) == m0 && tm.GetColor(p) == c0) continue;
                        tm.SetTile(p, t0); tm.SetTileFlags(p, TileFlags.None); tm.SetTransformMatrix(p, m0); tm.SetColor(p, c0); n++;
                    }
                EditorUtility.SetDirty(tm);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(b, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
        Physics2D.SyncTransforms();
        log.Add($"[{c.tag}] Trả {n} ô tile quanh khu ẩn cũ về bản sao lưu.");
    }

    static Bounds RoomBounds(Ctx c, string name)
    {
        var f = c.theirRooms.FirstOrDefault(r => r.name == name);
        if (f.name == null) { log.Add($"[{c.tag}] KHÔNG thấy phòng {name}"); c.failed = true; }
        return f.b;
    }

    static readonly List<Collider2D> hits = new List<Collider2D>();
    static bool Solid(Ctx c, int x, int y)
    {
        var p = new Vector3Int(x, y, 0);
        if (c.carve.Contains(new Vector2Int(x, y))) return false;
        if (c.solidMaps.Any(t => t.HasTile(p))) return true;
        var f = new ContactFilter2D { useTriggers = false };
        f.SetLayerMask(LayerMask.GetMask("Ground"));                 // bậc một chiều KHÔNG tính là sàn
        hits.Clear();
        Physics2D.OverlapPoint(new Vector2(x + 0.5f, y + 0.5f), f, hits);
        return hits.Any(h => h.gameObject.scene == c.scene && h.transform.root.name != RootName   // bỏ qua khối của khu ẩn cũ
                          && h.GetComponent<TilemapCollider2D>() == null && h.GetComponent<CompositeCollider2D>() == null);
    }

    /// Hàng đứng thấp nhất trong phòng tại cột x (ô trống, ô dưới đặc, 3 ô trống phía trên).
    static int FloorAt(Ctx c, Bounds b, int x)
    {
        for (int y = Mathf.FloorToInt(b.min.y); y < Mathf.CeilToInt(b.max.y) - 3; y++)
            if (!Solid(c, x, y) && Solid(c, x, y - 1) && !Solid(c, x, y + 1) && !Solid(c, x, y + 2)) return y;
        log.Add($"[{c.tag}] Không tìm được mặt sàn ở cột x={x}"); c.failed = true;
        return Mathf.FloorToInt(b.min.y) + 1;
    }

    // ---------- hình khối ----------
    static void AddRoom(Ctx c, string id, int x, int y, int w, int h)
    {
        var r = new RectInt(x, y, w, h);
        foreach (var (name, b) in c.theirRooms)
        {
            var inner = new Rect(b.min.x + 1, b.min.y + 1, b.size.x - 2, b.size.y - 2);
            if (inner.Overlaps(new Rect(x, y, w, h))) { log.Add($"[{c.tag}] {id} {r} ĐÈ lên phòng {name} → bỏ qua tầng này"); c.failed = true; }
        }
        c.rooms.Add((id, r));
        for (int i = 0; i < w; i++) for (int j = 0; j < h; j++)
        {
            var p = new Vector2Int(x + i, y + j);
            if (i == 0 || j == 0 || i == w - 1 || j == h - 1) c.fill.Add(p); else c.carve.Add(p);
        }
    }
    static void Carve(Ctx c, int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) c.carve.Add(new Vector2Int(x + i, y + j)); }
    static void Fill(Ctx c, int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) { var p = new Vector2Int(x + i, y + j); c.carve.Remove(p); c.fill.Add(p); } }
    static void Plat(Ctx c, int x, int y, int w) { for (int i = 0; i < w; i++) c.plat.Add(new Vector2Int(x + i, y)); }

    /// Hố rộng w ở sàn (hàng đứng s): đục hàng s-1 và s-2, DeadZone ở s-2, đắp đáy s-3. Giống hố dung nham tầng 6.
    static void Pit(Ctx c, int x, int s, int w)
    {
        Carve(c, x, s - 2, w, 2);                                      // hàng sàn (s-1) và hàng dưới (s-2)
        for (int i = 0; i < w; i++) c.dead.Add(new Vector2Int(x + i, s - 2));
        Fill(c, x, s - 3, w, 1);
    }

    /// Bậc một chiều 3 ô, so le, mỗi bước 3 ô: từ hàng đứng `stand` lên tới hàng `top`.
    static void Climb(Ctx c, int x, int stand, int top, int alt = 5)
    {
        int k = 0;
        for (int y = stand + 2; y <= top; y += 3, k++) Plat(c, k % 2 == 0 ? x : x + alt, y, 3);
    }

    /// Đường hầm ngang cao 3 ô tại hàng đứng s, từ cột x0 đi theo hướng dir tới khi gặp ô trống (tối đa 16 ô).
    static void TunnelTo(Ctx c, int x0, int s, int dir)
    {
        for (int i = 0, x = x0; i < 16; i++, x += dir)
        {
            bool open = !Solid(c, x, s) && !Solid(c, x, s + 1) && !Solid(c, x, s + 2);
            Carve(c, x, s, 1, 3);
            if (open && i > 0) return;
        }
        log.Add($"[{c.tag}] Hầm từ x={x0} y={s} dài quá 16 ô");
    }

    /// Lỗ sàn 3 ô ở cột hx: đục từ hàng `fromY` (hàng sàn phòng trên) xuống tới `toY`.
    static void Hole(Ctx c, int hx, int fromY, int toY) { for (int y = toY; y <= fromY; y++) Carve(c, hx, y, 3, 1); }

    // ===================== DỰNG CHUNG =====================
    static void Apply(Ctx c)
    {
        if (c.failed) { log.Add($"[{c.tag}] DỪNG, không sửa scene."); return; }
        string bk = $"Assets/Scenes/Backup/{c.scene.name}_truocKhuAn.unity";
        if (!System.IO.File.Exists(bk))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            AssetDatabase.CopyAsset(c.scene.path, bk);
            log.Add($"[{c.tag}] Sao lưu: {bk}");
        }
        foreach (var r in c.scene.GetRootGameObjects()) if (r.name == RootName) Object.DestroyImmediate(r);
        var rootGo = new GameObject(RootName, typeof(Grid));
        SceneManager.MoveGameObjectToScene(rootGo, c.scene);
        c.root = rootGo.transform;

        // 1) Đục tilemap có sẵn
        int erased = 0;
        foreach (var t in c.theirMaps)
        {
            foreach (var p in c.carve)
            {
                var q = new Vector3Int(p.x, p.y, 0);
                if (t.HasTile(q)) { t.SetTile(q, null); erased++; }
            }
            EditorUtility.SetDirty(t);
        }

        // 1b) Đắp đá dày 4 ô quanh phòng ẩn (ngoài mọi phòng có sẵn), để tường không mỏng 1 ô lơ lửng giữa khoảng trống
        foreach (var (_, r) in c.rooms)
            for (int x = r.xMin - 4; x < r.xMax + 4; x++)
                for (int y = r.yMin - 4; y < r.yMax + 4; y++)
                {
                    var p = new Vector2Int(x, y);
                    if (c.carve.Contains(p) || c.fill.Contains(p)) continue;
                    float px = x + 0.5f, py = y + 0.5f;
                    if (c.theirRooms.Any(t => px > t.b.min.x && px < t.b.max.x && py > t.b.min.y && py < t.b.max.y)) continue;
                    c.fill.Add(p);
                }

        // 2) Tilemap riêng của khu ẩn
        int groundLayer = LayerMask.NameToLayer("Ground"), platLayer = LayerMask.NameToLayer("PlatForm");
        var ground = NewMap(c.root, "KhuAn_Ground", 0, groundLayer);
        var fillCells = c.fill.Where(p => !c.carve.Contains(p) && !c.theirMaps.Any(t => t.HasTile(new Vector3Int(p.x, p.y, 0)))).ToList();
        Paint(ground, fillCells, c.tint);
        var tc = ground.gameObject.AddComponent<TilemapCollider2D>();
        ground.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        ground.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        tc.usedByComposite = true;
#pragma warning restore 0618

        var platTm = NewMap(c.root, "KhuAn_Platform_OneWay", 0, platLayer >= 0 ? platLayer : groundLayer);
        Paint(platTm, c.plat.Where(p => !c.fill.Contains(p)).ToList(), new Color(0.78f, 0.62f, 0.45f));
        var ptc = platTm.gameObject.AddComponent<TilemapCollider2D>();
        platTm.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var pcc = platTm.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        ptc.usedByComposite = true;
#pragma warning restore 0618
        pcc.usedByEffector = true;
        var eff = platTm.gameObject.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true; eff.surfaceArc = 160f; eff.useSideFriction = false;

        if (c.dead.Count > 0)
        {
            var deadTm = NewMap(c.root, "KhuAn_DeadZone (hố Dash)", 1, 0);
            deadTm.gameObject.tag = "DeadZone";
            Paint(deadTm, c.dead.ToList(), new Color(0.9f, 0.25f, 0.35f, 0.9f));
            deadTm.gameObject.AddComponent<TilemapCollider2D>().isTrigger = true;
        }

        // 3) Phòng + nền tối
        var roomsT = Group(c.root, "Rooms");
        foreach (var (id, r) in c.rooms) MakeRoom(c, roomsT, id, r);

        // 4) Khóa, cửa, điểm đánh dấu
        foreach (var b in c.build) b(c.root);

        EditorSceneManager.MarkSceneDirty(c.scene);
        log.Add($"[{c.tag}] Xong: {c.rooms.Count} phòng ẩn ({string.Join(", ", c.rooms.Select(r => $"{r.id} x{r.r.x} y{r.r.y} {r.r.width}x{r.r.height}"))}), đục {erased} ô tile có sẵn, đắp {fillCells.Count} ô, {c.plat.Count} ô bậc, {c.dead.Count} ô DeadZone.");
    }

    static Tilemap NewMap(Transform parent, string name, int order, int layer)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.layer = layer;
        var tr = go.GetComponent<TilemapRenderer>();
        tr.sortingOrder = order; tr.sharedMaterial = unlit;
        return go.GetComponent<Tilemap>();
    }
    static void Paint(Tilemap tm, List<Vector2Int> cells, Color col)
    {
        var ps = cells.Select(p => new Vector3Int(p.x, p.y, 0)).ToArray();
        tm.SetTiles(ps, Enumerable.Repeat<TileBase>(tile, ps.Length).ToArray());
        foreach (var p in ps) { tm.SetTileFlags(p, TileFlags.None); tm.SetColor(p, col); }
    }
    static Transform Group(Transform parent, string name)
    {
        var f = parent.Find(name);
        if (f != null) return f;
        var g = new GameObject(name); g.transform.SetParent(parent, false); return g.transform;
    }

    static void MakeRoom(Ctx c, Transform parent, string id, RectInt r)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Map/Room.prefab");
        if (prefab == null) { log.Add($"[{c.tag}] THIẾU Assets/Prefab/Map/Room.prefab"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = id; go.layer = LayerMask.NameToLayer("Room");
        go.transform.position = new Vector3(r.xMin, r.yMin, 0);
        float w = r.width, h = r.height;
        var poly = go.GetComponent<PolygonCollider2D>();
        poly.isTrigger = true; poly.offset = Vector2.zero; poly.pathCount = 1;
        poly.SetPath(0, new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });

        var cb = new GameObject("CamBounds") { layer = go.layer };
        cb.transform.SetParent(go.transform, false);
        var bounds = cb.AddComponent<PolygonCollider2D>();
        const float pad = 0.05f;
        bounds.isTrigger = true; bounds.pathCount = 1;
        bounds.SetPath(0, new[] { new Vector2(-pad, -pad), new Vector2(w + pad, -pad), new Vector2(w + pad, h + pad), new Vector2(-pad, h + pad) });

        var so = new SerializedObject(go.GetComponent<global::Room>());
        so.FindProperty("roomId").stringValue = id;
        var vcam = so.FindProperty("vcam").objectReferenceValue as Cinemachine.CinemachineVirtualCamera;

        // nền tối sau phòng (để camera không thấy khoảng trống)
        var bg = new GameObject("BG_" + id);
        bg.transform.SetParent(go.transform, false);
        bg.transform.position = new Vector3(r.center.x, r.center.y, BgDepth);
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = Color.Lerp(c.tint, Color.black, 0.82f); sr.sortingOrder = -100; sr.sharedMaterial = unlit;
        float k = 2f;                                                   // ở độ sâu = Camera Distance, khung hình to gấp đôi
        bg.transform.localScale = new Vector3((w + 4) * k / blockSprite.bounds.size.x, (h + 4) * k / blockSprite.bounds.size.y, 1);
        so.FindProperty("background").objectReferenceValue = bg;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (vcam != null)
        {
            vcam.name = "VCam_" + id;
            vcam.Priority = 0;
            vcam.m_Lens.FieldOfView = 30f;
            vcam.m_Lens.OrthographicSize = 5f;
            vcam.m_Lens.ModeOverride = Cinemachine.LensSettings.OverrideModes.Perspective;
            var ft = vcam.GetCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            if (ft == null) ft = vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            ft.m_CameraDistance = BgDepth; ft.m_ScreenX = 0.5f; ft.m_ScreenY = 0.7f;
            ft.m_XDamping = 0.2f; ft.m_YDamping = 0.2f; ft.m_ZDamping = 1f; ft.m_TargetMovementOnly = true;
            ft.m_DeadZoneWidth = 0; ft.m_DeadZoneHeight = 0; ft.m_SoftZoneWidth = 0.8f; ft.m_SoftZoneHeight = 0.8f;
            ft.m_CenterOnActivate = true;
            var comp = vcam.GetCinemachineComponent<Cinemachine.CinemachineComposer>();
            if (comp != null) comp.m_ScreenY = 0.3f;
            var conf = vcam.GetComponent<Cinemachine.CinemachineConfiner2D>();
            if (conf == null) conf = vcam.gameObject.AddComponent<Cinemachine.CinemachineConfiner2D>();
            conf.m_BoundingShape2D = bounds; conf.m_Damping = 0f; conf.InvalidateCache();
            vcam.transform.position = new Vector3(r.center.x, r.center.y, -10);
            PrefabUtility.RecordPrefabInstancePropertyModifications(vcam);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ft);
            PrefabUtility.RecordPrefabInstancePropertyModifications(conf);
        }
        else log.Add($"[{c.tag}] {id}: Room.prefab không có vcam");
    }

    static GameObject Block(Transform parent, string name, RectInt a, Color tint, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (collider) go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = new Vector3(a.x + a.width / 2f, a.y + a.height / 2f, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = tint; sr.sortingOrder = 1; sr.sharedMaterial = unlit;
        Vector2 ss = blockSprite.bounds.size;
        go.transform.localScale = new Vector3(a.width / ss.x, a.height / ss.y, 1);
        if (collider) go.AddComponent<BoxCollider2D>().size = ss;
        return go;
    }

    static void Marker(Transform parent, string name, Vector2 pos, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = col; sr.sortingOrder = 5; sr.sharedMaterial = unlit;
        go.transform.localScale = Vector3.one * (0.8f / blockSprite.bounds.size.x);
    }

    static void Fragment(Ctx c, int n, float x, int stand, string note) =>
        c.build.Add(root => Marker(Group(root, "Markers"), $"ManhSuThat_{n:00}_TODO ({note}; gắn TruthFragment khi có script)", new Vector2(x + 0.5f, stand + 0.6f), new Color(1f, 0.85f, 0.3f)));

    static void Marker2(Ctx c, string name, Vector2 pos) =>
        c.build.Add(root => { var g = new GameObject(name); g.transform.SetParent(Group(root, "Markers"), false); g.transform.position = pos; });

    static void Water(Ctx c, string name, RectInt a, bool deadly)
    {
        c.build.Add(root =>
        {
            var w = Block(Group(root, "Water"), name, a, new Color(0.2f, 0.75f, 0.85f, 0.4f), false);
            w.GetComponent<SpriteRenderer>().sortingOrder = 6;
            if (deadly)
            {
                w.tag = "DeadZone";
                var bc = w.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = blockSprite.bounds.size;
            }
        });
    }

    static void Door(Ctx c, string doorId, float x, int stand, string targetScene, string targetDoor, int enterDir, bool interact, bool active, string note)
    {
        c.build.Add(root =>
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Map/SceneDoor.prefab");
            if (pf == null) { log.Add($"[{c.tag}] THIẾU Assets/Prefab/Map/SceneDoor.prefab"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, Group(root, "Doors"));
            var pos = new Vector3(x + 0.5f, stand + 1.5f, 0);
            go.transform.position = pos;
            go.name = doorId + (note != null ? $" ({note})" : "");
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1);
            var so = new SerializedObject(go.GetComponent<SceneDoor>());
            so.FindProperty("doorId").stringValue = doorId;
            so.FindProperty("targetDoorId").stringValue = targetDoor;
            so.FindProperty("requireInteract").boolValue = interact;
            so.FindProperty("enterDirection").enumValueIndex = enterDir;
            var ts = so.FindProperty("targetScene");
            string sp = KhuAnProbe.Scenes.First(s => System.IO.Path.GetFileNameWithoutExtension(s) == targetScene);
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sp);
            var pa = ts.FindPropertyRelative("sceneAsset"); if (pa != null) pa.objectReferenceValue = asset;
            var pn = ts.FindPropertyRelative("sceneName"); if (pn != null) pn.stringValue = targetScene;
            Transform spawn = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
            if (spawn != null)
            {
                spawn.name = "SpawnPoint";
                spawn.position = pos + new Vector3(enterDir == 1 ? 2.5f : -2.5f, 0, 0);
                so.FindProperty("spawnPoint").objectReferenceValue = spawn;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(active);
        });
    }

    static void GPFloor(Ctx c, string id, int hx, int topRow)
    {
        c.build.Add(root =>
        {
            var gp = Block(Group(root, "Hidden"), id + "_BreakableFloor (Ground Pound)", new RectInt(hx, topRow - 1, 3, 2), GPTint);
            var so = new SerializedObject(gp.AddComponent<BreakableFloor>());
            so.FindProperty("floorId").stringValue = id; so.ApplyModifiedPropertiesWithoutUndo();
        });
    }

    // ===================== 6 TẦNG =====================
    static readonly Color T1 = new Color(0.45f, 0.42f, 0.40f), T2 = new Color(0.33f, 0.45f, 0.28f), T3 = new Color(0.35f, 0.40f, 0.50f),
                          T4 = new Color(0.16f, 0.42f, 0.45f), T5 = new Color(0.36f, 0.33f, 0.30f), T6 = new Color(0.40f, 0.30f, 0.45f);
    static readonly Color GPTint = new Color(0.88f, 0.36f, 1f), SealTint = new Color(1f, 0.30f, 0.43f);

    /// Tầng 1 — Hầm Mộ Người Gác Cửa. Vách trái F1_R03, sau hầm là hố 11 ô (khóa Dash).
    static void Floor1(Ctx c)
    {
        var b = RoomBounds(c, "F1_R03"); if (c.failed) return;
        int bx = Mathf.FloorToInt(b.min.x + 0.01f);                   // mép khung R03 (-40); tường thật nằm sâu hơn
        // Tìm cột đầu tiên (từ mép trái vào) có mặt đứng thật
        int xf = int.MinValue, fy = 0;
        for (int x = bx + 1; x < bx + 30 && xf == int.MinValue; x++)
            for (int y = Mathf.FloorToInt(b.min.y); y < Mathf.CeilToInt(b.max.y) - 3; y++)
                if (!Solid(c, x, y) && Solid(c, x, y - 1) && !Solid(c, x, y + 1) && !Solid(c, x, y + 2)) { xf = x; fy = y; break; }
        if (xf == int.MinValue) { log.Add("[F1] Không tìm được mặt sàn gần vách trái R03"); c.failed = true; return; }
        log.Add($"[F1] Mặt sàn gần vách trái R03: x={xf}, hàng đứng y={fy}");
        AddRoom(c, "F1_KA01_HanhLangTro", bx - 36, fy - 1, 36, 10);
        AddRoom(c, "F1_KA02_HamMo", bx - 54, fy - 11, 18, 20);        // hạ thấp để không đè R01 phía trên
        // Hầm từ KA01 qua khoảng trống ngoài tường tới mặt sàn thật: đục 3 hàng, đắp sàn và trần chỗ còn trống
        for (int x = bx - 1; x < xf; x++)
        {
            if (!Solid(c, x, fy - 1)) Fill(c, x, fy - 1, 1, 1);
            if (!Solid(c, x, fy + 3)) Fill(c, x, fy + 3, 1, 1);
            Carve(c, x, fy, 1, 3);
        }
        Pit(c, bx - 36 + 12, fy, 11);                                  // hố Dash
        Carve(c, bx - 37, fy, 2, 3);                                   // KA01 ↔ KA02
        Fill(c, bx - 42, fy - 1, 5, 1);                                // gờ cửa trong KA02
        Climb(c, bx - 49, fy - 10, fy - 3, 5);
        Plat(c, bx - 53, fy - 4, 4);                                   // bệ mộ
        Fragment(c, 1, bx - 51, fy - 3, "thư trưởng đoàn thám hiểm trước");
    }


    /// Tầng 2 — Rễ Cây Mẹ. Sàn nứt Ground Pound ở F2_R09, trục rễ đi xuống dưới đáy map, cuối là cửa một chiều xuống tầng 3.
    static void Floor2(Ctx c)
    {
        var b = RoomBounds(c, "F2_R09"); if (c.failed) return;
        int hx = 70, by = Mathf.FloorToInt(b.min.y + 0.01f), fy = FloorAt(c, b, hx + 1);
        AddRoom(c, "F2_KA01_TrucRe", hx - 7, by - 30, 18, 30);
        AddRoom(c, "F2_KA02_GocRe", hx - 43, by - 30, 36, 10);
        Hole(c, hx, fy - 1, by - 1);
        Carve(c, hx - 8, by - 29, 2, 3);
        Climb(c, hx - 4, by - 29, by - 4, 7);
        GPFloor(c, "F2_KA_GP", hx, fy - 1);
        Fragment(c, 2, hx - 22, by - 29, "sổ người làm vườn");
        Door(c, "F2_KA_ReCay", hx - 40, by - 29, "ThirdFloorScene", "F3_KA_ReCay", 1, false, true, "rễ xuống tầng 3, một chiều");
    }

    /// Tầng 3 — Kênh Thoát Nước Pháo Đài dưới hành lang tượng F3_R09 (khóa Bơi). Thêm đầu dưới Rễ Cây Mẹ ở F3_R05 (tắt tới khi mở từ tầng 2).
    static void Floor3(Ctx c)
    {
        var b = RoomBounds(c, "F3_R09"); if (c.failed) return;
        int hx = 258, by = Mathf.FloorToInt(b.min.y + 0.01f), fy = FloorAt(c, b, hx + 1);
        AddRoom(c, "F3_KA01_GiengCong", hx - 7, by - 20, 18, 20);
        AddRoom(c, "F3_KA02_KenhThoatNuoc", hx + 11, by - 20, 54, 10);
        Hole(c, hx, fy - 1, by - 1);
        Carve(c, hx + 10, by - 19, 2, 3);
        Water(c, "Water_Swim_TODO lỗ cống (DeadZone tạm tới khi có Bơi)", new RectInt(hx, by - 19, 3, fy - by + 18), true);
        Water(c, "Water_Swim_TODO KA01 (DeadZone tạm)", new RectInt(hx - 6, by - 19, 16, 18), true);
        Water(c, "Water_Swim_TODO KA02 (DeadZone tạm)", new RectInt(hx + 12, by - 19, 52, 8), true);
        Fragment(c, 3, hx + 40, by - 19, "lệnh của đội trưởng cận vệ");
        Door(c, "F3_KA_KenhNuoc", hx + 61, by - 19, "FourthFloorScene", "F4_KA_KenhNuoc", 0, false, true, "kênh nước sang thành chìm tầng 4");

        var r5 = RoomBounds(c, "F3_R05"); if (c.failed) return;
        int px = 120, pf = FloorAt(c, r5, px);
        Door(c, "F3_KA_ReCay", px, pf, "SecondFloorScene_v2", "F2_KA_ReCay", 1, true, false, "đầu dưới Rễ Cây Mẹ, mở từ tầng 2");
    }

    /// Tầng 4 — Kho Lưu Trữ Chìm. Sàn nứt Ground Pound ở F4_R24 (kênh ngầm), dưới đáy map. Nước chỉ để nhìn (như thành chìm).
    static void Floor4(Ctx c)
    {
        var b = RoomBounds(c, "F4_R24"); if (c.failed) return;
        int hx = 100, by = Mathf.FloorToInt(b.min.y + 0.01f), fy = FloorAt(c, b, hx + 1);
        AddRoom(c, "F4_KA01_GiengLuuTru", hx - 7, by - 20, 18, 20);
        AddRoom(c, "F4_KA02_KhoLuuTruChim", hx + 11, by - 20, 54, 10);
        Hole(c, hx, fy - 1, by - 1);
        Carve(c, hx + 10, by - 19, 2, 3);
        Climb(c, hx - 4, by - 19, by - 4, 7);
        Plat(c, hx + 20, by - 15, 8); Plat(c, hx + 34, by - 15, 8);   // kệ hồ sơ đổ
        GPFloor(c, "F4_KA_GP", hx, fy - 1);
        Water(c, "Water_Swim_TODO KA (chỉ để nhìn)", new RectInt(hx - 6, by - 19, 70, 18), false);
        Fragment(c, 4, hx + 50, by - 19, "sổ ghi chép thành phố");

        var r18 = RoomBounds(c, "F4_R18"); if (c.failed) return;
        int px = 100, pf = FloorAt(c, r18, px);
        Door(c, "F4_KA_KenhNuoc", px, pf, "ThirdFloorScene", "F3_KA_KenhNuoc", 1, true, true, "kênh nước lên pháo đài tầng 3");
    }

    /// Tầng 5 — Ống Khói Rồng. Vách phải miệng cống R21 (trong F5_R16), hố 11 ô (Dash), trục xuống; đáy là cửa xuống tầng 6.
    static void Floor5(Ctx c)
    {
        var b = RoomBounds(c, "F5_R16"); if (c.failed) return;
        int bx = Mathf.CeilToInt(b.max.x - 0.01f), fy = FloorAt(c, b, bx - 4);
        AddRoom(c, "F5_KA01_ViaSup", bx, fy - 1, 36, 10);
        AddRoom(c, "F5_KA02_OngKhoiRong", bx + 36, fy - 11, 18, 20);
        TunnelTo(c, bx, fy, -1);
        Pit(c, bx + 12, fy, 11);
        Carve(c, bx + 35, fy, 2, 3);
        Fill(c, bx + 37, fy - 1, 5, 1);                                // gờ cửa trong ống khói
        Climb(c, bx + 44, fy - 10, fy - 3, -5);
        Fragment(c, 5, bx + 47, fy - 10, "ghi chép thầy tu thánh đường");
        Door(c, "F5_KA_OngKhoi", bx + 50, fy - 10, "SixthFloorScene", "F6_KA_OngKhoi", 0, false, false, "xuống tầng 6, mở bằng cần gạt ở tầng 6");
    }

    /// Tầng 6 — Xưởng Giả Kim X1–X4 dưới hầm mộ R22, ấn phong ở sàn R22. Thêm đầu dưới Ống Khói Rồng ở tháp canh R21.
    static void Floor6(Ctx c)
    {
        var r22 = RoomBounds(c, "F6_R22"); if (c.failed) return;
        int hx = 203, fy = FloorAt(c, r22, hx + 1);
        AddRoom(c, "F6_X1_CauThangAnPhong", 198, -90, 18, 20);
        AddRoom(c, "F6_X2_XuongGiaKim", 144, -80, 54, 10);
        AddRoom(c, "F6_X3_PhongKheUoc", 144, -90, 54, 10);
        AddRoom(c, "F6_X4_DienQuy", 144, -110, 72, 20);
        Hole(c, hx, fy - 1, -71);                                      // ấn phong: sàn R22 → X1
        Carve(c, 197, -79, 2, 3);                                      // X1 ↔ X2
        Fill(c, 199, -80, 5, 1);                                       // gờ cửa trong X1
        Climb(c, 206, -89, -74, -5);                                   // X1: từ đáy lên lỗ ấn phong
        Carve(c, 147, -81, 3, 2);                                      // X2 → X3 (hố rơi một chiều)
        Carve(c, 190, -91, 3, 2);                                      // X3 → X4 (rơi vào điện)
        Carve(c, 206, -91, 3, 2);                                      // X4 → X1 (cổng sau boss)
        Climb(c, 200, -109, -94, 5);
        Plat(c, 160, -103, 8); Plat(c, 176, -100, 8); Plat(c, 192, -103, 8);   // ba bậc cho trận boss
        c.build.Add(root =>
        {
            var h = Group(root, "Hidden");
            Block(h, "AnPhong_TODO (mở khi đủ 6 Mảnh Sự Thật)", new RectInt(hx, fy - 2, 3, 2), SealTint);
            Block(h, "Gate_X4_X1_TODO (mở sau boss thật)", new RectInt(206, -91, 3, 2), SealTint);
        });
        Marker2(c, "BossThat_Spawn_TODO (pháp sư + quỷ, bạn thiết kế)", new Vector2(180, -100));
        Marker2(c, "Ending_That_TODO (kết thúc thật)", new Vector2(210, -108));
        Marker2(c, "VongTrieuHoi_TODO (vòng khế ước, chữ trên tường)", new Vector2(171, -89));
        Fragment(c, 6, 200, fy, "trang khế ước bị xé, trong hầm mộ R22");

        var r20 = RoomBounds(c, "F6_R20"); if (c.failed) return;
        int px = 210, pf = FloorAt(c, r20, px);
        Door(c, "F6_KA_OngKhoi", px, pf, "FifthFloorScene", "F5_KA_OngKhoi", 1, true, true, "ống khói lên tầng 5; thay bằng cần gạt");
    }

    // ===================== MENU =====================
    /// Đóng mọi scene tầng đang mở thêm (additive) MÀ KHÔNG LƯU, bỏ thay đổi dở dang.
    [MenuItem("Tools/Culinaria/Khu An/Dong scene tang dang mo (khong luu)")]
    public static void CloseFloorScenesDiscard()
    {
        foreach (var p in KhuAnProbe.Scenes)
        {
            var s = SceneManager.GetSceneByPath(p);
            if (s.isLoaded && SceneManager.sceneCount > 1) { Debug.Log($"[KhuAn] Đóng không lưu: {s.name} (dirty={s.isDirty})"); EditorSceneManager.CloseScene(s, true); }
        }
    }

    [MenuItem("Tools/Culinaria/Khu An/Debug - ban do o tang 1 quanh R03")]
    public static void DebugF1()
    {
        var s = SceneManager.GetSceneByPath(KhuAnProbe.Scenes[0]); bool opened = false;
        if (!s.isLoaded) { s = EditorSceneManager.OpenScene(KhuAnProbe.Scenes[0], OpenSceneMode.Additive); opened = true; }
        Physics2D.SyncTransforms();
        var c = Begin(s, "F1", Color.white);
        var sb = new System.Text.StringBuilder();
        for (int y = 18; y >= -14; y--)
        {
            sb.Append(y.ToString().PadLeft(4)).Append(' ');
            for (int x = -46; x <= -20; x++)
            {
                bool tm = c.theirMaps.Any(t => t.HasTile(new Vector3Int(x, y, 0)));
                bool so = Solid(c, x, y);
                sb.Append(tm ? '#' : so ? 'C' : '.');
            }
            sb.Append(" ¦ ");
        }
        hits.Clear();
        var f = new ContactFilter2D { useTriggers = false }; f.SetLayerMask(LayerMask.GetMask("Ground", "PlatForm"));
        var names = new HashSet<string>();
        for (int x = -46; x <= -20; x++) for (int y = -14; y <= 18; y++)
        { hits.Clear(); Physics2D.OverlapPoint(new Vector2(x + 0.5f, y + 0.5f), f, hits); foreach (var h in hits) names.Add(h.name + "(" + h.GetType().Name + ")"); }
        Debug.Log("[KhuAnDbg] x=-46..-20 # tile, C collider khác ¦ " + sb + " COLLIDERS: " + string.Join(", ", names));
        if (opened) EditorSceneManager.CloseScene(s, true);
    }

    /// Chạy thử: chỉ tính bố cục, báo ô sàn tìm được và số ô sẽ đục, KHÔNG sửa scene.
    static void LogDry(Ctx c)
    {
        int willErase = c.carve.Count(p => c.theirMaps.Any(t => t.HasTile(new Vector3Int(p.x, p.y, 0))));
        log.Add($"[{c.tag}] THỬ {(c.failed ? "LỖI" : "OK")}: {string.Join(", ", c.rooms.Select(r => $"{r.id} x{r.r.x}..{r.r.xMax} y{r.r.y}..{r.r.yMax}"))}; sẽ đục {willErase} ô tile có sẵn, đắp {c.fill.Count} ô, {c.dead.Count} ô DeadZone, {c.build.Count} vật.");
    }

    [MenuItem("Tools/Culinaria/Khu An/Build tat ca khu an")]
    public static void BuildAll() => Run(false);

    [MenuItem("Tools/Culinaria/Khu An/Kiem tra (khong sua)")]
    public static void DryRun() => Run(true);

    static void Run(bool dry)
    {
        log.Clear();
        tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile == null) { Debug.LogError("[KhuAn] Thiếu " + TilePath); return; }
        blockSprite = tile.sprite;
        unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        var floors = new (string path, string tag, Color tint, System.Action<Ctx> f)[]
        {
            (KhuAnProbe.Scenes[0], "F1", T1, Floor1), (KhuAnProbe.Scenes[1], "F2", T2, Floor2), (KhuAnProbe.Scenes[2], "F3", T3, Floor3),
            (KhuAnProbe.Scenes[3], "F4", T4, Floor4), (KhuAnProbe.Scenes[4], "F5", T5, Floor5), (KhuAnProbe.Scenes[5], "F6", T6, Floor6),
        };
        Scene prevActive = SceneManager.GetActiveScene();
        foreach (var (path, tag, tint, f) in floors)
        {
            Scene s = SceneManager.GetSceneByPath(path);
            bool opened = false;
            try
            {
                if (!s.isLoaded) { s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened = true; }
                else if (s.isDirty) { log.Add($"[{tag}] Scene đang mở và có thay đổi chưa lưu → bỏ qua, hãy lưu rồi chạy lại."); continue; }
                SceneManager.SetActiveScene(s);
                Physics2D.SyncTransforms();
                var c = Begin(s, tag, tint);
                f(c);
                if (dry) LogDry(c);
                else { Apply(c); if (!c.failed) EditorSceneManager.SaveScene(s); }
            }
            catch (System.Exception e) { log.Add($"[{tag}] LỖI: {e.GetType().Name}: {e.Message}"); Debug.LogException(e); }
            finally
            {
                if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
                if (opened && s.isLoaded) EditorSceneManager.CloseScene(s, true);
            }
        }
        System.IO.File.WriteAllText("Temp/KhuAnBuild.txt", string.Join("\n", log));
        foreach (var l in log) Debug.Log("[KhuAn] " + l);
    }
}
