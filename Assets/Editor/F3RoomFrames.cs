using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chia khung phòng ThirdFloorScene (phòng có thể gồm nhiều khung chữ nhật sau khi gộp — xem F3RoomMerge).
///  - Ô của phòng = hợp các con "RoomRect" / "RoomRect_<id>" (BoxCollider2D tắt, chỉ là dữ liệu).
///  - Lối thông = ô viền không bị tile chặn mà ô kề bên ngoài thuộc PHÒNG KHÁC. Ranh giới giữa 2 khung cùng phòng không tính.
///  - Collider gốc của Room = viền ngoài của (ô phòng + dải lấn 3 ô vào phòng kế bên ở mỗi lối thông). "CamBounds" = cùng đa giác.
///  - Mỗi lối thông có 1 EnemyWall (trigger) đúng ranh giới → quái không đi/bay qua chỗ chuyển phòng.
///  - VCam: FramingTransposer damping 0.2, screenY 0.7, ortho 5, priority 0.
///  - Vẽ thêm tile ở chỗ camera nhìn thấy mà chưa có gì; kéo giãn ảnh nền BG_<id> nếu khung còn lỗ.
/// Chạy: Tools > Culinaria > F3 Redivide Room Frames (F3RoomMerge cũng tự gọi). Chạy lại được.
public static class F3RoomFrames
{
    const string SceneName = "ThirdFloorScene";
    const string Prefix = "F3_R";
    const string GateRoot = "______RoomGate_____";
    const string HoleGlowRoot = "______HoleGlow_____";
    const int StripDepth = 3;          // dải lấn qua lỗ trần/sàn (ô)
    const int StripDepthSide = 1;      // dải lấn qua cửa ngang/hầm: chuyển phòng ngay sau vệt sáng giữa hầm
    const int StripMargin = 1;
    const float CamHalfH = 5f;
    const float CamHalfW = 10f;
    const int RevealRows = 2;          // C.4: lối vào khu bí mật — camera nhích lên được 2 ô
    const int RevealHalfW = 10;        // bề ngang phần nhô phải ≥ khung hình (Confiner2D không vào vùng hẹp hơn khung hình)
    static readonly HashSet<string> SecretRooms = new HashSet<string> { "F3_R08", "F3_R16", "F3_R20" };

    static readonly string[] BlockingMaps = { "F3_Ground", "F3_Ground_KnightDungeon", "F3_DeadZone_Lava" };
    static readonly string[] FillMaps = { "F3_Ground", "F3_Ground_KnightDungeon" };

    [MenuItem("Tools/Culinaria/F3 Redivide Room Frames")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F3 Room Frames", "Hãy mở ThirdFloorScene trước.", "OK");
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
        public readonly HashSet<Vector2Int> own = new HashSet<Vector2Int>();     // ô của các khung gốc
        public readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();   // + dải lấn
        public readonly HashSet<Vector2Int> camCells = new HashSet<Vector2Int>(); // = cells + phần lộ ở lối bí mật (chỉ CamBounds)
        public readonly List<string> notes = new List<string>();
        public RectInt Main => rects[0].r;
    }

    struct Opening { public char side; public int line, a, b; public Info nb; }

    public static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).ToList();
        var blocking = maps.Where(t => BlockingMaps.Contains(t.name)).ToList();
        var fill = maps.Where(t => FillMaps.Contains(t.name)).ToList();
        if (blocking.Count == 0) { Debug.LogError("[F3Frames] Không tìm thấy tilemap F3_Ground*"); return; }
        bool Blocked(int x, int y) { var p = new Vector3Int(x, y, 0); return blocking.Any(t => t.HasTile(p)); }

        var infos = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true))
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

        // 1) Mặt nạ ô + tường chặn quái ở lối thông
        var gates = new List<(Vector2 center, Vector2 size, string name)>();
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
                // mỗi lối thông chỉ tạo 1 tường (từ phía phòng bên trái / bên dưới)
                int len = op.b - op.a + 1;
                if (op.side == 'R') gates.Add((new Vector2(op.line + 1, op.a + len / 2f), new Vector2(0.4f, len), $"Gate_{i.room.name}_{op.nb.room.name}"));
                if (op.side == 'T') gates.Add((new Vector2(op.a + len / 2f, op.line + 1), new Vector2(len, 0.4f), $"Gate_{i.room.name}_{op.nb.room.name}"));
                if (op.side == 'T' && SecretRooms.Contains(op.nb.room.name))
                {
                    // Đoạn ô liền của hàng viền trên chứa lỗ → phần nhô không thò ra ngoài bề ngang của phòng
                    int mid = (op.a + op.b) / 2, rx0 = mid, rx1 = mid;
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
            if (vc == null) { Debug.LogWarning($"[F3Frames] {go.name}: chưa có vcam"); continue; }
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

        // 3) Tường chặn quái ở chỗ chuyển phòng (tạo lại mỗi lần)
        var oldGate = roots.FirstOrDefault(g => g.name == GateRoot);
        if (oldGate != null) Object.DestroyImmediate(oldGate);
        var gateRoot = new GameObject(GateRoot);
        SceneManager.MoveGameObjectToScene(gateRoot, scene);
        foreach (var (center, size, name) in gates)
        {
            var g = new GameObject(name) { tag = "EnemyWall" };
            g.transform.SetParent(gateRoot.transform, false);
            g.transform.position = center;
            var bc = g.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = size;
        }

        // 3b) Vệt sáng ở lỗ trần/sàn, đúng ranh giới 2 phòng (giống vệt sáng giữa hầm ngang của F3RoomMerge)
        roots = scene.GetRootGameObjects();   // GateRoot cũ vừa bị xóa → lấy lại danh sách root
        var oldGlow = roots.FirstOrDefault(g => g.name == HoleGlowRoot);
        if (oldGlow != null) Object.DestroyImmediate(oldGlow);
        var glowRoot = new GameObject(HoleGlowRoot);
        SceneManager.MoveGameObjectToScene(glowRoot, scene);
        var glow = F3RoomMerge.GlowSprite();
        foreach (var (center, size, name) in gates.Where(g => g.size.x > g.size.y))
        {
            var go = new GameObject("Glow" + name.Substring("Gate".Length));
            go.transform.SetParent(glowRoot.transform, false);
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = glow; sr.color = F3RoomMerge.GlowColor; sr.sortingOrder = -4;
            if (glow != null)
            {
                var s = glow.bounds.size;
                go.transform.localScale = new Vector3((size.x + 1f) / s.x, 3f / s.y, 1f);   // phủ bề ngang lỗ, cao 3 ô (lỗ dày 2 ô)
            }
            var pulse = go.AddComponent<GlowPulse>();
            if (SecretRooms.Any(s => name.EndsWith("_" + s))) pulse.SetRange(0.04f, 0.12f);   // lối bí mật: chỉ lờ mờ
        }

        // 4) Lấp tile ở những chỗ camera có thể nhìn thấy mà chưa có gì
        bool Covered(int x, int y)
        {
            var p = new Vector3Int(x, y, 0);
            if (maps.Any(t => t.HasTile(p))) return true;
            return owner.ContainsKey(new Vector2Int(x, y));
        }
        int painted = 0;
        var paintedRooms = new HashSet<string>();
        foreach (var i in infos)
        {
            int x0 = i.camCells.Min(p => p.x), x1 = i.camCells.Max(p => p.x), y0 = i.camCells.Min(p => p.y), y1 = i.camCells.Max(p => p.y);
            int ex = Mathf.CeilToInt(CamHalfW), ey = Mathf.CeilToInt(CamHalfH) + 1;
            for (int x = x0 - ex; x <= x1 + ex; x++)
                for (int y = y0 - ey; y <= y1 + ey; y++)
                {
                    if (Covered(x, y)) continue;
                    if (CopyNearest(fill, x, y)) { painted++; paintedRooms.Add(i.room.name); }
                }
        }

        FixBackgroundGaps(roots, maps, infos);

        Debug.Log($"[F3Frames] Xong {infos.Count} phòng, {gates.Count} chỗ chuyển phòng. Vẽ thêm {painted} ô tile{(painted > 0 ? " quanh " + string.Join(", ", paintedRooms) : "")}.");
        foreach (var i in infos)
            Debug.Log($"[F3Frames] {i.room.name} [{string.Join("+", i.rects.Select(r => r.id))}]: {string.Join(", ", i.notes)}");
    }

    /// Lần đầu (phòng chưa có RoomRect nào): lưu khung chữ nhật hiện tại của collider vào con "RoomRect".
    public static void EnsureRoomRect(Room r)
    {
        if (r.transform.Cast<Transform>().Any(t => t.name.StartsWith("RoomRect"))) return;
        var b = r.GetComponent<Collider2D>().bounds;
        var go = new GameObject("RoomRect") { layer = r.gameObject.layer };
        go.transform.SetParent(r.transform, false);
        go.transform.position = Vector3.zero;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.offset = b.center; box.size = b.size;
        box.enabled = false;   // chỉ là dữ liệu, không tham gia vật lý
    }

    /// Các khung chữ nhật gốc của phòng; khung của chính phòng đứng đầu.
    static List<(string id, RectInt r)> RoomRects(Room r)
    {
        EnsureRoomRect(r);
        var list = new List<(string id, RectInt r)>();
        foreach (Transform t in r.transform)
        {
            if (!t.name.StartsWith("RoomRect")) continue;
            var box = t.GetComponent<BoxCollider2D>();
            if (box == null) continue;
            Vector2 min = (Vector2)t.TransformPoint(box.offset) - box.size / 2f;
            string id = t.name == "RoomRect" ? r.name : t.name.Substring("RoomRect_".Length);
            list.Add((id, new RectInt(Mathf.RoundToInt(min.x), Mathf.RoundToInt(min.y), Mathf.RoundToInt(box.size.x), Mathf.RoundToInt(box.size.y))));
        }
        return list.OrderBy(p => p.id == r.name ? 0 : 1).ThenBy(p => p.id).ToList();
    }

    /// Lối thông của phòng: ô viền không bị chặn mà ô kề ngoài thuộc phòng khác; gom thành đoạn liền.
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

    /// Viền ngoài (ngược chiều kim đồng hồ) của tập ô, đã gộp các điểm thẳng hàng.
    static List<Vector2Int> Outline(HashSet<Vector2Int> cells)
    {
        var next = new Dictionary<Vector2Int, Vector2Int>();
        void E(Vector2Int a, Vector2Int b) => next[a] = b;
        foreach (var c in cells)
        {
            int x = c.x, y = c.y;
            if (!cells.Contains(new Vector2Int(x, y - 1))) E(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (!cells.Contains(new Vector2Int(x + 1, y))) E(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
            if (!cells.Contains(new Vector2Int(x, y + 1))) E(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
            if (!cells.Contains(new Vector2Int(x - 1, y))) E(new Vector2Int(x, y + 1), new Vector2Int(x, y));
        }
        var start = next.Keys.OrderBy(p => p.y).ThenBy(p => p.x).First();
        var loop = new List<Vector2Int> { start };
        var cur = next[start];
        int guard = 0;
        while (cur != start && guard++ < 100000) { loop.Add(cur); cur = next[cur]; }
        if (loop.Count < next.Count) Debug.LogWarning($"[F3Frames] Viền chỉ đi qua {loop.Count}/{next.Count} cạnh — tập ô có lỗ hoặc chạm chéo");
        var outl = new List<Vector2Int>();
        for (int k = 0; k < loop.Count; k++)
        {
            var p = loop[(k - 1 + loop.Count) % loop.Count]; var q = loop[k]; var n = loop[(k + 1) % loop.Count];
            var d1 = q - p; var d2 = n - q;
            if (d1.x * d2.y - d1.y * d2.x != 0) outl.Add(q);
        }
        return outl;
    }

    /// Chép tile (kèm ma trận/màu) từ ô có tile gần nhất theo hàng hoặc cột vào ô (x,y).
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

    /// Ô trong khung không có tile và không có ảnh nền nào che → kéo giãn nhóm ảnh nền BG_<id> của khung đó.
    static void FixBackgroundGaps(GameObject[] roots, List<Tilemap> maps, List<Info> infos)
    {
        var bgRoot = roots.FirstOrDefault(g => g.name == "______BackGround______");
        List<Bounds> BgBounds() => bgRoot == null ? new List<Bounds>()
            : bgRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.enabled && s.sprite != null).Select(s => s.bounds).ToList();
        var bgs = BgBounds();
        bool Seen(int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                foreach (var t in maps)
                {
                    var q = new Vector3Int(x + dx, y + dy, 0);
                    var tl = t.GetTile(q) as Tile;
                    if (tl == null) { if (dx == 0 && dy == 0 && t.HasTile(q)) return true; continue; }
                    if (tl.sprite == null) { if (dx == 0 && dy == 0) return true; continue; }
                    var m = t.GetTransformMatrix(q);
                    var sb = tl.sprite.bounds;
                    Vector3 cc = t.GetCellCenterWorld(q) + m.MultiplyPoint3x4(sb.center);
                    Vector3 ext = Vector3.Scale(sb.extents, new Vector3(Mathf.Abs(m.lossyScale.x), Mathf.Abs(m.lossyScale.y), 1));
                    if (Mathf.Abs(x + 0.5f - cc.x) <= ext.x + 0.01f && Mathf.Abs(y + 0.5f - cc.y) <= ext.y + 0.01f) return true;
                }
            var c = new Vector3(x + 0.5f, y + 0.5f, 0);
            return bgs.Any(b => c.x >= b.min.x && c.x <= b.max.x && c.y >= b.min.y && c.y <= b.max.y);
        }
        foreach (var i in infos)
            foreach (var (id, r) in i.rects)
            {
                int gaps = 0;
                for (int x = r.xMin; x < r.xMax; x++) for (int y = r.yMin; y < r.yMax; y++) if (!Seen(x, y)) gaps++;
                if (gaps == 0) continue;
                var group = bgRoot != null ? bgRoot.transform.Find("BG_" + id) : null;
                if (group == null) { Debug.LogWarning($"[F3Frames] {id}: {gaps} ô trống (không tile, không nền), không có BG_{id} để kéo giãn"); continue; }
                var srs = group.GetComponentsInChildren<SpriteRenderer>(true);
                var u = srs[0].bounds; foreach (var s in srs) u.Encapsulate(s.bounds);
                float sx = Mathf.Max(1f, r.width / u.size.x), sy = Mathf.Max(1f, r.height / u.size.y);
                float ox = Mathf.Min(u.min.x, r.xMin), oy = Mathf.Min(u.min.y, r.yMin);
                foreach (var s in srs)
                {
                    var t = s.transform; var p = t.position;
                    t.position = new Vector3(ox + (p.x - u.min.x) * sx, oy + (p.y - u.min.y) * sy, p.z);
                    t.localScale = new Vector3(t.localScale.x * sx, t.localScale.y * sy, t.localScale.z);
                    EditorUtility.SetDirty(t);
                }
                bgs = BgBounds();
                int left = 0;
                for (int x = r.xMin; x < r.xMax; x++) for (int y = r.yMin; y < r.yMax; y++) if (!Seen(x, y)) left++;
                Debug.Log($"[F3Frames] {id}: {gaps} ô trống → kéo giãn BG_{id} x{sx:F3} y{sy:F3}, còn {left} ô");
            }
    }
}
