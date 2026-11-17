using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// [Tầng 4] Nền nhiều lớp mới (bộ v2 do người dùng tạo bằng AI, đã thu nhỏ 1/5 + gom 40 màu):
///  - Khu A–C (hồ hang): Assets/Resouces/F4/TrenMatNuoc_v2  — L0 xa, L1 phế tích, L2 dải thạch nhũ trên + 2 khung hai đầu; R07 có tháp riêng.
///  - Khu D (thành phố chìm): Assets/Resouces/F4/DuoiNuoc_v2 — L0 nước xa, L1 phế tích, L2 dải tảo đáy + 2 khung; R20 có thành riêng. Phòng càng sâu càng tối.
///  - Khu E (lòng cột trụ R28–R30): Assets/Resouces/F4/TrongCot_v2 — sảnh lặp ngang + dọc, 2 cột hai đầu. Vẽ trên tường gạch BackWall (-12).
/// Mỗi lớp là một nhóm ở độ sâu z riêng, phóng (D+z)/D quanh tâm khung camera của phòng (quy trình culinaria-bg-depth-group),
/// nên khi camera bị giới hạn trong phòng thì nền luôn phủ kín khung hình. Lớp gần (khung, dải) được đặt bù trượt để
/// chạm đúng mép phòng khi camera đứng sát mép đó.
/// Mọi thứ nằm dưới root riêng "______SceneBG2_F4_____". Nền cũ của các phòng này và root the_drowned bị TẮT (không xóa). Phòng boss R27 giữ nguyên.
/// Chạy: Tools > Culinaria > F4 Layered Backdrops (v2). Chạy lại được. Gỡ: xóa root rồi bật lại nền cũ.
public static class F4LayeredBackdrops
{
    const string SceneName = "FourthFloorScene";
    const string ScenePath = "Assets/Scenes/FourthFloorScene.unity";
    const string RootName = "______SceneBG2_F4_____";
    const string OldSceneRoot = "______SceneBG_F4_____";      // ảnh the_drowned cũ ở khu D
    const string Lake = "Assets/Resouces/F4/TrenMatNuoc_v2/F4_BG2_";
    const string Sea = "Assets/Resouces/F4/DuoiNuoc_v2/F4_BG2_";
    const string Pillar = "Assets/Resouces/F4/TrongCot_v2/F4_BG2_";
    const float PPU = 17.1f;          // 188 px ≈ 11 ô
    const float D = 18.7f;            // khoảng cách camera = 5 / tan(15°)
    const float HalfH = 5f, HalfW = 5f * 16f / 9f;   // nửa khung hình ở mặt phẳng địa hình (16:9)

    enum Zone { Lake, Sea, Pillar }
    /// (phòng, khu, độ sáng, vùng cảnh đặc trưng (x trái, y đáy, rộng, cao) hoặc null)
    static readonly (string room, Zone zone, float light, RectInt? landmark)[] Rooms =
    {
        ("F4_R01", Zone.Lake, 1f, null), ("F4_R04", Zone.Lake, 0.95f, null), ("F4_R05", Zone.Lake, 1f, null),
        ("F4_R07", Zone.Lake, 1f, new RectInt(72, -60, 36, 20)),     // tháp cầu thang xoắn ở phần R07 (2×2)
        ("F4_R08", Zone.Lake, 0.95f, null), ("F4_R10", Zone.Lake, 0.95f, null), ("F4_R12", Zone.Lake, 1f, null),
        ("F4_H1", Zone.Lake, 0.7f, null), ("F4_H2", Zone.Lake, 0.7f, null), ("F4_H3", Zone.Lake, 0.7f, null),
        ("F4_CUR", Zone.Sea, 1f, null), ("F4_R15", Zone.Sea, 1f, null), ("F4_R16", Zone.Sea, 1f, null), ("F4_R17", Zone.Sea, 1f, null),
        ("F4_R18", Zone.Sea, 1f, null), ("F4_R20", Zone.Sea, 1f, new RectInt(90, -140, 36, 20)),   // thành kim tự tháp ở phần R20 (2×2)
        ("F4_R23", Zone.Sea, 1f, null), ("F4_R24", Zone.Sea, 1f, null), ("F4_R26", Zone.Sea, 1f, null),
        ("F4_H4", Zone.Sea, 0.75f, null), ("F4_H5", Zone.Sea, 0.75f, null), ("F4_H6", Zone.Sea, 0.75f, null), ("F4_H7", Zone.Sea, 0.75f, null),
        ("F4_R28", Zone.Pillar, 1f, null),
    };

    static Material mat;
    static List<string> log;
    static int sprites;

    [MenuItem("Tools/Culinaria/F4 Layered Backdrops (v2)")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (opened) { EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true); Debug.Log("[F4Bg2] Đã lưu và đóng FourthFloorScene"); }
        else Debug.Log("[F4Bg2] Scene đang mở: chưa lưu, xem thử rồi Ctrl+S");
    }

    /// Gọi được từ FourthFloorBuilder.Build() (scene đang mở, builder tự lưu).
    public static void Apply(Scene scene)
    {
        log = new List<string>(); sprites = 0;
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        // Dùng chung material với nền cũ (URP 2D: Sprite-Lit-Default sẽ tối nếu không có Light2D)
        var oldBgRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "______Background_____");
        mat = oldBgRoot ? oldBgRoot.GetComponentsInChildren<SpriteRenderer>(true).Where(s => s.name != "BG_Dark_FullMap").Select(s => s.sharedMaterial).FirstOrDefault(m => m != null) : null;
        log.Add("Material: " + (mat ? mat.name : "mặc định"));

        var rooms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        foreach (var (roomName, zone, light, landmark) in Rooms)
        {
            var room = rooms.FirstOrDefault(r => r.name == roomName);
            if (room == null) { log.Add("Không thấy phòng " + roomName); continue; }
            Rect R = CamRect(room);
            var bg = new GameObject("BG2_" + roomName); bg.transform.SetParent(root.transform, false);
            switch (zone)
            {
                case Zone.Lake: BuildLake(bg.transform, R, light, landmark); break;
                case Zone.Sea: BuildSea(bg.transform, R, light, landmark); break;
                case Zone.Pillar: BuildPillar(bg.transform, R); break;
            }

            var so = new SerializedObject(room);
            var p = so.FindProperty("background");
            if (p == null) { log.Add("Room.cs chưa có trường background"); continue; }
            var cur = p.objectReferenceValue as GameObject;
            if (cur != null && cur != bg && cur.transform.root.name != "______Background_____" && cur.transform.root.name != RootName)
            { log.Add($"{roomName}: ô Background là '{cur.name}' (không phải nền của builder), KHÔNG ghi đè"); continue; }
            if (cur != null && cur.transform.root.name == "______Background_____") cur.SetActive(false);
            p.objectReferenceValue = bg; so.ApplyModifiedPropertiesWithoutUndo();
            bg.SetActive(false);   // RoomTracker bật nền của phòng hiện tại
        }

        // Nền cũ theo phòng còn sót (BG_F4_Rxx không được Room nào trỏ tới) của các phòng đã có nền mới → tắt
        if (oldBgRoot != null)
        {
            var mine = new HashSet<string>(Rooms.Select(r => r.room));
            foreach (Transform t in oldBgRoot.transform)
            {
                string id = t.name.StartsWith("BGGroup_") ? t.name.Substring(8) : t.name.StartsWith("BG_F4_") ? t.name.Substring(3) : null;
                if (id != null && id != "F4_R27" && (mine.Contains(id) || id == "F4_R25" || id == "F4_R09" || id == "F4_R13")) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); }
            }
        }
        var drowned = scene.GetRootGameObjects().FirstOrDefault(g => g.name == OldSceneRoot);
        if (drowned != null && drowned.activeSelf) { drowned.SetActive(false); log.Add("Tắt " + OldSceneRoot + " (ảnh the_drowned cũ ở khu D)"); }

        Debug.Log($"[F4Bg2] {sprites} ảnh nền cho {Rooms.Length} phòng. " + string.Join(" | ", log));
    }

    static Rect CamRect(Room room)
    {
        var cb = room.transform.Find("CamBounds");
        var c = cb ? cb.GetComponent<Collider2D>() : room.GetComponent<Collider2D>();
        var b = c.bounds;
        return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
    }

    // ---------------- Các khu ----------------
    static void BuildLake(Transform bg, Rect R, float light, RectInt? lm)
    {
        var l0 = Load(Lake + "Lake_L0_Far"); var l1 = Load(Lake + "Lake_L1_Ruins");
        var top = Load(Lake + "Lake_L2_TopStrip");
        var (slT, slB) = LoadHalves(Lake + "Lake_L2_SideLeft"); var (srT, srB) = LoadHalves(Lake + "Lake_L2_SideRight");
        Color c = new Color(light, light, light, 1);
        Fill(bg, "L0_Far", l0, R, 2f * D, R.height, -26, c);
        FillBottom(bg, "L1_Ruins", l1, R, D, Mathf.Clamp(R.height, 11f, 14f), -25, c);
        if (lm.HasValue) Landmark(bg, Load(Lake + "R07_Tower"), srB, R, lm.Value, 0.5f, -24, c, srT);   // mép trái tháp che bằng nửa dưới khung đá
        float zn = 0.25f * D;
        TopStrip(bg, "L2_Top", top, R, zn, top ? top.rect.height / PPU : 0, -23, c);
        LakeCorners(bg, slT, slB, srT, srB, R, zn, -23, c);
    }

    static void BuildSea(Transform bg, Rect R, float light, RectInt? lm)
    {
        var l0 = Load(Sea + "Sea_L0_Far"); var l1 = Load(Sea + "Sea_L1_Ruins");
        var bot = Load(Sea + "Sea_L2_BottomStrip"); var sl = Load(Sea + "Sea_L2_SideLeft"); var sr = Load(Sea + "Sea_L2_SideRight");
        float deep = Mathf.Clamp01((-R.yMax - 90f) / 50f);                 // y -90 → -140: sáng → tối
        float f = light * Mathf.Lerp(1f, 0.62f, deep);
        Color cFar = new Color(f, f, f, 1), c = new Color(light * Mathf.Lerp(1f, 0.78f, deep), light * Mathf.Lerp(1f, 0.8f, deep), light * Mathf.Lerp(1f, 0.85f, deep), 1);
        // L0: phòng sâu đẩy dải mặt nước sáng lên trên, khuất khỏi khung (phóng cao hơn phòng, giữ đáy)
        float h0 = R.height * Mathf.Lerp(1f, 1.35f, deep);
        Fill(bg, "L0_Far", l0, R, 2f * D, h0, -26, cFar, R.yMin);
        FillBottom(bg, "L1_Ruins", l1, R, D, Mathf.Clamp(R.height, 11f, 14f), -25, c);
        if (lm.HasValue) Landmark(bg, Load(Sea + "R20_Citadel"), sr, R, lm.Value, 0.72f, -24, c);
        float zn = 0.25f * D;
        BottomStrip(bg, "L2_Bottom", bot, R, zn, bot ? bot.rect.height / PPU : 0, -23, c);
        Sides(bg, sl, sr, R, zn, -23, c, true);
    }

    static void BuildPillar(Transform bg, Rect R)
    {
        // Ảnh sảnh là ảnh đặc (không trong suốt) nên chỉ cần 1 lớp sảnh + 2 cột gần; lặp ngang lật gương, lặp dọc theo từng tầng sảnh.
        var hall = Load(Pillar + "Pillar_L1_Hall");
        var sl = Load(Pillar + "Pillar_L2_SideLeft"); var sr = Load(Pillar + "Pillar_L2_SideRight");
        Color c = Color.white;
        int floors = Mathf.Max(1, Mathf.CeilToInt(R.height / 22f));
        Fill(bg, "L1_Hall", hall, R, D, R.height / floors, -11, c, R.yMin, tileV: true);   // xếp chồng từng tầng sảnh (lòng tháp nhiều tầng); vẽ trên tường gạch BackWall (-12)
        float zn = 0.25f * D, k = (D + zn) / D;
        if (sl == null || sr == null) return;
        var grp = Group(bg, "L2_Columns", R, zn);
        float s = 20f / sl.bounds.size.y, wl = sl.bounds.size.x * s, wr = sr.bounds.size.x * s;
        float xl = R.xMin + (R.center.x - (R.xMin + HalfW)) * (1 - 1 / k);
        float xr = R.xMax + (R.center.x - (R.xMax - HalfW)) * (1 - 1 / k);
        float y0 = R.yMin - 2f;
        for (float y = y0; y < R.yMax + 2f; y += 20f)
        {
            Piece(grp, "ColL", sl, R, xl + wl / 2f, y + 10f, s, false, false, -10, c);
            Piece(grp, "ColR", sr, R, xr - wr / 2f, y + 10f, s, false, false, -10, c);
        }
    }

    // ---------------- Dựng lớp ----------------
    static Transform Group(Transform bg, string name, Rect R, float z)
    {
        float k = (D + z) / D;
        var g = new GameObject($"{name}_z{z:0.#}").transform;
        g.SetParent(bg, false);
        g.position = new Vector3(R.center.x, R.center.y, z);
        g.localScale = new Vector3(k, k, 1);
        return g;
    }

    /// Một bản sprite ở vị trí (x, y) trong "không gian phòng" (tọa độ thế giới trước khi nhóm phóng to).
    static void Piece(Transform grp, string name, Sprite sp, Rect R, float x, float y, float s, bool fx, bool fy, int order, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grp, false);
        go.transform.localPosition = new Vector3(x - R.center.x, y - R.center.y, 0);
        go.transform.localScale = new Vector3(s, s, 1);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sp; r.sortingOrder = order; r.flipX = fx; r.flipY = fy; r.color = c;
        if (mat != null) r.sharedMaterial = mat;
        sprites++;
    }

    /// Phủ kín phòng: cao h, lặp ngang lật gương xen kẽ (căn giữa); tileV = lặp dọc (không lật) từ đáy.
    static void Fill(Transform bg, string name, Sprite sp, Rect R, float z, float h, int order, Color c, float? yBottom = null, bool tileV = false)
    {
        if (sp == null) return;
        var grp = Group(bg, name, R, z);
        float s = h / sp.bounds.size.y, w = sp.bounds.size.x * s;
        int n = Mathf.Max(1, Mathf.CeilToInt(R.width / w - 0.01f));
        float x0 = R.center.x - n * w / 2f;
        float yb = yBottom ?? (R.center.y - h / 2f);
        int rows = tileV ? Mathf.Max(1, Mathf.CeilToInt((R.yMax - yb) / h - 0.01f)) : 1;
        for (int row = 0; row < rows; row++)
            for (int i = 0; i < n; i++)
                Piece(grp, $"{sp.name}_{row}_{i}", sp, R, x0 + w * (i + 0.5f), yb + h * (row + 0.5f), s, i % 2 == 1, false, order, c);
    }

    /// Lớp đứng trên đáy phòng (phế tích), cao h, lặp ngang lật gương.
    static void FillBottom(Transform bg, string name, Sprite sp, Rect R, float z, float h, int order, Color c)
        => Fill(bg, name, sp, R, z, h, order, c, R.yMin);

    /// Vị trí "trong không gian phòng" để một mép của lớp gần chạm đúng mép phòng khi camera đứng sát mép đó.
    static float EdgeX(Rect R, float edge, float k) => edge + (R.center.x - (edge < R.center.x ? edge + HalfW : edge - HalfW)) * (1 - 1 / k);
    static float EdgeY(Rect R, float edge, float k) => edge + (R.center.y - (edge < R.center.y ? edge + HalfH : edge - HalfH)) * (1 - 1 / k);

    static void TopStrip(Transform bg, string name, Sprite sp, Rect R, float z, float h, int order, Color c)
    {
        if (sp == null) return;
        float k = (D + z) / D;
        var grp = Group(bg, name, R, z);
        float s = h / sp.bounds.size.y, w = sp.bounds.size.x * s;
        float yTop = EdgeY(R, R.yMax, k);
        float span = R.width + 2f * HalfW;                                   // dải trượt ngang nên phủ dư hai bên
        int n = Mathf.Max(1, Mathf.CeilToInt(span / w));
        float x0 = R.center.x - n * w / 2f;
        for (int i = 0; i < n; i++) Piece(grp, $"{sp.name}_{i}", sp, R, x0 + w * (i + 0.5f), yTop - h / 2f, s, i % 2 == 1, false, order, c);
    }

    static void BottomStrip(Transform bg, string name, Sprite sp, Rect R, float z, float h, int order, Color c)
    {
        if (sp == null) return;
        float k = (D + z) / D;
        var grp = Group(bg, name, R, z);
        float s = h / sp.bounds.size.y, w = sp.bounds.size.x * s;
        float yBot = EdgeY(R, R.yMin, k);
        float span = R.width + 2f * HalfW;
        int n = Mathf.Max(1, Mathf.CeilToInt(span / w));
        float x0 = R.center.x - n * w / 2f;
        for (int i = 0; i < n; i++) Piece(grp, $"{sp.name}_{i}", sp, R, x0 + w * (i + 0.5f), yBot + h / 2f, s, i % 2 == 1, false, order, c);
    }

    /// Hai khung gần ở hai đầu phòng. Hồ: cao phủ cả phòng (dư phần trượt dọc). Biển: đứng trên đáy.
    static void Sides(Transform bg, Sprite sl, Sprite sr, Rect R, float z, int order, Color c, bool bottomAnchored)
    {
        if (sl == null || sr == null) return;
        float k = (D + z) / D;
        var grp = Group(bg, "L2_Sides", R, z);
        float slide = Mathf.Max(0f, R.height / 2f - HalfH) * (1 - 1 / k);
        float h = bottomAnchored ? sl.rect.height / PPU : 11f;              // giữ cỡ điểm ảnh gần như các lớp khác
        float s = h / sl.bounds.size.y;
        float wl = sl.bounds.size.x * s, wr = sr.bounds.size.x * s;
        float xl = EdgeX(R, R.xMin, k) + wl / 2f, xr = EdgeX(R, R.xMax, k) - wr / 2f;
        if (bottomAnchored)
        {
            float y = EdgeY(R, R.yMin, k) + h / 2f;
            Piece(grp, "SideL", sl, R, xl, y, s, false, false, order, c);
            Piece(grp, "SideR", sr, R, xr, y, s, false, false, order, c);
            return;
        }
        // Hồ: chồng các khung đá cao 11 ô (lật dọc xen kẽ để mép khớp) phủ cả chiều cao phòng + phần trượt dọc
        float total = R.height + 2f * slide;
        int rows = Mathf.Max(1, Mathf.CeilToInt(total / h - 0.01f));
        float y0 = R.center.y - rows * h / 2f;
        for (int i = 0; i < rows; i++)
        {
            float y = y0 + h * (i + 0.5f);
            Piece(grp, "SideL_" + i, sl, R, xl, y, s, false, i % 2 == 1, order, c);
            Piece(grp, "SideR_" + i, sr, R, xr, y, s, false, i % 2 == 1, order, c);
        }
    }

    /// Cảnh đặc trưng (ảnh đặc) cao bằng phòng, căn giữa vùng mốc; mép trái được che bằng một khung đá cùng độ sâu.
    static void Landmark(Transform bg, Sprite sp, Sprite seamCover, Rect R, RectInt area, float coverFrac, int order, Color c, Sprite seamTop = null)
    {
        if (sp == null) return;
        float z = 0.6f * D;
        var grp = Group(bg, "Landmark_" + sp.name, R, z);
        float h = R.height, s = h / sp.bounds.size.y, w = sp.bounds.size.x * s;
        float cx = area.center.x;
        if (cx + w / 2f > R.xMax) cx = R.xMax - w / 2f;                     // không thò ra ngoài mép phải phòng
        Piece(grp, sp.name, sp, R, cx, R.center.y, s, false, false, order, c);
        if (seamCover != null && cx - w / 2f > R.xMin + 0.5f)
        {
            float hc = h * coverFrac, sc = hc / seamCover.bounds.size.y, wc = seamCover.bounds.size.x * sc;
            float yc = coverFrac >= 1f ? R.center.y : R.yMin + hc / 2f;
            Piece(grp, "SeamCover", seamCover, R, cx - w / 2f + 0.5f - wc / 2f, yc, sc, false, false, order, c);
            if (seamTop != null)   // nửa trên khung đá bám trần, che nốt phần trên của mép
            {
                float sct = hc / seamTop.bounds.size.y, wct = seamTop.bounds.size.x * sct;
                Piece(grp, "SeamCoverTop", seamTop, R, cx - w / 2f + 0.5f - wct / 2f, R.yMax - hc / 2f, sct, false, false, order, c);
            }
        }
    }


    /// Hồ: 4 góc khung đá (nửa trên/nửa dưới của ảnh khung) bám 4 góc phòng, không phụ thuộc chiều cao phòng.
    static void LakeCorners(Transform bg, Sprite lT, Sprite lB, Sprite rT, Sprite rB, Rect R, float z, int order, Color c)
    {
        if (lT == null || lB == null || rT == null || rB == null) return;
        float k = (D + z) / D;
        var grp = Group(bg, "L2_Corners", R, z);
        float s = 1f;                                                        // giữ đúng cỡ điểm ảnh (PPU 17,1)
        float xl = EdgeX(R, R.xMin, k), xr = EdgeX(R, R.xMax, k), yb = EdgeY(R, R.yMin, k), yt = EdgeY(R, R.yMax, k);
        Piece(grp, "CornerLB", lB, R, xl + lB.bounds.size.x / 2f, yb + lB.bounds.size.y / 2f, s, false, false, order, c);
        Piece(grp, "CornerLT", lT, R, xl + lT.bounds.size.x / 2f, yt - lT.bounds.size.y / 2f, s, false, false, order, c);
        Piece(grp, "CornerRB", rB, R, xr - rB.bounds.size.x / 2f, yb + rB.bounds.size.y / 2f, s, false, false, order, c);
        Piece(grp, "CornerRT", rT, R, xr - rT.bounds.size.x / 2f, yt - rT.bounds.size.y / 2f, s, false, false, order, c);
    }

    /// Cắt ảnh khung thành 2 sprite (nửa trên, nửa dưới) bằng chế độ import Multiple. Không sửa file ảnh.
    static (Sprite top, Sprite bot) LoadHalves(string pathNoExt)
    {
        string path = pathNoExt + ".png";
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { log.Add("Không có " + path); return (null, null); }
        ti.GetSourceTextureWidthAndHeight(out int w, out int h);
        string nm = System.IO.Path.GetFileNameWithoutExtension(path);
        var rTop = new Rect(0, h / 2, w, h - h / 2); var rBot = new Rect(0, 0, w, h / 2);
#pragma warning disable 0618
        var cur = ti.spritesheet;
        bool ok = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Multiple && Mathf.Approximately(ti.spritePixelsPerUnit, PPU)
                  && ti.filterMode == FilterMode.Point && cur.Length == 2 && cur[0].rect == rTop && cur[1].rect == rBot;
        if (!ok)
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = PPU; ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed; ti.wrapMode = TextureWrapMode.Clamp;
            ti.spritesheet = new[] {
                new SpriteMetaData { name = nm + "_top", rect = rTop, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) },
                new SpriteMetaData { name = nm + "_bot", rect = rBot, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) } };
            ti.SaveAndReimport();
            log.Add("Cắt đôi khung " + nm);
        }
#pragma warning restore 0618
        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        return (all.FirstOrDefault(x => x.name == nm + "_top"), all.FirstOrDefault(x => x.name == nm + "_bot"));
    }


    static Sprite Load(string pathNoExt)
    {
        string path = pathNoExt + ".png";
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { log.Add("Không có " + path); return null; }
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        if (set.textureType != TextureImporterType.Sprite || !Mathf.Approximately(set.spritePixelsPerUnit, PPU) || set.spriteMode != (int)SpriteImportMode.Single
            || set.filterMode != FilterMode.Point || set.spriteAlignment != (int)SpriteAlignment.Center || ti.textureCompression != TextureImporterCompression.Uncompressed)
        {
            set.textureType = TextureImporterType.Sprite; set.spriteMode = (int)SpriteImportMode.Single;
            set.spritePixelsPerUnit = PPU; set.filterMode = FilterMode.Point; set.mipmapEnabled = false;
            set.alphaIsTransparency = true; set.spriteAlignment = (int)SpriteAlignment.Center; set.spriteMeshType = SpriteMeshType.FullRect;
            set.wrapMode = TextureWrapMode.Clamp;
            ti.SetTextureSettings(set);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            log.Add("Chỉnh import " + System.IO.Path.GetFileName(path));
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
