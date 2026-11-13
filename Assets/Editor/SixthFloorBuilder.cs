using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Greybox tầng 6 — Lâu Đài Tro Tàn, 29 khung phòng:
/// A (hang dung nham), B (hồ dung nham + cầu đá), C (lâu đài bỏ hoang của pháp sư), D (hang rồng, trùm cuối).
/// Theo tài liệu "Thiet-Ke-Tang-6-Lau-Dai-Tro-Tan.md" (1 ô lưới = 18×10 tile, y lưới tăng xuống dưới).
/// Dung nham: hố sâu 2 ô, ô trên là không khí, ô dưới là dung nham (mặt dung nham CHÌM 1 ô so với mặt đất) + DeadZone. Hố 11 ô là khóa Dash (Double Jump + chạy đà chỉ qua 7–8 ô).
/// Khối màu: cổng/song sắt (cam), chờ hạ boss (đỏ), sàn nứt (tím, BreakableFloor), dung nham (cam sáng, không va chạm).
/// Chạy: Tools > Culinaria > Build Sixth Floor (F6). Chạy lại bao nhiêu lần cũng được (xóa phần đã dựng rồi dựng lại).
/// Nội dung cũ của scene KHÔNG bị xóa, chỉ bị tắt (SetActive false).
public static partial class SixthFloorBuilder
{
    const string SceneNameExpected = "SixthFloorScene";
    const string ScenePath = "Assets/Scenes/SixthFloorScene.unity";
    const string BackupPath = "Assets/Scenes/Backup/SixthFloorScene_TruocF6Builder.unity";
    const int SW = 18, SH = 10;
    const string Out = "Assets/BackGround/SixthFloor/F6_Greybox";
    const int DashGap = 11;

    static readonly string[] GeneratedRoots = { "Grid_F6", "______Room_F6_____", "______Door_F6_____", "______Interact_F6_____",
                                                "______Hidden_F6_____", "______TODO_F6_____", "______Lava_F6_____", "______Art_F6_____" };
    static readonly string[] OldRootsToDisable = { "Grid", "GruzMother", "Canvas-HealthBar-Boss", "GameObject" };
    static readonly string[] OldVcamsToDisable = { "Virtual Camera" };

    static readonly Color ZoneA = new Color(0.40f, 0.27f, 0.25f);   // hang dung nham: bazan nâu đỏ
    static readonly Color ZoneB = new Color(0.33f, 0.24f, 0.30f);   // hồ dung nham + cầu: đá tím than
    static readonly Color ZoneC = new Color(0.44f, 0.43f, 0.55f);   // lâu đài: đá xám tím
    static readonly Color ZoneD = new Color(0.58f, 0.28f, 0.16f);   // hang rồng: đá than hồng
    static readonly Color PlatTint = new Color(0.78f, 0.62f, 0.45f);
    static readonly Color GateTint = new Color(1f, 0.54f, 0.36f);
    static readonly Color GPTint = new Color(0.88f, 0.36f, 1f);
    static readonly Color BossGateTint = new Color(1f, 0.30f, 0.43f);
    static readonly Color LavaTint = new Color(1f, 0.45f, 0.10f, 0.9f);

    class RoomDef
    {
        public string id; public int gx, gy, w, h; public RectInt r;
        public GameObject go; public Object vcam;
        public int BottomRow => gy + h - 1;
        public int Floor => r.yMin + 1;                     // hàng tile không khí thấp nhất (mặt sàn)
    }
    struct Door { public string a, b, side, kind; public int cx, cy, off; }

    static Dictionary<string, RoomDef> rooms;
    static readonly List<Door> doors = new List<Door>();
    const int minX = -SW, minY = -12 * SH, W = 14 * SW, H = 13 * SH;   // chừa 1 ô đá quanh lưới 0..11 × 0..10
    static bool[,] solid, plat, lava;
    static readonly List<string> log = new List<string>();
    static readonly List<(string name, RectInt area, string kind)> blockers = new List<(string, RectInt, string)>();
    static readonly List<(string id, RectInt area)> gpFloors = new List<(string, RectInt)>();
    static readonly List<(string id, float x, int y, int coins)> chests = new List<(string, float, int, int)>();
    static int lavaPits;
    static readonly List<(string name, Vector3 pos)> markers = new List<(string, Vector3)>();
    static readonly HashSet<string> noAutoClimb = new HashSet<string>();
    static Sprite blockSprite; static Tile blockTile; static bool artOk;

    static int CellBottom(int gy) => -(gy + 1) * SH;
    static int CellLeft(int gx) => gx * SW;

    // ===================== DỮ LIỆU PHÒNG =====================
    static void R(string id, int gx, int gy, int w, int h)
        => rooms[id] = new RoomDef { id = "F6_" + id, gx = gx, gy = gy, w = w, h = h,
               r = new RectInt(CellLeft(gx), CellBottom(gy + h - 1), w * SW, h * SH) };
    static void D(string a, string b, int cx, int cy, string side, int off = 8, string kind = "open")
        => doors.Add(new Door { a = a, b = b, cx = cx, cy = cy, side = side, off = off, kind = kind });

    static void DefineRooms()
    {
        rooms = new Dictionary<string, RoomDef>(); doors.Clear();
        // Khu A — hang dung nham
        R("R01", 0, 0, 1, 2); R("R02", 1, 1, 3, 1); R("R03", 4, 0, 2, 2); R("R04", 6, 1, 3, 1); R("R05", 9, 0, 2, 2);
        R("R06", 11, 0, 1, 4); R("R07", 8, 2, 3, 1); R("R08", 8, 3, 3, 1); R("R09", 6, 2, 2, 2);
        // Khu B — hồ dung nham + cầu đá
        R("R10", 3, 3, 3, 1); R("R11", 4, 2, 2, 1); R("R12", 1, 3, 2, 1);
        // Khu C — lâu đài bỏ hoang
        R("R13", 0, 2, 1, 2); R("R14", 1, 2, 2, 1); R("R15", 0, 4, 4, 2); R("R16", 4, 4, 2, 1); R("R17", 4, 5, 2, 1);
        R("R18", 6, 4, 2, 2); R("R19", 8, 4, 1, 3); R("R20", 9, 4, 2, 2); R("R21", 11, 4, 1, 2); R("R22", 9, 6, 3, 1);
        R("R23", 0, 6, 3, 1); R("R24", 3, 6, 3, 1); R("R25", 6, 6, 2, 2);
        // Khu D — hang rồng
        R("R26", 6, 8, 1, 2); R("R27", 4, 9, 2, 1); R("R28", 0, 8, 4, 2); R("R29", 0, 10, 2, 1);

        D("R01", "R02", 0, 1, "right"); D("R02", "R03", 3, 1, "right"); D("R03", "R04", 5, 1, "right");
        D("R04", "R05", 8, 1, "right"); D("R05", "R06", 10, 1, "right");
        D("R06", "R07", 11, 2, "left"); D("R06", "R08", 11, 3, "left");       // đường cao / đường thấp
        D("R07", "R09", 8, 2, "left"); D("R08", "R09", 8, 3, "left"); D("R09", "R10", 6, 3, "left");
        D("R10", "R11", 4, 3, "top", 8, "secret");                            // lỗ trần bí mật (Double Jump)
        D("R10", "R12", 3, 3, "left"); D("R12", "R13", 1, 3, "left");
        D("R13", "R14", 0, 2, "right"); D("R13", "R15", 0, 3, "bottom");
        D("R15", "R16", 3, 4, "right"); D("R15", "R17", 3, 5, "right");
        D("R16", "R18", 5, 4, "right"); D("R17", "R18", 5, 5, "right"); D("R18", "R19", 7, 5, "right");
        D("R19", "R20", 8, 5, "right");                                       // sau cửa là hố Dash trong R20
        D("R20", "R21", 10, 5, "right"); D("R20", "R22", 9, 5, "bottom", 24, "gp");
        D("R25", "R19", 7, 6, "right", 8, "gate");                            // L1 song sắt, mở từ phía phòng ngai
        D("R15", "R23", 1, 5, "bottom"); D("R23", "R24", 2, 6, "right");
        D("R24", "R25", 5, 6, "right");                                       // trước cửa là hố Dash bắt buộc trong R24
        D("R25", "R26", 6, 7, "bottom"); D("R26", "R27", 6, 9, "left"); D("R27", "R28", 4, 9, "left");
        D("R28", "R29", 0, 9, "bottom", 8, "boss");                           // mở khi hạ rồng
    }

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/Build Sixth Floor (F6)")]
    public static void Build()
    {
        Scene scene = SceneManager.GetSceneByName(SceneNameExpected);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("[F6] Hãy mở SixthFloorScene trước (mở kèm scene khác cũng được)."); return; }
        Scene prevActive = SceneManager.GetActiveScene();
        if (prevActive != scene) SceneManager.SetActiveScene(scene);       // object mới tạo phải rơi vào SixthFloorScene
        if (SceneManager.GetActiveScene() != scene) { Debug.LogError("[F6] Không đặt được SixthFloorScene làm scene active → dừng, không dựng gì."); return; }
        log.Clear(); blockers.Clear(); gpFloors.Clear(); chests.Clear(); lavaPits = 0; markers.Clear(); tunnelCells.Clear();

        // Sao lưu bản gốc một lần, trước lần dựng đầu tiên
        if (!System.IO.File.Exists(BackupPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Backup")) AssetDatabase.CreateFolder("Assets/Scenes", "Backup");
            if (scene.isDirty) log.Add("CẢNH BÁO: scene đang có thay đổi chưa lưu; bản sao lưu là bản trên đĩa.");
            AssetDatabase.CopyAsset(ScenePath, BackupPath);
            log.Add("Đã sao lưu scene gốc: " + BackupPath);
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (GeneratedRoots.Contains(root.name)) Object.DestroyImmediate(root);
            else if (OldRootsToDisable.Contains(root.name) && root.activeSelf) { root.SetActive(false); log.Add("Tắt (không xóa) nội dung cũ: " + root.name); }
        }
        foreach (var root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (OldVcamsToDisable.Contains(t.name) && t.GetComponent("CinemachineVirtualCamera") != null && t.gameObject.activeSelf)
                { t.gameObject.SetActive(false); log.Add("Tắt camera cũ: " + t.name); }

        DefineRooms();
        MakeBlockTile();
        artOk = PrepareArt();                 // phần hình ảnh (SixthFloorBuilder.Art.cs); thiếu tài nguyên thì giữ greybox màu
        BuildGeometry();
        WriteTilemaps();
        BuildRooms();
        BuildDoorsAndInteract();
        BuildHiddenBlockers();
        BuildTodoMarkers();
        if (artOk) DecorateF6(scene);
        F6RoomMerge.ApplyAll(scene);           // gộp phòng + vệt sáng hầm + chia khung camera (F6RoomMerge.cs, F6RoomFrames.cs)

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"Xong: {rooms.Count} khung phòng, {doors.Count} cửa, {chests.Count} rương, {gpFloors.Count} sàn nứt, {lavaPits} hố dung nham, {blockers.Count} khối chặn.");
        if (prevActive.IsValid() && prevActive.isLoaded && prevActive != scene) SceneManager.SetActiveScene(prevActive);   // trả lại scene người dùng đang làm
        Debug.Log("[F6] " + string.Join(" | ", log));
    }

    // ===================== HÌNH KHỐI =====================
    static bool In(int x, int y) => x >= minX && y >= minY && x < minX + W && y < minY + H;
    static void Set(bool[,] a, int x, int y, bool v) { if (In(x, y)) a[x - minX, y - minY] = v; }
    static bool Get(bool[,] a, int x, int y) => In(x, y) && a[x - minX, y - minY];
    static void Fill(int x, int y, int w, int h) { for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) Set(solid, x + i, y + j, true); }
    static void Carve(int x, int y, int w, int h)
    {
        for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) { Set(solid, x + i, y + j, false); Set(plat, x + i, y + j, false); }
    }
    static void Plat(int x, int y, int w) { for (int i = 0; i < w; i++) if (!Get(solid, x + i, y)) Set(plat, x + i, y, true); }

    /// Hố dung nham, tính từ mép trái phòng. Luật: mặt dung nham chìm 1 ô so với mặt đất, không bao giờ ngang mặt đất.
    /// Hố sâu 2 ô: hàng sàn (yMin) thành không khí, hàng dưới (yMin-1) là dung nham, hàng dưới nữa (yMin-2) đắp đá làm đáy
    /// (đáy này thò xuống 1 ô vào trần phòng bên dưới, nếu có). Dung nham vẽ trên tilemap F6_DeadZone_Lava (tag DeadZone, trigger).
    static void Lava(string id, int off, int w, string note = "")
    {
        var r = rooms[id];
        int x = r.r.xMin + off;
        Carve(x, r.r.yMin, w, 1);
        Carve(x, r.r.yMin - 1, w, 1);
        for (int i = 0; i < w; i++) Set(lava, x + i, r.r.yMin - 1, true);
        Fill(x, r.r.yMin - 2, w, 1);
        lavaPits++;
    }

    /// Bậc một chiều rộng 3, so le, mỗi bước ≤ 3 ô (tầm nhảy đơn). Giống tầng 4, 5.
    static void ClimbStack(RoomDef room, int x, int topSurface, bool includeTop, int altDir = 0)
    {
        int ix0 = room.r.xMin + 1, ix1 = room.r.xMax - 2;
        int total = topSurface - room.Floor;
        if (total <= 3) return;
        int n = Mathf.CeilToInt(total / 3f);
        var ys = new List<int>();
        for (int i = 1; i <= n; i++)
        {
            if (i == n && !includeTop) break;
            ys.Add(room.Floor + Mathf.RoundToInt(total * i / (float)n) - 1);
        }
        for (int i = 0; i < ys.Count; i++)
        {
            int px = x, fromTop = ys.Count - 1 - i;
            if (fromTop % 2 == 1) px = altDir != 0 ? x + 5 * altDir : ((x + 8 <= ix1) ? x + 5 : x - 5);
            px = Mathf.Clamp(px, ix0, ix1 - 2);
            Plat(px, ys[i], 3);
        }
    }

    /// Vách ngăn tại các khoảng cách (tile) tính từ mép trái phòng: lẻ thò từ trần (chừa khe 3 ô), chẵn dựng từ sàn (cao 3).
    static void Walls(string id, params int[] offs)
    {
        var r = rooms[id];
        var holes = doors.Where(d => (d.side == "bottom" || d.side == "top") && (d.a == id || d.b == id))
                         .Select(d => CellLeft(d.cx) + d.off)
                         .Where(hx => hx >= r.r.xMin && hx < r.r.xMax).ToList();
        for (int i = 0; i < offs.Length; i++)
        {
            int off = offs[i];
            if (off < 9 || off > r.r.width - 10 || holes.Any(hx => r.r.xMin + off > hx - 4 && r.r.xMin + off < hx + 6))
            { log.Add($"Bỏ vách {id}+{off} (sát cửa/lỗ)"); continue; }
            int x = r.r.xMin + off;
            if (i % 2 == 0) Fill(x, r.Floor + 3, 2, r.r.yMax - 1 - (r.Floor + 3));
            else Fill(x, r.Floor, 2, 3);
        }
    }

    /// Khe Wall Jump 3 ô + rương trên đỉnh. twinOff < 0: khe sát vách trái (giống tháp gác R11 tầng 5).
    /// twinOff >= 0: hai cột 3 ô đứng giữa phòng, khe ở giữa, rương trên đỉnh cột phải (dùng khi vách trái là đường hầm).
    static void WallJumpPillar(string id, string chestId, int coins, int twinOff = -1)
    {
        var r = rooms[id];
        if (twinOff < 0)
        {
            int px = r.r.xMin + 4;
            Fill(px, r.Floor + 3, 2, r.r.height - 9);
            Fill(px, r.r.yMax - 6, 7, 1);
            chests.Add((chestId, px + 4.5f, r.r.yMax - 5, coins));
            return;
        }
        int x0 = r.r.xMin + twinOff, hgt = r.r.height - 10;   // thấp hơn 1 ô: trần phía trên có đáy hố dung nham của phòng trên
        Fill(x0, r.Floor + 3, 3, hgt); Fill(x0 + 6, r.Floor + 3, 3, hgt);
        chests.Add((chestId, x0 + 7.5f, r.Floor + 3 + hgt, coins));
    }

    static void BuildGeometry()
    {
        solid = new bool[W, H]; plat = new bool[W, H]; lava = new bool[W, H];
        Fill(minX, minY, W, H);
        foreach (var r in rooms.Values) Carve(r.r.xMin + 1, r.r.yMin + 1, r.r.width - 2, r.r.height - 2);

        // ---- Cửa giữa các phòng (giống tầng 4, 5) ----
        foreach (var d in doors)
        {
            RoomDef A = rooms[d.a], B = rooms[d.b];
            if (d.side == "right" || d.side == "left")
            {
                int bx = d.side == "right" ? (d.cx + 1) * SW : d.cx * SW;
                int yb = CellBottom(d.cy) + 1;
                Carve(bx - 1, yb, 2, 3);
                if (d.kind == "gate") blockers.Add(($"Gate_L1_SongSat_{A.id}_{B.id} (mở từ {A.id})", new RectInt(bx - 1, yb, 2, 3), "gate"));
                foreach (var (room, inward) in new[] { (A, d.side == "right" ? -1 : 1), (B, d.side == "right" ? 1 : -1) })
                {
                    if (d.cy == room.BottomRow) continue;
                    int lx = inward < 0 ? bx - 6 : bx + 1;
                    Fill(lx, yb - 1, 5, 1);
                    if (!noAutoClimb.Contains(room.id.Substring(3))) ClimbStack(room, inward < 0 ? lx - 4 : lx + 6, yb, false, inward < 0 ? -1 : 1);
                }
            }
            else
            {
                int by = d.side == "bottom" ? CellBottom(d.cy) : CellBottom(d.cy) + SH;
                int hx = CellLeft(d.cx) + d.off;
                Carve(hx, by - 1, 3, 2);
                RoomDef lower = d.side == "bottom" ? B : A;
                RoomDef upper = d.side == "bottom" ? A : B;
                var hole = new RectInt(hx, by - 1, 3, 2);
                if (d.kind == "secret") Plat(hx - 1, lower.Floor + 4, 5);            // chỉ một bậc: lên lỗ cần Double Jump (5 ô)
                else ClimbStack(lower, hx, lower.r.yMax - 3, true);
                switch (d.kind)
                {
                    case "gp": gpFloors.Add(($"F6_{upper.id.Substring(3)}_GP", hole)); break;
                    case "boss": blockers.Add(($"Gate_Boss_{A.id}_{B.id}", hole, "boss")); break;
                }
            }
        }

        // ---- R01 giếng vào: gờ trên cùng cho cửa lên tầng 5 + bậc leo ----
        var r01 = rooms["R01"];
        Fill(r01.r.xMin + 1, r01.r.yMax - 9, 8, 1);
        ClimbStack(r01, r01.r.xMin + 10, r01.r.yMax - 8, false);

        // ---- R02 hành lang bazan: hai vũng dung nham nhỏ (dạy "dung nham là chết") ----
        Lava("R02", 16, 3); Lava("R02", 34, 3);

        // ---- R03 thác dung nham: hồ 7 ô (Double Jump + chạy đà) hoặc đi đường bậc cao ----
        var r03 = rooms["R03"];
        Lava("R03", 13, 7);
        Plat(r03.r.xMin + 6, r03.Floor + 3, 4); Plat(r03.r.xMin + 14, r03.Floor + 6, 6); Plat(r03.r.xMin + 25, r03.Floor + 3, 4);

        // ---- R04 sông dung nham: 5 khúc sông 5 ô, giữa là mỏm đá 3 ô ----
        foreach (int o in new[] { 8, 16, 24, 32, 40 }) Lava("R04", o, 5);

        // ---- R07 hang hắc diện thạch (đường cao): vách đá thò từ trần / dựng từ sàn ----
        Walls("R07", 14, 26, 38);

        // ---- R08 ổ quỷ lửa (đường thấp): hai hố dung nham 4 ô ----
        Lava("R08", 14, 4); Lava("R08", 34, 4);

        // ---- R09 lò đá: hai cột đá giữa phòng, khe Wall Jump ở giữa → Rương 1 ----
        WallJumpPillar("R09", "F6_Chest01", 1000, 10);

        // ---- R10 bờ hồ dung nham: một lạch dung nham 5 ô; lỗ trần bí mật lên R11 ----
        Lava("R10", 36, 5);
        chests.Add(("F6_Chest02", rooms["R11"].r.xMax - 6.5f, rooms["R11"].Floor, 1200));

        // ---- R12 cầu đá: hai nhịp gãy 4 ô ----
        Lava("R12", 9, 4, "_CauGay"); Lava("R12", 23, 4, "_CauGay");

        // ---- R14 tường thành: quãng sập 11 ô (Dash) → Rương 4 ----
        Lava("R14", 12, DashGap, "_Dash_TuyChon");
        chests.Add(("F6_Chest04", rooms["R14"].r.xMax - 5.5f, rooms["R14"].Floor, 1500));

        // ---- R15 đại sảnh: ban công dài nối ra cửa trên bên phải, hai đèn chùm ----
        var r15 = rooms["R15"];
        Plat(r15.r.xMin + 34, r15.Floor + 9, 32);
        Plat(r15.r.xMin + 14, r15.Floor + 4, 5); Plat(r15.r.xMin + 22, r15.Floor + 7, 5); Plat(r15.r.xMin + 40, r15.Floor + 5, 5);

        // ---- R18 thư viện: kệ sách là bậc một chiều ----
        var r18 = rooms["R18"];
        Plat(r18.r.xMin + 24, r18.Floor + 3, 6); Plat(r18.r.xMin + 18, r18.Floor + 6, 8); Plat(r18.r.xMin + 26, r18.Floor + 9, 6);

        // ---- R19 tháp pháp sư: phòng thí nghiệm trên đỉnh (nhặt Dash) ----
        var r19 = rooms["R19"];
        int ledge = CellBottom(5) + 1;                              // mặt gờ cửa hàng 5
        Plat(r19.r.xMin + 7, ledge + 2, 4); Plat(r19.r.xMin + 2, ledge + 5, 3);
        Fill(r19.r.xMin + 6, ledge + 8, 11, 1);                     // sàn phòng thí nghiệm
        markers.Add(("AbilityPickup_Dash_TODO (thêm Ability.Dash rồi gắn AbilityPickup)", new Vector3(r19.r.xMin + 12.5f, ledge + 9.5f, 0)));

        // ---- R20 nhà nguyện đổ: hố 11 ô ngay sau cửa (Dash), sàn nứt GP xuống hầm mộ ----
        Lava("R20", 9, DashGap, "_Dash_TuyChon");

        // ---- R21 tháp canh: khe Wall Jump → Rương 5 ----
        WallJumpPillar("R21", "F6_Chest05", 1200);

        // ---- R22 hầm mộ pháp sư: Rương 6 ở cuối ----
        Walls("R22", 12, 36);
        chests.Add(("F6_Chest06", rooms["R22"].r.xMax - 5.5f, rooms["R22"].Floor, 1500));

        // ---- R23 ngục tối: vách xà lim, Rương 3 ở đầu trái ----
        Walls("R23", 12, 40);
        chests.Add(("F6_Chest03", rooms["R23"].r.xMin + 4.5f, rooms["R23"].Floor, 800));

        // ---- R24 hành lang sụp: hố 11 ô — KHÓA DASH BẮT BUỘC ----
        Lava("R24", 21, DashGap, "_Dash_BatBuoc");

        // ---- R25 phòng ngai: bệ ngai ----
        var r25 = rooms["R25"];
        Fill(r25.r.xMin + 20, r25.Floor, 10, 1);

        // ---- R28 hang rồng: ba bậc đá cho trận trùm cuối ----
        var r28 = rooms["R28"];
        Plat(r28.r.xMin + 14, r28.Floor + 4, 8); Plat(r28.r.xMin + 32, r28.Floor + 7, 8); Plat(r28.r.xMin + 50, r28.Floor + 4, 8);

        CarveTunnels();
    }

    // ===================== HẦM Ở RANH GIỚI PHÒNG (kiểu Hollow Knight) =====================
    static readonly HashSet<Vector2Int> tunnelCells = new HashSet<Vector2Int>();

    static void CarveTunnels()
    {
        int hl = F6RoomMerge.HalfLen, dh = F6RoomMerge.DoorH;
        foreach (var (bx, ys) in F6RoomMerge.Tunnels)
        {
            int n = 0;
            for (int x = bx - hl; x < bx + hl; x++)
            {
                if (!Get(solid, x, ys - 1)) log.Add($"CẢNH BÁO hầm x={bx}: cột {x} không có sàn ở y={ys - 1}");
                var col = new List<int>();
                int y = ys + dh;
                while (!Get(solid, x, y) && col.Count < 45) { col.Add(y); y++; }
                if (col.Count >= 45) { log.Add($"CẢNH BÁO hầm x={bx}: cột {x} không có trần, bỏ qua"); continue; }
                foreach (int yy in col)
                {
                    Set(solid, x, yy, true); Set(plat, x, yy, false);
                    tunnelCells.Add(new Vector2Int(x, yy)); n++;
                }
            }
            log.Add($"Hầm x={bx} y={ys}: đắp {n} ô");
        }
    }

    // ===================== TILE =====================
    static void MakeBlockTile()
    {
        if (!AssetDatabase.IsValidFolder("Assets/BackGround")) AssetDatabase.CreateFolder("Assets", "BackGround");
        if (!AssetDatabase.IsValidFolder("Assets/BackGround/SixthFloor")) AssetDatabase.CreateFolder("Assets/BackGround", "SixthFloor");
        if (!AssetDatabase.IsValidFolder(Out)) AssetDatabase.CreateFolder("Assets/BackGround/SixthFloor", "F6_Greybox");
        string png = Out + "/F6_Block.png";
        if (!System.IO.File.Exists(png))
        {
            var tex = new Texture2D(16, 16);
            for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++)
                tex.SetPixel(x, y, (x == 0 || y == 0 || x == 15 || y == 15) ? new Color(0.78f, 0.78f, 0.78f) : Color.white);
            System.IO.File.WriteAllBytes(png, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(png);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(png);
        if (imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != 16 || imp.filterMode != FilterMode.Point)
        {
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 16; imp.filterMode = FilterMode.Point; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        string tilePath = Out + "/F6_Block.asset";
        blockTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (blockTile == null)
        {
            blockTile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(blockTile, tilePath);
        }
        blockTile.sprite = blockSprite; blockTile.flags = TileFlags.None; blockTile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(blockTile);
    }

    static Tilemap NewTilemap(Transform grid, string name, int order, int layer = 0)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid, false);
        go.layer = layer;
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    /// Khu của một ô tile: A hang dung nham, B hồ dung nham + cầu, C lâu đài, D hang rồng.
    static char ZoneAt(int x, int y)
    {
        if (y < -80) return 'D';
        if (y < -40) return 'C';
        if (x < 18 || (x < 54 && y >= -30)) return y >= -20 ? 'A' : 'C';          // tháp cổng + tường thành
        if (x < 108 && y < -20) return 'B';                                        // hồ dung nham + cầu + hốc bí mật
        return 'A';
    }
    static Color ZoneColor(int x, int y) { char z = ZoneAt(x, y); return z == 'A' ? ZoneA : z == 'B' ? ZoneB : z == 'C' ? ZoneC : ZoneD; }

    static void Paint(Tilemap tm, bool[,] a, System.Func<int, int, Color> color, System.Func<int, int, bool> extra = null)
    {
        var ps = new List<Vector3Int>();
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
        {
            int x = i + minX, y = j + minY;
            if (a[i, j] && (extra == null || extra(x, y))) ps.Add(new Vector3Int(x, y, 0));
        }
        tm.SetTiles(ps.ToArray(), Enumerable.Repeat<TileBase>(blockTile, ps.Count).ToArray());
        foreach (var p in ps) tm.SetColor(p, color(p.x, p.y));
    }

    static void WriteTilemaps()
    {
        var gridT = new GameObject("Grid_F6", typeof(Grid)).transform;
        int groundLayer = LayerMask.NameToLayer("Ground");

        if (artOk) PaintBackWallArt(NewTilemap(gridT, "F6_BackWall (tường nền lâu đài, không va chạm)", -60));

        var ground = NewTilemap(gridT, "F6_Ground", 0, groundLayer);
        if (artOk) PaintGroundArt(ground); else Paint(ground, solid, (x, y) => ZoneColor(x, y));
        var tc = ground.gameObject.AddComponent<TilemapCollider2D>();
        var rb = ground.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Static;
        ground.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        tc.usedByComposite = true;
#pragma warning restore 0618

        var platTm = NewTilemap(gridT, "F6_Platform_OneWay (bậc, ban công, kệ sách)", 0, groundLayer);
        if (artOk) PaintPlatformArt(platTm); else Paint(platTm, plat, (x, y) => PlatTint, (x, y) => !Get(solid, x, y));
        var ptc = platTm.gameObject.AddComponent<TilemapCollider2D>();
        var prb = platTm.gameObject.AddComponent<Rigidbody2D>(); prb.bodyType = RigidbodyType2D.Static;
        var pcc = platTm.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        ptc.usedByComposite = true;
#pragma warning restore 0618
        pcc.usedByEffector = true;
        var eff = platTm.gameObject.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true; eff.surfaceArc = 160f; eff.useSideFriction = false;

        // Dung nham: tilemap riêng, tag DeadZone, collider trigger (giống F3_DeadZone_Lava của tầng 3)
        var lavaTm = NewTilemap(gridT, "F6_DeadZone_Lava", 1);
        lavaTm.gameObject.tag = "DeadZone";
        if (artOk) PaintLavaArt(lavaTm); else Paint(lavaTm, lava, (x, y) => LavaTint);
        lavaTm.gameObject.AddComponent<TilemapCollider2D>().isTrigger = true;
    }

    // ===================== ROOM (Module 5) =====================
    static GameObject Group(string name) => new GameObject(name);

    static void BuildRooms()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Room.prefab");
        if (prefab == null) { log.Add("THIẾU prefab Assets/Prefab/Room.prefab"); return; }
        var parent = Group("______Room_F6_____").transform;
        foreach (var r in rooms.Values.OrderBy(r => r.id))
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = r.id;
            go.layer = LayerMask.NameToLayer("Room");
            go.transform.position = new Vector3(r.r.xMin, r.r.yMin, 0);
            var poly = go.GetComponent<PolygonCollider2D>();
            float w = r.r.width, h = r.r.height;
            poly.pathCount = 1;
            poly.SetPath(0, new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
            poly.offset = Vector2.zero;

            var so = new SerializedObject(go.GetComponent<global::Room>());
            so.FindProperty("roomId").stringValue = r.id;
            so.ApplyModifiedPropertiesWithoutUndo();
            r.go = go; r.vcam = so.FindProperty("vcam").objectReferenceValue;

            if (r.vcam is Component vc)
            {
                foreach (var c in vc.GetComponents<Component>())
                {
                    if (c == null || c.GetType().Name != "CinemachineConfiner2D") continue;
                    var cso = new SerializedObject(c);
                    var p = cso.FindProperty("m_BoundingShape2D");
                    if (p != null) { p.objectReferenceValue = poly; cso.ApplyModifiedPropertiesWithoutUndo(); }
                    var m = c.GetType().GetMethod("InvalidateCache");
                    if (m != null) m.Invoke(c, null);
                }
                vc.gameObject.name = "VCam_" + r.id;
                vc.transform.position = new Vector3(r.r.xMin + w / 2, r.r.yMin + h / 2, -10);
            }
        }
    }

    // ===================== CỬA, RƯƠNG, ĐIỂM LƯU =====================
    static GameObject Inst(string path, Transform parent, Vector3 pos, string name = null)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf == null) { log.Add("THIẾU prefab: " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        if (name != null) go.name = name;
        return go;
    }

    static GameObject MakeDoor(Transform parent, string doorId, Vector3 pos, string targetScenePath, string targetDoorId, int enterDir)
    {
        var go = Inst("Assets/Prefab/SceneDoor.prefab", parent, pos, doorId);
        if (go == null) return null;
        go.transform.localScale = new Vector3(0.5f, 0.5f, 1);
        var so = new SerializedObject(go.GetComponent<SceneDoor>());
        so.FindProperty("doorId").stringValue = doorId;
        so.FindProperty("targetDoorId").stringValue = targetDoorId;
        so.FindProperty("requireInteract").boolValue = true;
        so.FindProperty("enterDirection").enumValueIndex = enterDir;
        var ts = so.FindProperty("targetScene");
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetScenePath);
        var pa = ts.FindPropertyRelative("sceneAsset"); if (pa != null) pa.objectReferenceValue = asset;
        var pn = ts.FindPropertyRelative("sceneName"); if (pn != null) pn.stringValue = asset != null ? asset.name : "";
        Transform spawn = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
        if (spawn != null)
        {
            spawn.name = "SpawnPoint";
            spawn.position = pos + new Vector3(enterDir == 1 ? 2.5f : -2.5f, 0, 0);
            so.FindProperty("spawnPoint").objectReferenceValue = spawn;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    static void MakeChest(Transform parent, string id, float x, float yStand, int coins)
    {
        var go = Inst("Assets/Prefab/Chest.prefab", parent, new Vector3(x, yStand + 0.5f, 0), id);
        if (go == null) return;
        var so = new SerializedObject(go.GetComponent<Chest>());
        so.FindProperty("chestId").stringValue = id;
        so.FindProperty("coinAmount").intValue = coins;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MakeCheckpoint(Transform parent, string id, float x, RoomDef room, bool campFire = true)
    {
        float yStand = room.Floor;
        var go = new GameObject(id);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(x, yStand + 1.5f, 0);
        var bc = go.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(2, 3);
        var so = new SerializedObject(go.AddComponent<CheckPoint>());
        so.FindProperty("checkpointId").stringValue = id;
        var cam = so.FindProperty("areaCamera"); if (cam != null) cam.objectReferenceValue = room.vcam;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (campFire) Inst("Assets/Prefab/CampFire.prefab", go.transform, new Vector3(x, yStand + 0.5f, 0), "CampFire (bếp nấu)");
    }

    static void BuildDoorsAndInteract()
    {
        var doorsT = Group("______Door_F6_____").transform;
        var r01 = rooms["R01"].r;
        float ledgeStand = r01.yMax - 8;

        // Cửa lên tầng 5: đọc FifthFloorScene xem cửa nào trỏ về tầng 6 (F5_Exit → F6_Entry)
        var up = FindPartnerDoor("Assets/Scenes/FifthFloorScene.unity", SceneNameExpected);
        string myEntry = up.myId ?? "F6_Entry", f5Door = up.theirId ?? "F5_Exit";
        log.Add(up.theirId == null ? "CHƯA KHỚP cửa tầng 5: tạm đặt F6_Entry → F5_Exit." : $"Cửa tầng 5: {myEntry} ↔ {f5Door} (đọc từ FifthFloorScene)");
        MakeDoor(doorsT, myEntry, new Vector3(r01.xMin + 2.5f, ledgeStand + 1.5f, 0), "Assets/Scenes/FifthFloorScene.unity", f5Door, 1);

        // Điểm hồi sinh mặc định của scene: dời SixthFloorSpawnPoint có sẵn về R01 (không tạo cái thứ hai)
        var scene = SceneManager.GetActiveScene();
        var sp = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "SixthFloorSpawnPoint");
        if (sp == null) sp = GameObject.FindGameObjectsWithTag("SpawnPoint").FirstOrDefault(g => g.scene == scene);
        if (sp != null) { sp.transform.position = new Vector3(r01.xMin + 6.5f, ledgeStand + 1f, 0); log.Add($"Điểm hồi sinh '{sp.name}' dời về R01."); }
        else log.Add("KHÔNG thấy SixthFloorSpawnPoint: chưa đặt điểm hồi sinh mặc định.");

        var it = Group("______Interact_F6_____").transform;
        MakeCheckpoint(it, "F6_CP01_TraiThamHiem", rooms["R05"].r.xMin + 18.5f, rooms["R05"]);
        MakeCheckpoint(it, "F6_CP02_BepLauDai", rooms["R17"].r.xMin + 14.5f, rooms["R17"]);
        MakeCheckpoint(it, "F6_CP03_SuoiMana_ThuVien", rooms["R18"].r.xMin + 9.5f, rooms["R18"], false);
        MakeCheckpoint(it, "F6_CP04_TienPhongRong", rooms["R27"].r.xMin + 20.5f, rooms["R27"]);
        foreach (var c in chests) MakeChest(it, c.id, c.x, c.y, c.coins);

        var boss = new GameObject("BossSpawn (Rồng - trùm cuối, chưa thiết kế)");
        boss.transform.SetParent(it); boss.transform.position = new Vector3(rooms["R28"].r.center.x, rooms["R28"].Floor + 6, 0);

        var r05 = rooms["R05"];
        var safe = new GameObject("F6_SafeZone_TODO (Trại thám hiểm R05)");
        safe.transform.SetParent(it);
        safe.transform.position = new Vector3(r05.r.center.x, r05.r.center.y, 0);
        var sb = safe.AddComponent<BoxCollider2D>(); sb.isTrigger = true;
        sb.size = new Vector2(r05.r.width - 2, r05.r.height - 2);
    }

    /// Mở scene (additive nếu chưa mở), tìm SceneDoor trỏ về mySceneName. Trả về (id cửa đó, id nó mong cửa của mình có).
    static (string theirId, string myId) FindPartnerDoor(string scenePath, string mySceneName)
    {
        if (!System.IO.File.Exists(scenePath)) return (null, null);
        Scene s = SceneManager.GetSceneByPath(scenePath);
        bool opened = false;
        if (!s.isLoaded) { s = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive); opened = true; }
        (string, string) result = (null, null);
        foreach (var root in s.GetRootGameObjects())
            foreach (var door in root.GetComponentsInChildren<SceneDoor>(true))
            {
                var so = new SerializedObject(door);
                var ts = so.FindProperty("targetScene");
                var pn = ts != null ? ts.FindPropertyRelative("sceneName") : null;
                if (pn != null && pn.stringValue == mySceneName)
                    result = (so.FindProperty("doorId").stringValue, so.FindProperty("targetDoorId").stringValue);
            }
        if (opened) EditorSceneManager.CloseScene(s, true);
        return result;
    }

    static GameObject Block(Transform parent, string name, RectInt a, Color tint, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        if (collider) go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = new Vector3(a.x + a.width / 2f, a.y + a.height / 2f, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = tint; sr.sortingOrder = 1;
        Vector2 ss = blockSprite != null ? (Vector2)blockSprite.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(a.width / ss.x, a.height / ss.y, 1);
        if (collider) go.AddComponent<BoxCollider2D>().size = ss;
        return go;
    }

    static void BuildHiddenBlockers()
    {
        var hid = Group("______Hidden_F6_____").transform;
        foreach (var b in blockers)
        {
            string note = b.kind == "boss" ? " (mở khi hạ rồng - chưa có script)" : " (song sắt - chưa có script mở)";
            Block(hid, b.name + note, b.area, b.kind == "boss" ? BossGateTint : GateTint);
        }
        foreach (var (id, rect) in gpFloors)
        {
            var gp = Block(hid, id + "_BreakableFloor", rect, GPTint);
            var so = new SerializedObject(gp.AddComponent<BreakableFloor>());
            so.FindProperty("floorId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void BuildTodoMarkers()
    {
        var todo = Group("______TODO_F6_____").transform;
        void M(string name, string room, float dx, float dy) => markers.Add((name, new Vector3(rooms[room].r.xMin + dx, rooms[room].Floor + dy, 0)));
        M("Quai_Doi", "R02", 26, 5); M("Quai_QuyLua (FlameDevil)", "R03", 28, 1); M("Quai_Doi x2 (trên sông dung nham)", "R04", 26, 5);
        M("NPC_NhaThamHiem_TODO (bán đồ ăn, kể chuyện lâu đài)", "R05", 10, 1);
        M("Quai_Nhen + Doi", "R07", 30, 1); M("Quai_QuyLua x2 + Devil", "R08", 26, 1); M("Quai_Devil", "R09", 20, 1);
        M("Landmark_LauDaiGiuaHo (ảnh nền)", "R10", 12, 5); M("Quai_Doi (trên cầu)", "R12", 18, 5);
        M("Quai_BoXuong (lính gác cổng)", "R13", 9, 1);
        M("Quai_BoXuong x2 + PhapSu_TODO", "R15", 44, 1); M("Quai_PhapSu_TODO (trên ban công)", "R15", 50, 11);
        M("Quai_Undead", "R16", 18, 1); M("KhoThucAn_Bep (nhặt được)", "R17", 26, 1);
        M("SuoiMana_TODO (hồi mana, Module 12)", "R18", 9, 1); M("Quai_PhapSu_TODO x2 (trên kệ sách)", "R18", 24, 8);
        M("Quai_PhapSu_TODO (gác phòng thí nghiệm)", "R19", 9, 12);
        M("Quai_Undead + PhapSu_TODO", "R20", 28, 1); M("Quai_Doi", "R21", 12, 10);
        M("Quai_BoXuong x2 (hầm mộ)", "R22", 24, 1); M("Quai_Undead x2 + Nhen (xà lim)", "R23", 24, 1);
        M("Quai_QuyLua (dung nham tràn vào)", "R24", 40, 1);
        M("TranDanh_PhapSu x3 + BoXuong (phòng ngai)", "R25", 18, 1); M("Lever_L1_SongSat (mở sang đáy tháp R19)", "R25", 31, 11);
        M("Quai_Devil (giếng tro)", "R26", 9, 8);
        M("R28: hang rồng - phòng trùm cuối trống", "R28", 36, 2); M("Ending_TODO (kho báu rồng, kết thúc game)", "R29", 26, 1);
        foreach (var (name, pos) in markers) { var go = new GameObject(name); go.transform.SetParent(todo); go.transform.position = pos; }
    }
}
