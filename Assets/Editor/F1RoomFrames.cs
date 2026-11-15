using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chia phòng FirstFloorScene 1 theo các RoomGlow đặt tay (tool KHÔNG tạo / xóa / sửa RoomGlow, RoomGate).
///  1) Gộp / tạo phòng theo bảng Groups + Rects. Phòng đầu nhóm giữ id; tham chiếu tới phòng/vcam bị gộp trỏ sang phòng đầu nhóm.
///  2) Đường hầm (tường dày) trong bảng Tunnels: chỉ đắp nếu chưa đắp (đã đắp rồi thì bỏ qua, không đụng tới).
///  3) Mỗi phòng = hợp các khung chữ nhật (con "RoomRect_<id>", BoxCollider2D tắt — chỉ là dữ liệu).
///     Ranh giới phòng đi qua các RoomGlow; chỗ thông khác giữa 2 phòng là ranh giới hở (không cần bọc kín, không có vệt sáng).
///  4) Collider phòng = khung + dải lấn ở mỗi chỗ thông (1 ô qua cửa ngang, 3 ô qua lỗ trần/sàn) để chuyển phòng không giật;
///     CamBounds = cùng đa giác.
///  5) Ô trong khung phòng (+1 ô viền cho camera phối cảnh) chưa có tile/ảnh nền → vẽ thêm
///     (trong vùng chơi = gạch nền chép từ BackGround, ngoài = đá tối Room1).
/// Chạy: Tools > Culinaria > F1 Redivide Rooms (mở FirstFloorScene 1 trước). Chạy lại được.
public static class F1RoomFrames
{
    const string SceneName = "FirstFloorScene 1";
    const string BackupPath = "Assets/Scenes/Backup/FirstFloorScene 1_2026-11-15_beforeRedivide.unity";
    const int StripDepth = 3, StripDepthSide = 1, StripMargin = 1;

    static readonly string[] BlockingMaps = { "Tilemap", "PlatForm", "Room1_PlatForm", "DeadZone" };
    static readonly string[] CoverMaps = { "Tilemap", "PlatForm", "Room1_PlatForm", "BackGround", "Room1" };

    /// Phòng đầu nhóm ← các phòng gộp vào
    static readonly (string leader, string[] members)[] Groups =
    {
        ("F1_R01", new[] { "F1_R02" }),             // hành lang vào + giếng
        ("F1_R04", new[] { "F1_R05" }),             // khối giữa + hành lang giữa
    };

    /// Khung chữ nhật (xMin, yMin, xMax, yMax) của từng phòng — ranh giới khớp với RoomGlow
    static readonly Dictionary<string, RectInt[]> Rects = new Dictionary<string, RectInt[]>
    {
        // hành lang vào + giếng (xuống tới đáy giếng y 14 = Glow_F1_R03_F1_R01) + phần nhô lên tới sàn sảnh y 39
        ["F1_R01"] = new[] { R(-81, 19, -21, 34), R(-21, 14, -2, 34), R(-44, 34, -18, 39) },
        // sảnh lối xuống: khớp ảnh Tang1_Map1, cửa sang tháp x 5 = Glow_F1_R09_F1_R08
        ["F1_R09"] = new[] { R(-35, 39, 5, 54) },
        // hang dưới: trần y 14 (đáy giếng), phải x 1 (giữa hầm = Glow_F1_R03_F1_R04)
        ["F1_R03"] = new[] { R(-40, -9, 1, 14) },
        // khối giữa + hành lang giữa + sảnh tây (tới x 74 = Glow_F1_R04_F1_R06) + cột cạnh hầm tháp (từ x 53 = Glow_F1_R08_F1_R04)
        ["F1_R04"] = new[] { R(1, -9, 25, 20), R(25, 0, 37, 20), R(37, 6, 57, 25), R(30, 20, 37, 25), R(53, 25, 57, 41), R(57, 6, 74, 41) },
        // sảnh giữa (x 74 → 108 = Glow_F1_R06_F1_R07)
        ["F1_R06"] = new[] { R(74, 6, 108, 41) },
        // khu gai phía đông
        ["F1_R07"] = new[] { R(108, 6, 128, 41) },
        // giếng nối + hành lang tháp + tháp (x 5 → 53)
        ["F1_R08"] = new[] { R(29, 25, 53, 37), R(5, 37, 53, 45), R(10, 45, 31, 52) },
    };
    static RectInt R(int x0, int y0, int x1, int y1) => new RectInt(x0, y0, x1 - x0, y1 - y0);

    /// Đường hầm: các ô được lấp (chỉ ô đang trống), lòng hầm để trống. Đã lấp rồi thì bỏ qua.
    static readonly (string name, RectInt[] fill)[] Tunnels =
    {
        // R03 | R04: lòng hầm x -3..4, y -4..-1 (sàn y -5)
        ("Hầm hang|khối giữa", new[] { R(-3, 0, 5, 3), R(-2, 3, 1, 34), R(1, 3, 5, 14), R(-3, -9, 5, -5) }),
    };

    class Info
    {
        public Room room;
        public readonly HashSet<Vector2Int> own = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public readonly List<string> notes = new List<string>();
    }
    struct Opening { public char side; public int line, a, b; public Info nb; }

    [MenuItem("Tools/Culinaria/F1 Redivide Rooms")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { EditorUtility.DisplayDialog("F1 Rooms", "Hãy mở FirstFloorScene 1 trước.", "OK"); return; }
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            AssetDatabase.CopyAsset(scene.path, BackupPath);
        }
        Merge(scene);
        EnsureRooms(scene);
        BuildTunnels(scene);
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Object VcamOf(Room r) => new SerializedObject(r).FindProperty("vcam").objectReferenceValue;

    // ===================== 1) GỘP / TẠO PHÒNG =====================
    static void Merge(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var rooms = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First());
        var behaviours = roots.SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToList();
        foreach (var (leader, members) in Groups)
        {
            if (!rooms.TryGetValue(leader, out var L)) { Debug.LogWarning("[F1Rooms] Không thấy " + leader); continue; }
            var lv = VcamOf(L) as Component;
            foreach (var id in members)
            {
                if (!rooms.TryGetValue(id, out var M) || M == null) continue;
                var mv = VcamOf(M) as Component;
                var map = new Dictionary<Object, Object> { [M] = L, [M.gameObject] = L.gameObject };
                if (mv != null && lv != null) { map[mv] = lv; map[mv.gameObject] = lv.gameObject; map[mv.transform] = lv.transform; }
                foreach (var b in behaviours)
                {
                    if (b == null || b.transform.IsChildOf(M.transform)) continue;
                    var so = new SerializedObject(b); var it = so.GetIterator(); bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        if (!map.TryGetValue(it.objectReferenceValue, out var to)) continue;
                        it.objectReferenceValue = to; changed = true;
                        Debug.Log($"[F1Rooms] {b.gameObject.name}.{b.GetType().Name}.{it.propertyPath}: {id} → {leader}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(b); }
                }
                Object.DestroyImmediate(M.gameObject);
                Debug.Log($"[F1Rooms] Gộp {id} vào {leader}");
            }
        }
    }

    static void EnsureRooms(Scene scene)
    {
        var rooms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        if (rooms.Count == 0) { Debug.LogError("[F1Rooms] Scene không có Room nào để nhân bản"); return; }
        var template = rooms.First();
        foreach (var id in Rects.Keys)
        {
            if (rooms.Any(r => r.name == id)) continue;
            var go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = id;
            foreach (Transform ch in go.transform.Cast<Transform>().ToList())
                if (ch.GetComponent<CinemachineVirtualCamera>() == null) Object.DestroyImmediate(ch.gameObject);
            var so = new SerializedObject(go.GetComponent<Room>());
            so.FindProperty("roomId").stringValue = id;
            so.FindProperty("vcam").objectReferenceValue = go.GetComponentInChildren<CinemachineVirtualCamera>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[F1Rooms] Tạo phòng mới {id}");
        }
    }

    // ===================== 2) ĐƯỜNG HẦM (chỉ khi chưa đắp) =====================
    static void BuildTunnels(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).ToList();
        var ground = maps.FirstOrDefault(t => t.name == "Tilemap");
        var rock = maps.FirstOrDefault(t => t.name == "Room1");
        var bg = maps.FirstOrDefault(t => t.name == "BackGround");
        var decor = maps.FirstOrDefault(t => t.name == "Room1_Decorate");
        var blocking = maps.Where(t => BlockingMaps.Contains(t.name)).ToList();
        if (ground == null || rock == null) { Debug.LogError("[F1Rooms] Thiếu tilemap Tilemap / Room1"); return; }
        Vector3Int Cell(Tilemap t, int x, int y) => t.WorldToCell(new Vector3(x + 0.5f, y + 0.5f, 0));
        bool Blocked(int x, int y) => blocking.Any(t => t.HasTile(Cell(t, x, y)));
        var rockTile = rock.GetTile(new Vector3Int(-15, 16, 0));
        var all = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var mapBound = all.FirstOrDefault(t => t.name == "MapBoundController")?.GetComponent<PolygonCollider2D>();
        var entry = all.FirstOrDefault(t => t.name == "F1_Up");
        Physics2D.SyncTransforms();

        foreach (var (name, rects) in Tunnels)
        {
            var filled = new HashSet<Vector2Int>();
            foreach (var r in rects)
                for (int x = r.xMin; x < r.xMax; x++) for (int y = r.yMin; y < r.yMax; y++)
                    if (!Blocked(x, y)) filled.Add(new Vector2Int(x, y));
            if (filled.All(c => rock.GetTile(Cell(rock, c.x, c.y)) == rockTile)) { Debug.Log($"[F1Rooms] {name}: đã đắp từ trước, bỏ qua"); continue; }

            var stats = new Dictionary<int, Dictionary<TileBase, int>>();
            var count = new Dictionary<TileBase, int>();
            foreach (var p in ground.cellBounds.allPositionsWithin)
            {
                var t = ground.GetTile(p); if (t == null) continue;
                var w = ground.GetCellCenterWorld(p); int x = Mathf.FloorToInt(w.x), y = Mathf.FloorToInt(w.y);
                int key = (Blocked(x, y + 1) ? 0 : 1) | (Blocked(x, y - 1) ? 0 : 2) | (Blocked(x - 1, y) ? 0 : 4) | (Blocked(x + 1, y) ? 0 : 8);
                if (!stats.TryGetValue(key, out var d)) stats[key] = d = new Dictionary<TileBase, int>();
                d[t] = d.TryGetValue(t, out var n) ? n + 1 : 1;
                count[t] = count.TryGetValue(t, out var m) ? m + 1 : 1;
            }
            TileBase Pick(int key)
            {
                if (stats.TryGetValue(key, out var d)) return d.OrderByDescending(kv => kv.Value).First().Key;
                foreach (int k in new[] { key & 1, key & 2, key & 4, key & 8 }) if (k != 0 && stats.TryGetValue(k, out var d2)) return d2.OrderByDescending(kv => kv.Value).First().Key;
                return count.OrderByDescending(kv => kv.Value).First().Key;
            }

            var reach = new HashSet<Vector2Int>();
            if (entry != null)
            {
                var q = new Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(Mathf.FloorToInt(entry.position.x), Mathf.FloorToInt(entry.position.y)));
                while (q.Count > 0 && reach.Count < 200000)
                {
                    var c = q.Dequeue();
                    if (reach.Contains(c) || filled.Contains(c) || Blocked(c.x, c.y)) continue;
                    if (mapBound != null && !mapBound.OverlapPoint(new Vector2(c.x + 0.5f, c.y + 0.5f))) continue;
                    reach.Add(c);
                    q.Enqueue(c + Vector2Int.left); q.Enqueue(c + Vector2Int.right); q.Enqueue(c + Vector2Int.up); q.Enqueue(c + Vector2Int.down);
                }
            }
            bool Open(int x, int y) => reach.Contains(new Vector2Int(x, y));
            int edge = 0, inner = 0;
            foreach (var c in filled)
            {
                int key = (Open(c.x, c.y + 1) ? 1 : 0) | (Open(c.x, c.y - 1) ? 2 : 0) | (Open(c.x - 1, c.y) ? 4 : 0) | (Open(c.x + 1, c.y) ? 8 : 0);
                if (bg != null) bg.SetTile(Cell(bg, c.x, c.y), null);
                if (decor != null) decor.SetTile(Cell(decor, c.x, c.y), null);
                rock.SetTile(Cell(rock, c.x, c.y), rockTile);
                if (key != 0) { ground.SetTile(Cell(ground, c.x, c.y), Pick(key)); edge++; } else inner++;
            }
            var cc = ground.GetComponent<CompositeCollider2D>(); if (cc != null) cc.GenerateGeometry();
            foreach (var t in new[] { ground, rock, bg, decor }) if (t != null) EditorUtility.SetDirty(t);
            Debug.Log($"[F1Rooms] {name}: lấp {filled.Count} ô ({edge} ô viền, {inner} ô ruột đá)");
        }
    }

    // ===================== 3..5) KHUNG, CHỖ THÔNG, CAMERA, VẼ THÊM =====================
    public static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var maps = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true)).ToList();
        var blocking = maps.Where(t => BlockingMaps.Contains(t.name)).ToList();
        var cover = maps.Where(t => CoverMaps.Contains(t.name)).ToList();
        var bgMap = maps.FirstOrDefault(t => t.name == "BackGround");
        var rockMap = maps.FirstOrDefault(t => t.name == "Room1");
        var bgSprites = roots.Where(g => g.name.Contains("BackGround")).SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true))
                             .Where(s => s.gameObject.activeInHierarchy && s.sprite != null).Select(s => s.bounds).ToList();
        var all = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var mapBound = all.FirstOrDefault(t => t.name == "MapBoundController")?.GetComponent<PolygonCollider2D>();
        var entry = all.FirstOrDefault(t => t.name == "F1_Up");
        Physics2D.SyncTransforms();

        Vector3Int Cell(Tilemap t, int x, int y) => t.WorldToCell(new Vector3(x + 0.5f, y + 0.5f, 0));
        bool Blocked(int x, int y) => blocking.Any(t => t.HasTile(Cell(t, x, y)));
        bool Playable(int x, int y) => mapBound == null || mapBound.OverlapPoint(new Vector2(x + 0.5f, y + 0.5f));
        bool Covered(int x, int y)
        {
            if (cover.Any(t => t.HasTile(Cell(t, x, y)))) return true;
            return bgSprites.Any(bb => bb.min.x <= x + 0.01f && x + 0.99f <= bb.max.x && bb.min.y <= y + 0.01f && y + 0.99f <= bb.max.y);
        }

        var reach = new HashSet<Vector2Int>();
        if (entry != null)
        {
            var q = new Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(Mathf.FloorToInt(entry.position.x), Mathf.FloorToInt(entry.position.y)));
            while (q.Count > 0 && reach.Count < 200000)
            {
                var c = q.Dequeue();
                if (reach.Contains(c) || Blocked(c.x, c.y) || !Playable(c.x, c.y)) continue;
                reach.Add(c);
                q.Enqueue(c + Vector2Int.left); q.Enqueue(c + Vector2Int.right); q.Enqueue(c + Vector2Int.up); q.Enqueue(c + Vector2Int.down);
            }
        }
        System.Func<int, int, bool> Walkable = (x, y) => reach.Count == 0 ? (!Blocked(x, y) && Playable(x, y)) : reach.Contains(new Vector2Int(x, y));

        var infos = new List<Info>();
        foreach (var room in roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).OrderBy(r => r.name))
        {
            if (!Rects.TryGetValue(room.name, out var rs)) { Debug.LogWarning($"[F1Rooms] {room.name}: chưa có khung trong bảng Rects"); continue; }
            foreach (Transform ch in room.transform.Cast<Transform>().ToList())
                if (ch.name.StartsWith("RoomRect") || ch.name == "CamBounds" || ch.name.StartsWith("Buffer")) Object.DestroyImmediate(ch.gameObject);
            var info = new Info { room = room };
            for (int k = 0; k < rs.Length; k++)
            {
                var r = rs[k];
                var go = new GameObject("RoomRect_" + room.name + (k == 0 ? "" : "_" + k)) { layer = room.gameObject.layer };
                go.transform.SetParent(room.transform, false);
                go.transform.position = Vector3.zero;
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true; box.offset = r.center; box.size = r.size; box.enabled = false;
                for (int x = r.xMin; x < r.xMax; x++) for (int y = r.yMin; y < r.yMax; y++) info.own.Add(new Vector2Int(x, y));
            }
            infos.Add(info);
        }
        var owner = new Dictionary<Vector2Int, Info>();
        foreach (var i in infos) foreach (var c in i.own)
        {
            if (owner.TryGetValue(c, out var o)) Debug.LogWarning($"[F1Rooms] Ô {c} thuộc cả {o.room.name} và {i.room.name}");
            else owner[c] = i;
        }

        // Dải lấn ở mỗi chỗ thông (chỉ để chuyển phòng không giật; không tạo RoomGlow / RoomGate)
        foreach (var i in infos)
        {
            i.cells.UnionWith(i.own);
            foreach (var op in Openings(i, owner, Walkable))
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
            }
        }

        foreach (var i in infos)
        {
            var go = i.room.gameObject;
            var loop = Outline(i.cells);
            var poly = go.GetComponent<PolygonCollider2D>();
            poly.isTrigger = true; poly.offset = Vector2.zero; poly.pathCount = 1;
            poly.SetPath(0, loop.Select(p => (Vector2)go.transform.InverseTransformPoint(new Vector3(p.x, p.y, 0))).ToArray());

            var cb = new GameObject("CamBounds") { layer = go.layer };
            cb.transform.SetParent(go.transform, false);
            cb.transform.position = Vector3.zero;
            var bounds = cb.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true; bounds.pathCount = 1;
            bounds.SetPath(0, loop.Select(p => (Vector2)cb.transform.InverseTransformPoint(new Vector3(p.x, p.y, 0))).ToArray());

            var vc = VcamOf(i.room) as CinemachineVirtualCamera;
            if (vc == null) { Debug.LogWarning($"[F1Rooms] {go.name}: chưa có vcam"); continue; }
            vc.name = "VCam_" + go.name;
            vc.Priority = 0;
            var conf = vc.GetComponent<CinemachineConfiner2D>();
            if (conf == null) conf = vc.gameObject.AddComponent<CinemachineConfiner2D>();
            conf.m_BoundingShape2D = bounds; conf.m_Damping = 0f; conf.InvalidateCache();
            var c0 = Rects[go.name][0].center;
            vc.transform.position = new Vector3(c0.x, c0.y, vc.transform.position.z);
            EditorUtility.SetDirty(conf); EditorUtility.SetDirty(vc); EditorUtility.SetDirty(poly); EditorUtility.SetDirty(bounds);
        }

        var rockTile = rockMap != null ? rockMap.GetTile(new Vector3Int(-15, 16, 0)) : null;
        int bgPainted = 0, rockPainted = 0;
        var visible = new HashSet<Vector2Int>();
        foreach (var i in infos)
            foreach (var c in i.own)
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) visible.Add(c + new Vector2Int(dx, dy));
        foreach (var c in visible)
        {
            if (Covered(c.x, c.y)) continue;
            if (Playable(c.x, c.y) && bgMap != null && CopyNearest(bgMap, c.x, c.y)) { bgPainted++; continue; }
            if (rockMap != null && rockTile != null) { rockMap.SetTile(Cell(rockMap, c.x, c.y), rockTile); rockPainted++; }
        }
        if (bgMap != null) EditorUtility.SetDirty(bgMap);
        if (rockMap != null) EditorUtility.SetDirty(rockMap);

        Debug.Log($"[F1Rooms] Xong {infos.Count} phòng. Vẽ thêm {bgPainted} ô gạch nền + {rockPainted} ô đá tối. (Không đụng tới RoomGlow / RoomGate)");
        foreach (var i in infos) Debug.Log($"[F1Rooms] {i.room.name}: {string.Join(", ", i.notes)}");
    }

    static List<Opening> Openings(Info i, Dictionary<Vector2Int, Info> owner, System.Func<int, int, bool> walkable)
    {
        var raw = new Dictionary<(char side, int line, Info nb), List<int>>();
        var dirs = new[] { ('L', new Vector2Int(-1, 0)), ('R', new Vector2Int(1, 0)), ('B', new Vector2Int(0, -1)), ('T', new Vector2Int(0, 1)) };
        foreach (var c in i.own)
            foreach (var (s, d) in dirs)
            {
                var o = c + d;
                if (i.own.Contains(o) || !walkable(c.x, c.y) || !walkable(o.x, o.y)) continue;
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
                if (k < v.Count) start = prev = v[k];
            }
        }
        return res.OrderBy(o => o.side).ThenBy(o => o.a).ToList();
    }

    /// Viền ngoài (ngược chiều kim đồng hồ) của tập ô; ở đỉnh 2 ô chỉ chạm góc luôn rẽ trái → không nhảy nhánh.
    static List<Vector2Int> Outline(HashSet<Vector2Int> cells)
    {
        var outE = new Dictionary<Vector2Int, List<Vector2Int>>();
        void E(Vector2Int a, Vector2Int b) { if (!outE.TryGetValue(a, out var l)) outE[a] = l = new List<Vector2Int>(); l.Add(b); }
        foreach (var c in cells)
        {
            int x = c.x, y = c.y;
            if (!cells.Contains(new Vector2Int(x, y - 1))) E(new Vector2Int(x, y), new Vector2Int(x + 1, y));
            if (!cells.Contains(new Vector2Int(x + 1, y))) E(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
            if (!cells.Contains(new Vector2Int(x, y + 1))) E(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
            if (!cells.Contains(new Vector2Int(x - 1, y))) E(new Vector2Int(x, y + 1), new Vector2Int(x, y));
        }
        var loops = new List<List<Vector2Int>>();
        while (outE.Count > 0)
        {
            var start = outE.Keys.OrderBy(p => p.y).ThenBy(p => p.x).First();
            var loop = new List<Vector2Int>();
            var cur = start; var dir = Vector2Int.zero; int guard = 0;
            while (outE.TryGetValue(cur, out var l) && guard++ < 200000)
            {
                Vector2Int nxt;
                if (l.Count == 1 || dir == Vector2Int.zero) nxt = l[0];
                else { var left = new Vector2Int(-dir.y, dir.x); nxt = l.OrderByDescending(n => { var d = n - cur; return d == left ? 2 : d == dir ? 1 : 0; }).First(); }
                l.Remove(nxt); if (l.Count == 0) outE.Remove(cur);
                loop.Add(cur); dir = nxt - cur; cur = nxt;
                if (cur == start) break;
            }
            loops.Add(loop);
        }
        if (loops.Count > 1) Debug.LogWarning($"[F1Rooms] Tập ô có {loops.Count} vòng viền — dùng vòng ngoài dài nhất");
        var best = loops.OrderByDescending(l => l.Count).First();
        var outl = new List<Vector2Int>();
        for (int k = 0; k < best.Count; k++)
        {
            var p = best[(k - 1 + best.Count) % best.Count]; var q = best[k]; var n = best[(k + 1) % best.Count];
            var d1 = q - p; var d2 = n - q;
            if (d1.x * d2.y - d1.y * d2.x != 0) outl.Add(q);
        }
        return outl;
    }

    static bool CopyNearest(Tilemap t, int x, int y)
    {
        var q = t.WorldToCell(new Vector3(x + 0.5f, y + 0.5f, 0));
        for (int d = 1; d <= 40; d++)
            foreach (var p in new[] { q + new Vector3Int(d, 0, 0), q - new Vector3Int(d, 0, 0), q + new Vector3Int(0, d, 0), q - new Vector3Int(0, d, 0) })
            {
                var tile = t.GetTile(p);
                if (tile == null) continue;
                t.SetTile(q, tile);
                t.SetTileFlags(q, TileFlags.None);
                t.SetTransformMatrix(q, t.GetTransformMatrix(p));
                t.SetColor(q, t.GetColor(p));
                return true;
            }
        return false;
    }
}
