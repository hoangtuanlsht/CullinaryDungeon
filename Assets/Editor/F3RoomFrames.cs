using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Chia lại khung phòng ThirdFloorScene theo cùng cách với FirstFloorScene 1:
///  - Collider gốc của Room = vùng nhận diện phòng (giữ khít ô phòng, không chồng phòng khác → không nhận nhầm).
///  - Con "Buffer_*" = dải đệm lấn BUFFER ô sang phòng bên cạnh, CHỈ ở chỗ có lối thông (cửa, lỗ trần/sàn, vách đã mở).
///    Room.Contains() dùng các dải này để GIỮ phòng hiện tại → đứng ở cửa không làm camera nhảy qua lại.
///  - Con "CamBounds" = khung giới hạn camera (Confiner2D). Các phòng hành lang nối liền nhau (vách đã mở)
///    dùng CHUNG một CamBounds bao cả dải → camera trượt liền mạch, không khựng ở ranh giới phòng.
///  - VCam: FramingTransposer damping 0.2, screenY 0.7, ortho 5 (giống tầng 1).
/// Chạy lại được nhiều lần (xoá CamBounds/Buffer cũ rồi tạo lại). Không đụng tới tile, quái hay đồ trang trí.
public static class F3RoomFrames
{
    const string SceneName = "ThirdFloorScene";
    const string Prefix = "F3_R";
    const float BufferDepth = 3f;     // dải đệm lấn sang phòng kế bên (ô)
    const float BufferMargin = 1f;    // nới thêm 2 đầu dải đệm dọc theo cạnh
    const float MergeRatio = 0.75f;   // cạnh chung mở >= 75% → coi là 1 hành lang liền

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
        public Room room; public RectInt r;
        public readonly List<(char side, int a, int b)> openings = new List<(char, int, int)>();
        public Info parent;
        public Info Root() => parent == null ? this : (parent = parent.Root());
    }

    public static void Apply(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var blocking = roots.SelectMany(g => g.GetComponentsInChildren<Tilemap>(true))
            .Where(t => t.name == "F3_Ground" || t.name == "F3_Ground_KnightDungeon" || t.name == "F3_DeadZone_Lava").ToList();
        if (blocking.Count == 0) { Debug.LogError("[F3Frames] Không tìm thấy tilemap đất F3_Ground*"); return; }
        bool Blocked(int x, int y) { var p = new Vector3Int(x, y, 0); return blocking.Any(t => t.HasTile(p)); }

        var infos = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true))
            .Where(r => r.name.StartsWith(Prefix)).OrderBy(r => r.name)
            .Select(r =>
            {
                var b = r.GetComponent<Collider2D>().bounds;
                return new Info
                {
                    room = r,
                    r = new RectInt(Mathf.RoundToInt(b.min.x), Mathf.RoundToInt(b.min.y), Mathf.RoundToInt(b.size.x), Mathf.RoundToInt(b.size.y))
                };
            }).ToList();

        // 1) Tìm lối thông trên viền mỗi phòng (ô viền không có đất/dung nham)
        foreach (var i in infos)
        {
            var r = i.r;
            Scan(i, 'L', r.yMin + 1, r.yMax - 2, y => !Blocked(r.xMin, y));
            Scan(i, 'R', r.yMin + 1, r.yMax - 2, y => !Blocked(r.xMax - 1, y));
            Scan(i, 'B', r.xMin + 1, r.xMax - 2, x => !Blocked(x, r.yMin));
            Scan(i, 'T', r.xMin + 1, r.xMax - 2, x => !Blocked(x, r.yMax - 1));
        }

        // 2) Gộp các phòng hành lang nối liền (cùng cao độ, vách chung đã mở gần hết)
        foreach (var a in infos)
            foreach (var b in infos)
            {
                if (a == b || a.r.xMax != b.r.xMin || a.r.yMin != b.r.yMin || a.r.yMax != b.r.yMax) continue;
                int open = a.openings.Where(o => o.side == 'R').Sum(o => o.b - o.a + 1);
                if (open >= (a.r.height - 2) * MergeRatio && a.Root() != b.Root()) b.Root().parent = a.Root();
            }

        int nBuf = 0, nShared = 0;
        var sharedBounds = new Dictionary<Info, PolygonCollider2D>();
        foreach (var i in infos)
        {
            var go = i.room.gameObject;
            int roomLayer = go.layer;
            // Xoá khung cũ
            foreach (Transform ch in go.transform.Cast<Transform>().ToList())
                if (ch.name == "CamBounds" || ch.name.StartsWith("Buffer")) Object.DestroyImmediate(ch.gameObject);

            // Collider gốc: đúng khung ô phòng
            var poly = go.GetComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.offset = Vector2.zero;
            poly.pathCount = 1;
            poly.SetPath(0, RectPath(i.r, go.transform));

            // Dải đệm ở mỗi lối thông
            int k = 0;
            foreach (var o in i.openings)
            {
                Rect br;
                float a = o.a - BufferMargin, b = o.b + 1 + BufferMargin;
                switch (o.side)
                {
                    case 'L': br = Rect.MinMaxRect(i.r.xMin - BufferDepth, a, i.r.xMin + 1, b); break;
                    case 'R': br = Rect.MinMaxRect(i.r.xMax - 1, a, i.r.xMax + BufferDepth, b); break;
                    case 'B': br = Rect.MinMaxRect(a, i.r.yMin - BufferDepth, b, i.r.yMin + 1); break;
                    default:  br = Rect.MinMaxRect(a, i.r.yMax - 1, b, i.r.yMax + BufferDepth); break;
                }
                var bgo = new GameObject($"Buffer_{o.side}{k++}") { layer = roomLayer };
                bgo.transform.SetParent(go.transform, false);
                bgo.transform.position = Vector3.zero;
                var bc = bgo.AddComponent<BoxCollider2D>();
                bc.isTrigger = true;
                bc.offset = br.center - (Vector2)go.transform.position;
                bc.offset = br.center;
                bgo.transform.position = Vector3.zero;
                bc.size = br.size;
                nBuf++;
            }

            // Khung camera: 1 cho mỗi dải hành lang liền (đặt ở phòng đầu dải), phòng lẻ có khung riêng
            var root = i.Root();
            if (!sharedBounds.TryGetValue(root, out var bounds))
            {
                var group = infos.Where(x => x.Root() == root).ToList();
                var u = group.Select(x => x.r).Aggregate((p, q) => new RectInt(Mathf.Min(p.xMin, q.xMin), Mathf.Min(p.yMin, q.yMin),
                    Mathf.Max(p.xMax, q.xMax) - Mathf.Min(p.xMin, q.xMin), Mathf.Max(p.yMax, q.yMax) - Mathf.Min(p.yMin, q.yMin)));
                var host = root.room.gameObject;
                var cb = new GameObject("CamBounds") { layer = roomLayer };
                cb.transform.SetParent(host.transform, false);
                cb.transform.position = Vector3.zero;
                bounds = cb.AddComponent<PolygonCollider2D>();
                bounds.isTrigger = true;
                bounds.pathCount = 1;
                bounds.SetPath(0, RectPath(u, cb.transform));
                sharedBounds[root] = bounds;
                if (group.Count > 1) { nShared++; Debug.Log($"[F3Frames] Hành lang liền: {string.Join(" + ", group.Select(x => x.room.name))} → khung {u}"); }
            }

            // VCam giống tầng 1
            var so = new SerializedObject(i.room);
            var vc = so.FindProperty("vcam").objectReferenceValue as CinemachineVirtualCamera;
            if (vc == null) { Debug.LogWarning($"[F3Frames] {go.name}: chưa có vcam"); continue; }
            vc.Priority = 0;
            vc.m_Lens.OrthographicSize = 5f;
            var ft = vc.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (ft != null)
            {
                ft.m_XDamping = 0.2f; ft.m_YDamping = 0.2f;
                ft.m_DeadZoneWidth = 0f; ft.m_DeadZoneHeight = 0f;
                ft.m_ScreenX = 0.5f; ft.m_ScreenY = 0.7f;
                EditorUtility.SetDirty(ft);
            }
            var conf = vc.GetComponent<CinemachineConfiner2D>() ?? vc.gameObject.AddComponent<CinemachineConfiner2D>();
            conf.m_BoundingShape2D = bounds;
            conf.m_Damping = 0f;
            conf.InvalidateCache();
            var c = bounds.bounds.center;
            vc.transform.position = new Vector3(c.x, c.y, vc.transform.position.z);
            EditorUtility.SetDirty(conf); EditorUtility.SetDirty(vc); EditorUtility.SetDirty(poly);
        }
        Debug.Log($"[F3Frames] Xong: {infos.Count} phòng, {nBuf} dải đệm, {nShared} hành lang dùng chung khung camera");
        foreach (var i in infos)
            Debug.Log($"[F3Frames] {i.room.name} {i.r} lối thông: {string.Join(", ", i.openings.Select(o => $"{o.side}[{o.a}..{o.b}]"))}");
    }

    static void Scan(Info i, char side, int from, int to, System.Func<int, bool> open)
    {
        int start = int.MinValue;
        for (int v = from; v <= to + 1; v++)
        {
            bool o = v <= to && open(v);
            if (o && start == int.MinValue) start = v;
            if (!o && start != int.MinValue) { i.openings.Add((side, start, v - 1)); start = int.MinValue; }
        }
    }

    static Vector2[] RectPath(RectInt r, Transform t)
    {
        Vector2 L(float x, float y) => t.InverseTransformPoint(new Vector3(x, y, 0));
        return new[] { L(r.xMin, r.yMin), L(r.xMax, r.yMin), L(r.xMax, r.yMax), L(r.xMin, r.yMax) };
    }
}
