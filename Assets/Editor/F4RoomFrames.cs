using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chia khung phòng FourthFloorScene (chép từ F2RoomFrames, chỉ đổi dữ liệu tầng).
///  - Ô của phòng = hợp các con "RoomRect" / "RoomRect_<id>" (BoxCollider2D tắt, chỉ là dữ liệu).
///  - Collider gốc của Room = ô phòng + dải lấn sang phòng kế bên ở mỗi lối thông. "CamBounds" = cùng đa giác (+ phần lộ ở lối bí mật).
///  - Mỗi lối thông có 1 EnemyWall + Barrier đúng ranh giới.
///  - THÊM ở tầng 2: vùng cấm quái quanh mọi chỗ chuyển phòng (hầm ngang, lỗ trần/sàn). Hai đầu vùng có EnemyWall + Barrier
///    để quái tuần tra quay đầu và quái đang đuổi/bị đánh văng không vào được → người chơi vừa sang phòng không bị đánh úp.
/// Chạy: Tools > Culinaria > F4 Redivide Room Frames (F4RoomMerge và FourthFloorBuilder cũng tự gọi). Chạy lại được.
public static class F4RoomFrames
{
    const string SceneName = "FourthFloorScene";
    const string Prefix = "F4_";          // phòng thường F4_Rxx + phòng hầm ẩn F4_H1..H7, F4_CUR (phòng hốc F4P_* bỏ qua)
    const string GateRoot = "______RoomGate_____";
    const string HoleGlowRoot = "______HoleGlow_____";
    const string EnemyRoot = "______EnemyF4______";
    const int StripDepth = 3;
    const int StripDepthSide = 1;
    const int StripMargin = 1;
    const float CamHalfH = 5f;
    const float CamHalfW = 10f;
    const int RevealRows = 2;
    const int RevealHalfW = 10;
    const int KeepSide = 8;            // vùng cấm quái: 8 ô mỗi phía của ranh giới ngang (hầm 4 ô + 4 ô đệm)
    const int KeepHole = 5;            // vùng cấm quái: 5 ô mỗi bên lỗ trần/sàn
    const int KeepHoleDepth = 12;      // phòng dưới: vùng cấm kéo từ lỗ xuống tới sàn, tối đa 12 ô
    static readonly HashSet<string> SecretRooms = new HashSet<string> { "F4_H1", "F4_H2", "F4_H3", "F4_H4" };   // hầm ẩn phía trên lỗ trần R05, R08, R12, R17

    /// Khung gốc của 30 phòng trong FourthFloorBuilder (1 ô lưới = 18×10, y lưới tăng xuống). Nhận ra khung theo hình học → đổi tên phòng thoải mái.
    public static readonly Dictionary<RectInt, string> OriginalRects = new Dictionary<RectInt, string>();
    static F4RoomFrames()
    {
        void R(string id, int gx, int gy, int w, int h) => OriginalRects[new RectInt(gx * 18, -(gy + h) * 10, w * 18, h * 10)] = id;
        R("F4_R01", 1, 0, 1, 2); R("F4_R02", 2, 1, 2, 1); R("F4_R03", 4, 1, 1, 1); R("F4_R04", 2, 2, 2, 1);
        R("F4_R05", 1, 3, 3, 1); R("F4_R06", 4, 3, 1, 1); R("F4_R07", 4, 4, 2, 2); R("F4_R08", 6, 4, 1, 1);
        R("F4_R09", 3, 4, 1, 2); R("F4_R10", 2, 6, 2, 1); R("F4_R11", 4, 6, 1, 1);
        R("F4_R12", 5, 6, 3, 2); R("F4_R13", 8, 7, 2, 1); R("F4_R14", 10, 7, 1, 1); R("F4_R15", 6, 8, 1, 2);
        R("F4_R16", 5, 10, 2, 1); R("F4_R17", 7, 10, 1, 1); R("F4_R18", 4, 11, 3, 1); R("F4_R19", 3, 10, 1, 2);
        R("F4_R20", 5, 12, 2, 2); R("F4_R21", 4, 12, 1, 1); R("F4_R22", 7, 11, 2, 1); R("F4_R23", 7, 12, 1, 1);
        R("F4_R24", 5, 14, 2, 1); R("F4_R25", 7, 14, 1, 2); R("F4_R26", 8, 15, 1, 1); R("F4_R27", 9, 14, 3, 2);
        R("F4_R28", 11, 13, 1, 1); R("F4_R29", 10, 8, 1, 6); R("F4_R30", 12, 13, 2, 1);
    }

    static readonly string[] BlockingMaps = { "F4_Ground" };
    static readonly string[] FillMaps = { "F4_Ground" };

    [MenuItem("Tools/Culinaria/F4 Redivide Room Frames")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F4 Room Frames", "Hãy mở FourthFloorScene trước.", "OK");
            return;
        }
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    class Info
    {
        public Room room;
        public List<(string id, RectInt r)> rects;
        public readonly HashSet<Vector2Int> own = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> camCells = new HashSet<Vector2Int>();
        public readonly List<string> notes = new List<string>();
        public RectInt Main => rects[0].r;
    }

    struct Opening { public char side; public int line, a, b; public Info nb; }

    public static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).Where(t => t.gameObject.activeInHierarchy).ToList();
        var blocking = maps.Where(t => BlockingMaps.Contains(t.name)).ToList();
        var fill = maps.Where(t => FillMaps.Contains(t.name)).ToList();
        if (blocking.Count == 0) { Debug.LogError("[F4Frames] Không tìm thấy tilemap F4_Ground"); return; }
        bool Blocked(int x, int y) { var p = new Vector3Int(x, y, 0); return blocking.Any(t => t.HasTile(p)); }

        var infos = roots.Where(g => g.activeSelf).SelectMany(g => g.GetComponentsInChildren<Room>(true))
            .Where(r => r.name.StartsWith(Prefix)).OrderBy(r => r.name)
            .Select(r =>
            {
                var i = new Info { room = r, rects = RoomRects(r) };
                foreach (var (_, rc) in i.rects)
                    for (int x = rc.xMin; x < rc.xMax; x++) for (int y = rc.yMin; y < rc.yMax; y++) i.own.Add(new Vector2Int(x, y));
                return i;
            }).ToList();
        var owner = new Dictionary<Vector2Int, Info>();
        foreach (var i in infos) foreach (var c in i.own) if (!owner.ContainsKey(c)) owner[c] = i;

        // 1) Mặt nạ ô + tường chặn quái ở lối thông + vùng cấm quái
        var gates = new List<(Vector2 center, Vector2 size, string name)>();
        var keeps = new List<(Vector2 center, Vector2 size, string name)>();
        var zones = new List<Rect>();
        var secretGates = new HashSet<string>();
        foreach (var i in infos)
        {
            i.cells.UnionWith(i.own);
            foreach (var op in Openings(i, owner, Blocked))
            {
                int depth = op.side == 'L' || op.side == 'R' ? StripDepthSide : StripDepth;
                for (int k = 1; k <= depth; k++)
                    for (int t = op.a - StripMargin; t <= op.b + StripMargin; t++)
                    {
                        var c = op.side == 'L' ? new Vector2Int(op.line - k, t) : op.side == 'R' ? new Vector2Int(op.line + k, t)
                              : op.side == 'B' ? new Vector2Int(t, op.line - k) : new Vector2Int(t, op.line + k);
                        if (op.nb.own.Contains(c)) i.cells.Add(c);
                    }
                i.notes.Add($"{op.side}[{op.a}..{op.b}]→{op.nb.room.name}");
                int len = op.b - op.a + 1;
                string gname = $"Gate_{i.room.name}_{op.nb.room.name}_{op.a}";
                string kname = "Keep" + gname.Substring(4);
                if (op.side == 'R')
                {
                    float bx = op.line + 1;
                    gates.Add((new Vector2(bx, op.a + len / 2f), new Vector2(0.4f, len), gname));
                    float y0 = op.a - 1, y1 = op.b + 3;
                    zones.Add(new Rect(bx - KeepSide, y0, 2 * KeepSide, y1 - y0));
                    keeps.Add((new Vector2(bx - KeepSide, (y0 + y1) / 2f), new Vector2(0.4f, y1 - y0), kname + "_L"));
                    keeps.Add((new Vector2(bx + KeepSide, (y0 + y1) / 2f), new Vector2(0.4f, y1 - y0), kname + "_R"));
                }
                if (op.side == 'T')
                {
                    float by = op.line + 1;
                    gates.Add((new Vector2(op.a + len / 2f, by), new Vector2(len, 0.4f), gname));
                    float xl = op.a - KeepHole, xr = op.b + 1 + KeepHole;
                    zones.Add(new Rect(xl, by, xr - xl, 5f));
                    keeps.Add((new Vector2(xl, by + 2.5f), new Vector2(0.4f, 5f), kname + "_UL"));
                    keeps.Add((new Vector2(xr, by + 2.5f), new Vector2(0.4f, 5f), kname + "_UR"));
                    int mid = (op.a + op.b) / 2, fy = (int)by - 1;
                    while (!Blocked(mid, fy) && fy > by - KeepHoleDepth) fy--;
                    float ly0 = Mathf.Max(fy, by - KeepHoleDepth), lh = by - ly0;
                    zones.Add(new Rect(xl, ly0, xr - xl, lh));
                    keeps.Add((new Vector2(xl, ly0 + lh / 2f), new Vector2(0.4f, lh), kname + "_DL"));
                    keeps.Add((new Vector2(xr, ly0 + lh / 2f), new Vector2(0.4f, lh), kname + "_DR"));

                    if (op.nb.rects.Any(rr => SecretRooms.Contains(rr.id)))
                    {
                        secretGates.Add(gname);
                        int rx0 = mid, rx1 = mid;
                        while (i.own.Contains(new Vector2Int(rx0 - 1, op.line))) rx0--;
                        while (i.own.Contains(new Vector2Int(rx1 + 1, op.line))) rx1++;
                        int w = Mathf.Min(2 * RevealHalfW + 1, rx1 - rx0 + 1);
                        int wx0 = Mathf.Clamp(mid - RevealHalfW, rx0, rx1 + 1 - w);
                        for (int x = wx0; x < wx0 + w; x++)
                            for (int y = op.line + 1; y <= op.line + RevealRows; y++) i.camCells.Add(new Vector2Int(x, y));
                        i.notes.Add($"lộ {RevealRows} ô x[{wx0}..{wx0 + w - 1}]");
                    }
                }
            }
        }

        // 2) Collider + CamBounds + VCam
        foreach (var i in infos)
        {
            var go = i.room.gameObject;
            foreach (Transform ch in go.transform.Cast<Transform>().ToList())
                if (ch.name == "CamBounds" || ch.name.StartsWith("Buffer")) Object.DestroyImmediate(ch.gameObject);

            i.camCells.UnionWith(i.cells);
            var loop = Outline(i.cells);
            var camLoop = Outline(i.camCells);
            var poly = go.GetComponent<PolygonCollider2D>();
            poly.isTrigger = true; poly.offset = Vector2.zero; poly.pathCount = 1;
            poly.SetPath(0, loop.Select(p => (Vector2)go.transform.InverseTransformPoint(new Vector3(p.x, p.y, 0))).ToArray());

            var cb = new GameObject("CamBounds") { layer = go.layer };
            cb.transform.SetParent(go.transform, false);
            cb.transform.position = Vector3.zero;
            var bounds = cb.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true; bounds.pathCount = 1;
            bounds.SetPath(0, camLoop.Select(p => (Vector2)cb.transform.InverseTransformPoint(new Vector3(p.x, p.y, 0))).ToArray());

            var so = new SerializedObject(i.room);
            var vc = so.FindProperty("vcam").objectReferenceValue as CinemachineVirtualCamera;
            if (vc == null) { Debug.LogWarning($"[F4Frames] {go.name}: chưa có vcam"); continue; }
            vc.name = "VCam_" + go.name;
            vc.Priority = 0;
            vc.m_Lens.OrthographicSize = CamHalfH;
            var ft = vc.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (ft != null)
            {
                ft.m_XDamping = 0.2f; ft.m_YDamping = 0.2f;
                ft.m_DeadZoneWidth = 0f; ft.m_DeadZoneHeight = 0f;
                ft.m_ScreenX = 0.5f; ft.m_ScreenY = 0.7f;
                EditorUtility.SetDirty(ft);
            }
            var conf = vc.GetComponent<CinemachineConfiner2D>();
            if (conf == null) conf = vc.gameObject.AddComponent<CinemachineConfiner2D>();
            conf.m_BoundingShape2D = bounds;
            conf.m_Damping = 0f;
            conf.InvalidateCache();
            var c = i.Main.center;
            vc.transform.position = new Vector3(c.x, c.y, vc.transform.position.z);
            EditorUtility.SetDirty(conf); EditorUtility.SetDirty(vc); EditorUtility.SetDirty(poly); EditorUtility.SetDirty(bounds);
        }

        // 3) Tường chặn quái ở chỗ chuyển phòng + hai đầu vùng cấm quái (tạo lại mỗi lần)
        var oldGate = roots.FirstOrDefault(g => g.name == GateRoot);
        if (oldGate != null) Object.DestroyImmediate(oldGate);
        var gateRoot = new GameObject(GateRoot);
        SceneManager.MoveGameObjectToScene(gateRoot, scene);
        int barrierLayer = SetupBarrierLayer();
        foreach (var (center, size, name) in gates) Wall(gateRoot.transform, center, size, name, true, barrierLayer);
        foreach (var (center, size, name) in keeps) Wall(gateRoot.transform, center, size, name, false, barrierLayer);

        // 3a) Quái đang đứng trong vùng cấm → báo
        roots = scene.GetRootGameObjects();
        var enemyRoot = roots.FirstOrDefault(g => g.name == EnemyRoot);
        var inside = new List<string>();
        if (enemyRoot != null)
            foreach (var e in enemyRoot.GetComponentsInChildren<Enemy>(true))
            {
                Vector2 p = e.transform.position;
                if (zones.Any(z => z.Contains(p) || z.Contains(p + Vector2.down))) inside.Add($"{e.transform.parent.name}@{p.x:F0},{p.y:F0}");
            }
        if (inside.Count > 0) Debug.LogWarning($"[F4Frames] {inside.Count} quái đang đứng trong vùng cấm: {string.Join("; ", inside)}");
        else Debug.Log("[F4Frames] Không quái nào đứng trong vùng cấm quanh chỗ chuyển phòng.");

        // 3b) Vệt sáng ở lỗ trần/sàn
        var oldGlow = roots.FirstOrDefault(g => g.name == HoleGlowRoot);
        if (oldGlow != null) Object.DestroyImmediate(oldGlow);
        var glowRoot = new GameObject(HoleGlowRoot);
        SceneManager.MoveGameObjectToScene(glowRoot, scene);
        var glow = F4RoomMerge.GlowSprite();
        foreach (var (center, size, name) in gates.Where(g => g.size.x > g.size.y))
        {
            var go = new GameObject("Glow" + name.Substring("Gate".Length));
            go.transform.SetParent(glowRoot.transform, false);
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = glow; sr.color = F4RoomMerge.GlowColor; sr.sortingOrder = -4;
            if (glow != null)
            {
                var s = glow.bounds.size;
                go.transform.localScale = new Vector3((size.x + 1f) / s.x, 3f / s.y, 1f);
            }
            var pulse = go.AddComponent<GlowPulse>();
            if (secretGates.Contains(name)) pulse.SetRange(0.04f, 0.12f);
        }

        // 4) Tầng 4 KHÔNG vẽ thêm tile ngoài phòng (Confiner2D giữ cả khung hình trong CamBounds nên ngoài phòng không bao giờ lộ ra).
        //    Ô đất ngoài khung phòng do người dùng tự vẽ được giữ nguyên, chỉ đếm để báo.
        int outsideTiles = 0;
        foreach (var t in fill)
            foreach (var p in t.cellBounds.allPositionsWithin)
                if (t.HasTile(p) && !owner.ContainsKey(new Vector2Int(p.x, p.y))) outsideTiles++;

        Debug.Log($"[F4Frames] Xong {infos.Count} phòng, {gates.Count} chỗ chuyển phòng, {keeps.Count} tường vùng cấm quái. Vẽ thêm 0 ô tile ({outsideTiles} ô đất có sẵn nằm ngoài khung phòng, giữ nguyên).");
        foreach (var i in infos)
            Debug.Log($"[F4Frames] {i.room.name} [{string.Join("+", i.rects.Select(r => r.id))}]: {string.Join(", ", i.notes)}");
    }

    static void Wall(Transform parent, Vector2 center, Vector2 size, string name, bool isGate, int barrierLayer)
    {
        var g = new GameObject(name) { tag = "EnemyWall" };
        g.transform.SetParent(parent, false);
        g.transform.position = center;
        var bc = g.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = size;
        if (isGate) g.AddComponent<RoomGate>();
        if (barrierLayer >= 0)
        {
            var bar = new GameObject("Barrier") { layer = barrierLayer };
            bar.transform.SetParent(g.transform, false);
            var bb = bar.AddComponent<BoxCollider2D>();
            bb.size = new Vector2(Mathf.Max(size.x, 0.6f), Mathf.Max(size.y, 0.6f));
        }
    }

    static int SetupBarrierLayer()
    {
        int bl = LayerMask.NameToLayer(RoomGate.BarrierLayer), el = LayerMask.NameToLayer("Enemy");
        if (bl < 0 || el < 0) { Debug.LogError($"[F4Frames] Thiếu layer {RoomGate.BarrierLayer} hoặc Enemy → quái vẫn đi qua được chỗ chuyển phòng"); return -1; }
        for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(bl, i, i != el);
        AssetDatabase.SaveAssets();
        return bl;
    }

    public static void EnsureRoomRect(Room r)
    {
        if (r.transform.Cast<Transform>().Any(t => t.name.StartsWith("RoomRect"))) return;
        // khung gốc lấy theo vị trí phòng (collider có thể đã bị nới dải lấn ở lần chia khung trước)
        Vector2 o = r.transform.position;
        RectInt rect = OriginalRects.Keys.FirstOrDefault(k => k.x == Mathf.RoundToInt(o.x) && k.y == Mathf.RoundToInt(o.y));
        Vector2 center, size;
        if (rect.width > 0) { center = rect.center; size = rect.size; }
        else { var b = r.GetComponent<Collider2D>().bounds; center = b.center; size = b.size; }
        var go = new GameObject("RoomRect") { layer = r.gameObject.layer };
        go.transform.SetParent(r.transform, false);
        go.transform.position = Vector3.zero;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.offset = center; box.size = size;
        box.enabled = false;
    }

    public static List<(string id, RectInt r)> RoomRects(Room r)
    {
        EnsureRoomRect(r);
        var list = new List<(string id, RectInt r)>();
        string mainId = null;
        foreach (Transform t in r.transform)
        {
            if (!t.name.StartsWith("RoomRect")) continue;
            var box = t.GetComponent<BoxCollider2D>();
            if (box == null) continue;
            Vector2 min = (Vector2)t.TransformPoint(box.offset) - box.size / 2f;
            var rect = new RectInt(Mathf.RoundToInt(min.x), Mathf.RoundToInt(min.y), Mathf.RoundToInt(box.size.x), Mathf.RoundToInt(box.size.y));
            string id = OriginalRects.TryGetValue(rect, out var oid) ? oid
                      : t.name == "RoomRect" ? r.name : t.name.Substring("RoomRect_".Length);
            list.Add((id, rect));
            if (t.name == "RoomRect") mainId = id;
        }
        return list.OrderBy(p => p.id == mainId ? 0 : 1).ThenBy(p => p.id).ToList();
    }

    static List<Opening> Openings(Info i, Dictionary<Vector2Int, Info> owner, System.Func<int, int, bool> blocked)
    {
        var raw = new Dictionary<(char side, int line, Info nb), List<int>>();
        var dirs = new[] { ('L', new Vector2Int(-1, 0)), ('R', new Vector2Int(1, 0)), ('B', new Vector2Int(0, -1)), ('T', new Vector2Int(0, 1)) };
        foreach (var c in i.own)
            foreach (var (s, d) in dirs)
            {
                var o = c + d;
                if (i.own.Contains(o) || blocked(c.x, c.y)) continue;
                if (!owner.TryGetValue(o, out var nb) || nb == i) continue;
                bool horiz = s == 'L' || s == 'R';
                var key = (s, horiz ? c.x : c.y, nb);
                if (!raw.TryGetValue(key, out var l)) raw[key] = l = new List<int>();
                l.Add(horiz ? c.y : c.x);
            }
        var res = new List<Opening>();
        foreach (var kv in raw)
        {
            var v = kv.Value.Distinct().OrderBy(z => z).ToList();
            int start = v[0], prev = v[0];
            for (int k = 1; k <= v.Count; k++)
            {
                if (k < v.Count && v[k] == prev + 1) { prev = v[k]; continue; }
                res.Add(new Opening { side = kv.Key.side, line = kv.Key.line, a = start, b = prev, nb = kv.Key.nb });
                if (k < v.Count) { start = prev = v[k]; }
            }
        }
        return res.OrderBy(o => o.side).ThenBy(o => o.a).ToList();
    }

    static List<Vector2Int> Outline(HashSet<Vector2Int> cells)
    {
        var next = new Dictionary<Vector2Int, Vector2Int>();
        foreach (var c in cells)
        {
            int x = c.x, y = c.y;
            if (!cells.Contains(new Vector2Int(x, y - 1))) next[new Vector2Int(x, y)] = new Vector2Int(x + 1, y);
            if (!cells.Contains(new Vector2Int(x + 1, y))) next[new Vector2Int(x + 1, y)] = new Vector2Int(x + 1, y + 1);
            if (!cells.Contains(new Vector2Int(x, y + 1))) next[new Vector2Int(x + 1, y + 1)] = new Vector2Int(x, y + 1);
            if (!cells.Contains(new Vector2Int(x - 1, y))) next[new Vector2Int(x, y + 1)] = new Vector2Int(x, y);
        }
        var start = next.Keys.OrderBy(p => p.y).ThenBy(p => p.x).First();
        var loop = new List<Vector2Int> { start };
        var cur = next[start];
        int guard = 0;
        while (cur != start && guard++ < 100000) { loop.Add(cur); cur = next[cur]; }
        if (loop.Count < next.Count) Debug.LogWarning($"[F4Frames] Viền chỉ đi qua {loop.Count}/{next.Count} cạnh — tập ô có lỗ hoặc chạm chéo");
        var outl = new List<Vector2Int>();
        for (int k = 0; k < loop.Count; k++)
        {
            var p = loop[(k - 1 + loop.Count) % loop.Count]; var q = loop[k]; var n = loop[(k + 1) % loop.Count];
            var d1 = q - p; var d2 = n - q;
            if (d1.x * d2.y - d1.y * d2.x != 0) outl.Add(q);
        }
        return outl;
    }

    static bool CopyNearest(List<Tilemap> fill, int x, int y)
    {
        for (int d = 1; d <= 40; d++)
            foreach (var p in new[] { new Vector3Int(x + d, y, 0), new Vector3Int(x - d, y, 0), new Vector3Int(x, y + d, 0), new Vector3Int(x, y - d, 0) })
                foreach (var t in fill)
                {
                    var tile = t.GetTile(p);
                    if (tile == null) continue;
                    var q = new Vector3Int(x, y, 0);
                    t.SetTile(q, tile);
                    t.SetTileFlags(q, TileFlags.None);
                    t.SetTransformMatrix(q, t.GetTransformMatrix(p));
                    t.SetColor(q, t.GetColor(p));
                    return true;
                }
        return false;
    }
}
