using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Greybox tầng 5 — Phố Ngầm Bỏ Hoang, đủ 30 phòng:
/// A (trại orc), B (phố trên), C (phố dưới), D (mê cung tối, fog of war), E (boss).
/// Theo tài liệu "Thiet-Ke-Tang-5-Pho-Ngam-Bo-Hoang.md" (mục 1b: 1 ô lưới = 18×10 tile, y lưới tăng xuống dưới).
/// Luật mặt đường: mỗi con phố có một độ cao mặt đường cố định (phố trên: đáy hàng 5, phố dưới: đáy hàng 7),
/// cửa ngang giữa các phòng cùng dải luôn đặt ở hàng đáy để mặt phố liền một mạch.
/// Nhà dân: tường nền (không va chạm) + sàn tầng một chiều mỗi 5 tile + bậc lửng ở giữa, mái là sàn đi được.
/// Khối màu: tường giả (vàng), cổng/song sắt/orc gác (cam), chờ hạ boss/Minotaurus (đỏ), sàn nứt (tím, BreakableFloor).
/// Chạy: Tools > Culinaria > Build Fifth Floor (F5). Chạy lại bao nhiêu lần cũng được (xóa phần đã dựng rồi dựng lại).
/// Nội dung cũ của scene KHÔNG bị xóa, chỉ bị tắt (SetActive false) để tham chiếu.
public static partial class FifthFloorBuilder
{
    const string SceneNameExpected = "FifthFloorScene";
    const string ScenePath = "Assets/Scenes/FifthFloorScene.unity";
    const string BackupPath = "Assets/Scenes/Backup/FifthFloorScene_TruocF5Builder.unity";
    const int SW = 18, SH = 10;
    const string Out = "Assets/BackGround/FifthFloor/F5_Greybox";

    static readonly string[] GeneratedRoots = { "Grid_F5", "______Room_F5_____", "______Door_F5_____", "______Interact_F5_____",
                                                "______Hidden_F5_____", "______TODO_F5_____", "______FogZone_F5_____", "______Art_F5_____" };
    static readonly string[] OldRootsToDisable = { "Grid", "______Background______", "MovingPlatForm", "_____Minotaurus_____",
                                                   "LimitMapController", "LimitMapController (1)" };
    static readonly string[] OldVcamsToDisable = { "Virtual Camera", "Virtual Camera (1)" };

    static readonly Color ZoneA = new Color(0.62f, 0.45f, 0.32f);   // trại orc: đất nâu
    static readonly Color ZoneB = new Color(0.40f, 0.52f, 0.66f);   // phố trên: đá xanh xám
    static readonly Color ZoneC = new Color(0.34f, 0.55f, 0.56f);   // phố dưới: đá xanh rêu
    static readonly Color ZoneD = new Color(0.30f, 0.30f, 0.42f);   // mê cung: tím tối
    static readonly Color ZoneE = new Color(0.50f, 0.26f, 0.26f);   // boss: đỏ tối
    static readonly Color HouseWall = new Color(0.55f, 0.40f, 0.30f, 0.55f);
    static readonly Color TentWall = new Color(0.75f, 0.33f, 0.24f, 0.55f);
    static readonly Color PlatTint = new Color(0.86f, 0.70f, 0.45f);
    static readonly Color FakeWallTint = new Color(1f, 0.83f, 0.30f);
    static readonly Color GateTint = new Color(1f, 0.54f, 0.36f);
    static readonly Color GPTint = new Color(0.88f, 0.36f, 1f);
    static readonly Color BossGateTint = new Color(1f, 0.30f, 0.43f);

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
    const int minX = -SW, minY = -16 * SH, W = 18 * SW, H = 17 * SH;   // chừa 1 ô đá quanh lưới 0..15 × 0..14
    static bool[,] solid, plat, house, tent;
    static readonly List<string> log = new List<string>();
    static readonly List<(string name, RectInt area, string kind)> blockers = new List<(string, RectInt, string)>();
    static readonly List<(string id, RectInt area)> gpFloors = new List<(string, RectInt)>();
    static readonly List<(string id, float x, int y, int coins)> chests = new List<(string, float, int, int)>();
    static readonly List<(string id, RectInt area)> deadZones = new List<(string, RectInt)>();
    static readonly List<(string name, Vector3 pos)> markers = new List<(string, Vector3)>();
    static readonly HashSet<string> noAutoClimb = new HashSet<string> { "R16", "R41" };   // R16 cầu thang riêng, R41 bậc làm tay (giữ khe Wall Jump)
    static Sprite blockSprite; static Tile blockTile; static bool artOk;

    static int CellBottom(int gy) => -(gy + 1) * SH;
    static int CellLeft(int gx) => gx * SW;

    // ===================== DỮ LIỆU PHÒNG =====================
    static void R(string id, int gx, int gy, int w, int h)
        => rooms[id] = new RoomDef { id = "F5_" + id, gx = gx, gy = gy, w = w, h = h,
               r = new RectInt(CellLeft(gx), CellBottom(gy + h - 1), w * SW, h * SH) };
    static void D(string a, string b, int cx, int cy, string side, int off = 8, string kind = "open")
        => doors.Add(new Door { a = a, b = b, cx = cx, cy = cy, side = side, off = off, kind = kind });

    static void DefineRooms()
    {
        rooms = new Dictionary<string, RoomDef>(); doors.Clear();
        // Bản rộng (07/11/2026): phòng phố rộng ~1,5 lần để thêm nhà dân và giãn khoảng cách giữa các nhà.
        // Khu A — trại orc (dải 1, mặt đất: đáy hàng 1) + hầm nhiệm vụ
        R("R01", 0, 0, 1, 2); R("R02", 1, 1, 2, 1); R("R03", 3, 0, 4, 2); R("R04", 7, 1, 2, 1);
        R("R05", 9, 0, 5, 2); R("R06", 14, 1, 1, 1); R("R07", 3, 2, 3, 1); R("R08", 6, 2, 3, 1);
        // Khu B — phố trên (mặt phố: đáy hàng 5)
        R("R09", 13, 2, 1, 2); R("R10", 11, 4, 4, 2); R("R11", 15, 3, 1, 3); R("R12", 7, 4, 4, 2);
        R("R13", 4, 4, 3, 2); R("R14", 4, 6, 2, 1); R("R15", 0, 4, 4, 2);
        // Khu C — phố dưới (mặt phố: đáy hàng 7)
        R("R16", 0, 6, 1, 2); R("R17", 1, 6, 3, 2); R("R18", 4, 7, 3, 1); R("R19", 7, 6, 5, 2);
        R("R20", 12, 7, 3, 1); R("R21", 15, 6, 1, 3);
        // Khu D — mê cung tối (08/11/2026: tách thành khu riêng 20 phòng, hàng 8–13, để fog of war có đất diễn)
        R("R22", 13, 8, 2, 2); R("R23", 8, 8, 5, 1); R("R24", 5, 8, 3, 1); R("R25", 1, 8, 4, 1);
        R("R26", 8, 9, 4, 1); R("R27", 7, 9, 1, 2); R("R28", 3, 9, 4, 2); R("R29", 12, 9, 1, 3);
        R("R30", 15, 9, 1, 2); R("R31", 8, 10, 4, 1); R("R32", 13, 10, 2, 1); R("R33", 1, 10, 2, 2);
        R("R34", 3, 11, 5, 1); R("R35", 8, 11, 4, 1); R("R36", 13, 11, 3, 1); R("R37", 1, 12, 6, 1);
        R("R38", 7, 12, 3, 2); R("R39", 10, 12, 6, 1); R("R40", 10, 13, 4, 1); R("R41", 0, 8, 1, 5);
        // Khu E — boss
        R("R42", 0, 13, 2, 1); R("R43", 2, 13, 3, 2);

        D("R01", "R02", 0, 1, "right"); D("R02", "R03", 2, 1, "right"); D("R03", "R04", 6, 1, "right");
        D("R04", "R05", 8, 1, "right"); D("R05", "R06", 13, 1, "right");
        D("R03", "R07", 3, 1, "bottom", 8, "guard");        // nắp hầm, orc gác tới khi nhận nhiệm vụ
        D("R07", "R08", 5, 2, "right");
        D("R05", "R09", 13, 1, "bottom", 8, "gp");          // SÀN NỨT BẮT BUỘC
        D("R09", "R10", 13, 3, "bottom");
        D("R10", "R11", 14, 5, "right"); D("R10", "R12", 11, 5, "left"); D("R12", "R13", 7, 5, "left"); D("R13", "R15", 4, 5, "left");
        D("R13", "R14", 5, 5, "bottom", 8, "gp");           // GP-A: sàn quầy rượu → hầm rượu
        D("R15", "R16", 0, 5, "bottom", 8, "mino");         // mở khi hạ Minotaurus ở chợ cũ
        D("R16", "R17", 0, 7, "right"); D("R17", "R18", 3, 7, "right"); D("R18", "R19", 6, 7, "right");
        D("R19", "R20", 11, 7, "right"); D("R20", "R21", 14, 7, "right");
        D("R14", "R17", 4, 6, "left", 8, "fake");           // H1 tường giả hầm rượu ↔ nhà dân

        // ---- Mê cung ----
        D("R21", "R22", 15, 8, "left");                     // lối vào từ miệng cống
        D("R22", "R23", 13, 8, "left"); D("R22", "R30", 14, 9, "right");
        D("R23", "R24", 8, 8, "left"); D("R23", "R26", 9, 8, "bottom");
        D("R23", "R29", 12, 8, "bottom", 8, "drop");        // hố rơi: R23 → trục rơi R29 → (rơi tiếp) R39
        D("R24", "R25", 5, 8, "left", 8, "fake");           // H2 tường giả ngõ chuột ↔ hầm mộ trên
        D("R27", "R24", 7, 9, "top");
        D("R25", "R28", 3, 8, "bottom");
        D("R41", "R25", 0, 8, "right", 8, "gate");          // L3 song sắt, mở từ phía giếng đáy R41
        D("R26", "R27", 8, 9, "left"); D("R26", "R29", 11, 9, "right");
        D("R27", "R28", 7, 10, "left"); D("R27", "R31", 7, 10, "right");
        D("R29", "R39", 12, 11, "bottom", 8, "drop");
        D("R30", "R32", 15, 10, "left"); D("R32", "R36", 13, 10, "bottom"); D("R36", "R39", 14, 11, "bottom");
        D("R31", "R35", 10, 10, "bottom"); D("R35", "R34", 8, 11, "left"); D("R35", "R38", 9, 11, "bottom");
        D("R28", "R33", 3, 10, "left"); D("R28", "R34", 4, 10, "bottom");
        D("R33", "R37", 1, 11, "bottom", 8, "drop");        // hố vạc: rơi xuống kênh mộ
        D("R34", "R37", 5, 11, "bottom"); D("R37", "R38", 6, 12, "right"); D("R38", "R39", 9, 12, "right");
        D("R39", "R40", 11, 12, "bottom");                  // hầm khóa: ngõ cụt có Rương 7
        D("R37", "R41", 1, 12, "left");
        D("R41", "R16", 0, 8, "top", 8, "gate");            // L2 song sắt lên chân cầu thang đá, mở từ phía R41
        D("R41", "R42", 0, 12, "bottom");                   // đáy giếng → tiền phòng boss
        D("R42", "R43", 1, 13, "right");
    }

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/F5 - Reload FifthFloorScene from disk (bỏ thay đổi chưa lưu)")]
    public static void ReloadFromDisk()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[F5] Đã mở lại FifthFloorScene từ đĩa.");
    }

    [MenuItem("Tools/Culinaria/Build Fifth Floor (F5)")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneNameExpected)
        {
            EditorUtility.DisplayDialog("F5 Builder", "Hãy mở FifthFloorScene và đặt làm scene active trước.", "OK");
            return;
        }
        log.Clear(); blockers.Clear(); gpFloors.Clear(); chests.Clear(); deadZones.Clear(); markers.Clear(); houses.Clear(); tunnelCells.Clear();
        F5RoomMerge.Backup(scene);      // bản trước khi chia phòng kiểu Hollow Knight (một lần)

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
        artOk = PrepareArt();                 // phần hình ảnh (FifthFloorBuilder.Art.cs); thiếu tài nguyên thì giữ greybox màu
        BuildGeometry();
        WriteTilemaps();
        BuildRooms();
        BuildDoorsAndInteract();
        BuildHiddenBlockers();
        BuildFogZones(scene);
        BuildTodoMarkers();
        if (artOk) DecorateF5(scene);
        RemovePropsInTunnels(scene);
        F5RoomMerge.ApplyAll(scene);           // gộp phòng + vệt sáng hầm + chia khung camera (F5RoomMerge.cs, F5RoomFrames.cs)

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"Xong: {rooms.Count} phòng, {doors.Count} cửa, {chests.Count} rương, {gpFloors.Count} sàn nứt, {blockers.Count} khối chặn.");
        foreach (var l in log) Debug.Log("[F5] " + l);
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

    static RoomDef RoomAtCell(int gx, int gy) =>
        rooms.Values.FirstOrDefault(r => gx >= r.gx && gx < r.gx + r.w && gy >= r.gy && gy < r.gy + r.h);

    /// Bậc một chiều rộng 3, so le, mỗi bước ≤ 3 ô (tầm nhảy đơn). Giống tầng 4.
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

    /// Nhà nhiều tầng: tường nền + sàn tầng một chiều mỗi 5 tile + bậc lửng (để leo bằng nhảy đơn). Mái là sàn trên cùng.
    static void House(string id, int offX, int width, int floors, bool isTent = false)
    {
        var room = rooms[id];
        int x0 = room.r.xMin + offX, y0 = room.Floor, top = y0 + 5 * floors - 1;
        if (top > room.r.yMax - 4) { log.Add($"BỎ nhà {id}+{offX}: {floors} tầng không vừa trần"); return; }
        houses.Add((id, x0, y0, width, floors, isTent));
        var target = isTent ? tent : house;
        for (int x = x0; x < x0 + width; x++) for (int y = y0; y < top; y++) if (!Get(solid, x, y)) Set(target, x, y, true);
        if (isTent) return;                                                                   // lều orc: không có sàn tầng, không có mái đi được
        for (int k = 1; k <= floors; k++)
        {
            Plat(x0, y0 + 5 * k - 1, width);                                                 // sàn tầng k (tầng trên cùng = mái)
            int stepX = (k % 2 == 1) ? x0 + 1 : x0 + width - 4;                              // bậc lửng so le hai đầu nhà
            Plat(stepX, y0 + 5 * (k - 1) + 2, 3);
        }
    }

    static void BuildGeometry()
    {
        solid = new bool[W, H]; plat = new bool[W, H]; house = new bool[W, H]; tent = new bool[W, H];
        Fill(minX, minY, W, H);
        foreach (var r in rooms.Values) Carve(r.r.xMin + 1, r.r.yMin + 1, r.r.width - 2, r.r.height - 2);

        // ---- Cửa giữa các phòng (giống tầng 4) ----
        foreach (var d in doors)
        {
            RoomDef A = rooms[d.a], B = rooms[d.b];
            if (d.side == "right" || d.side == "left")
            {
                int bx = d.side == "right" ? (d.cx + 1) * SW : d.cx * SW;
                int yb = CellBottom(d.cy) + 1;
                Carve(bx - 1, yb, 2, 3);
                if (d.kind == "fake") blockers.Add(($"FakeWall_{(A.id == "F5_R14" ? "H1" : "H2")}_{A.id}_{B.id}", new RectInt(bx - 1, yb, 2, 3), "fake"));
                if (d.kind == "gate") blockers.Add(($"Gate_L3_SongSat_{B.id}_{A.id} (mở từ {A.id})", new RectInt(bx - 1, yb, 2, 3), "gate"));
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
                if (d.kind != "drop" && !noAutoClimb.Contains(lower.id.Substring(3))) ClimbStack(lower, hx, lower.r.yMax - 3, true);   // hố rơi: không có bậc leo ngược
                var hole = new RectInt(hx, by - 1, 3, 2);
                RoomDef upper = d.side == "bottom" ? A : B;
                switch (d.kind)
                {
                    case "gp": gpFloors.Add(($"F5_{upper.id.Substring(3)}_GP_{(d.a == "R05" ? "Chinh" : "A")}", hole)); break;
                    case "guard": blockers.Add(($"Gate_OrcGacHam_{A.id}_{B.id}", hole, "gate")); break;
                    case "mino": blockers.Add(($"Gate_Minotaurus_{A.id}_{B.id}", hole, "boss")); break;
                    case "gate": blockers.Add(($"Gate_L2_SongSat_{B.id}_{A.id} (mở từ {A.id})", hole, "gate")); break;
                }
            }
        }

        // ---- R01: gờ trên cùng cho cửa lên tầng 4 + bậc leo (Wall Jump dùng 2 vách) ----
        var r01 = rooms["R01"];
        Fill(r01.r.xMin + 1, r01.r.yMax - 9, 8, 1);
        ClimbStack(r01, r01.r.xMin + 10, r01.r.yMax - 8, false);

        // ---- R03 chợ orc (72 ô): lều hai đầu, sạp giữa trên giàn gỗ hai tầng ----
        var r03 = rooms["R03"];
        House("R03", 3, 9, 1, true); House("R03", 60, 9, 1, true);
        Plat(r03.r.xMin + 18, r03.Floor + 3, 10); Plat(r03.r.xMin + 34, r03.Floor + 3, 10); Plat(r03.r.xMin + 25, r03.Floor + 6, 13);
        Plat(r03.r.xMin + 48, r03.Floor + 3, 8);

        // ---- R05 trại orc (90 ô): 4 lều dân, sàn nứt lớn ở góc phải (cửa xuống R09) ----
        House("R05", 3, 9, 1, true); House("R05", 18, 8, 1, true); House("R05", 32, 9, 1, true); House("R05", 48, 9, 1, true);

        // ---- R06 đài canh: Rương 1 trên mái chòi, gờ cao 5 ô (cần Double Jump) ----
        var r06 = rooms["R06"];
        Fill(r06.r.xMax - 7, r06.Floor + 4, 5, 1);
        chests.Add(("F5_Chest01", r06.r.xMax - 4.5f, r06.Floor + 5, 800));

        // ---- R08 ổ trộm: vật phẩm nhiệm vụ ở cuối phòng ----
        markers.Add(("Quest_BaoLuongOrc (vật phẩm nhiệm vụ)", new Vector3(rooms["R08"].r.xMax - 4.5f, rooms["R08"].Floor + 0.5f, 0)));

        // ---- R10 cổng thành (72 ô): trụ cổng có vòm thấp 4 ô, nhà gác, 2 nhà dân. Ô 39–51 chừa cho bậc dưới giếng sụt ----
        var r10 = rooms["R10"];
        Fill(r10.r.xMin + 15, r10.Floor + 4, 3, r10.r.height - 6);
        House("R10", 2, 10, 2); House("R10", 23, 10, 3); House("R10", 56, 12, 3);

        // ---- R11 tháp gác: khe Wall Jump 3 ô sát tường trái, Rương 2 trên đỉnh ----
        var r11 = rooms["R11"];
        int px11 = r11.r.xMin + 4;
        Fill(px11, r11.Floor + 3, 2, r11.r.height - 9);
        Fill(px11, r11.r.yMax - 6, 7, 1);
        chests.Add(("F5_Chest02", px11 + 4.5f, r11.r.yMax - 5, 1000));

        // ---- R12 phố rèn (72 ô): lò rèn (khối đặc có hốc, sàn nứt GP-B trên nóc) + 3 nhà, cách nhau 6–7 ô ----
        var r12 = rooms["R12"];
        int fx = r12.r.xMin + 3;
        Fill(fx, r12.Floor, 9, 4);
        Carve(fx + 1, r12.Floor, 7, 3);                       // hốc dưới lò
        gpFloors.Add(("F5_R12_GP_B", new RectInt(fx + 3, r12.Floor + 3, 3, 1)));
        chests.Add(("F5_Chest03", fx + 4.5f, r12.Floor, 800));
        Plat(fx + 9, r12.Floor + 2, 3);                       // bậc lên nóc lò
        House("R12", 20, 10, 3); House("R12", 37, 10, 2); House("R12", 54, 10, 3);

        // ---- R13 quán rượu (54 ô): quán 3 tầng (sàn quầy có sàn nứt GP-A) + 1 nhà dân 2 tầng ----
        House("R13", 4, 26, 3); House("R13", 38, 10, 2);

        // ---- R14 hầm rượu: Rương 4 ----
        chests.Add(("F5_Chest04", rooms["R14"].r.xMin + 12.5f, rooms["R14"].Floor, 800));

        // ---- R15 chợ cũ (72 ô): 3 nhà dân, quảng trường 24 ô ở giữa cho Minotaurus ----
        House("R15", 13, 9, 2); House("R15", 47, 9, 3); House("R15", 58, 8, 2);   // nhà cuối dời từ ô 62 → 58: chừa 4 ô cho hầm R13|R15
        markers.Add(("Minotaurus_MiniBoss (EnemyStats riêng ~60%)", new Vector3(rooms["R15"].r.xMin + 34, rooms["R15"].Floor + 1, 0)));

        // ---- R16 cầu thang đá: hai đợt bậc gấp khúc ----
        var r16 = rooms["R16"];
        int sx = r16.r.xMin + 7, sy = r16.r.yMax - 5;
        for (int i = 0; i < 4; i++) Fill(sx + 2 * i, sy - i, 2, 1);                  // đợt 1: đi sang phải, xuống
        Fill(r16.r.xMax - 5, sy - 4, 4, 1);                                           // chiếu nghỉ
        int sy2 = sy - 5;
        for (int i = 0; i < 7; i++) Fill(r16.r.xMax - 7 - 2 * i, sy2 - i, 2, 1);      // đợt 2: quay sang trái, xuống
        Fill(r16.r.xMin + 4, r16.Floor + 1, 2, 1);                                    // bậc thấp cuối cùng, leo ngược lên được

        // ---- R17 nhà dân (54 ô): 3 nhà, Rương 5 ở tầng áp mái nhà giữa ----
        House("R17", 2, 12, 3); House("R17", 20, 12, 3); House("R17", 38, 10, 2);
        chests.Add(("F5_Chest05", rooms["R17"].r.xMin + 26.5f, rooms["R17"].Floor + 10, 600));

        // ---- R18 hẻm hàng thịt (54 ô): 3 nhà dân thấp ----
        House("R18", 3, 8, 1); House("R18", 18, 9, 1); House("R18", 34, 9, 1);

        // ---- R19 đại lộ sụt (90 ô): vực 8 ô giữa phố (DeadZone), 4 nhà hai bên, đường mái ----
        var r19 = rooms["R19"];
        int pitX = r19.r.xMin + 41;
        Carve(pitX, r19.r.yMin, 8, 1);
        deadZones.Add(("F5_R19_Vuc_DeadZone", new RectInt(pitX, r19.r.yMin, 8, 1)));
        House("R19", 2, 11, 3); House("R19", 20, 10, 2); House("R19", 56, 10, 3); House("R19", 73, 10, 2);
        markers.Add(("Minotaurus_TuanTra", new Vector3(r19.r.xMin + 66, r19.Floor + 1, 0)));

        // ---- R20 thánh đường: bệ thờ ----
        var r20 = rooms["R20"];
        Fill(r20.r.xMin + 25, r20.Floor, 4, 1);

        // ---- Mê cung: vách ngăn thò từ trần (chừa khe bò 3 ô) / dựng từ sàn (cao 3, nhảy qua) ----
        MazeWalls("R23", 14, 30, 48, 64, 78); MazeWalls("R24", 26); MazeWalls("R25", 16, 30, 46, 58);
        MazeWalls("R26", 20, 36, 54); MazeWalls("R31", 18, 34, 48, 62); MazeWalls("R34", 20, 34, 52, 66, 80);
        MazeWalls("R35", 26, 40, 56); MazeWalls("R37", 16, 34, 50, 66, 84, 98); MazeWalls("R39", 16, 30, 48, 62, 78, 94);
        MazeWalls("R36", 30, 44); MazeWalls("R40", 16, 30, 46);
        var r22 = rooms["R22"]; Plat(r22.r.xMin + 6, r22.Floor + 9, 24);
        var r28 = rooms["R28"]; Fill(r28.r.xMin + 20, r28.Floor + 6, 30, 1); Plat(r28.r.xMin + 14, r28.Floor + 3, 4); Plat(r28.r.xMin + 52, r28.Floor + 3, 4);   // ngã tư: tầng lửng giữa phòng
        var r38 = rooms["R38"];                                                     // đại sảnh cột: 4 cột đá lớn, đèn thờ ở giữa
        foreach (int cx in new[] { 8, 18, 34, 44 }) Fill(r38.r.xMin + cx, r38.Floor + 4, 2, r38.r.height - 6);
        Plat(r38.r.xMin + 12, r38.Floor + 4, 6); Plat(r38.r.xMin + 36, r38.Floor + 4, 6);

        // ---- R41 giếng đáy: khe Wall Jump 3 ô sát tường trái, cột cao tới trần, hốc rương khoét vào cột từ phía khe → Rương 6.
        //      Bậc leo giữa giếng làm tay (so le hai cột), lên tới gờ song sắt L3 (R25) và lỗ song sắt L2 trên trần (R16).
        var r41 = rooms["R41"];
        int f41 = r41.Floor, x41 = r41.r.xMin;
        Fill(x41 + 4, f41 + 3, 3, r41.r.yMax - 2 - (f41 + 3) + 1);
        Carve(x41 + 4, f41 + 33, 2, 3);
        chests.Add(("F5_Chest06", x41 + 4.5f, f41 + 33, 1200));
        for (int dy = 3, i = 0; dy <= 45; dy += 3, i++) Plat(i % 2 == 0 ? x41 + 7 : x41 + 11, f41 + dy - 1, 3);

        // ---- R40 hầm khóa: ngõ cụt duy nhất, Rương 7 ở cuối ----
        chests.Add(("F5_Chest07", rooms["R40"].r.xMax - 5.5f, rooms["R40"].Floor, 1500));

        CarveTunnels();
    }

    // ===================== HẦM Ở RANH GIỚI PHÒNG (kiểu Hollow Knight) =====================
    static readonly HashSet<Vector2Int> tunnelCells = new HashSet<Vector2Int>();

    /// Cửa ngang còn giữ ranh giới (F5RoomMerge.Tunnels) thành đường hầm dài 2*HalfLen ô: đắp đá từ đỉnh cửa lên tới trần ở cả hai phía.
    static void CarveTunnels()
    {
        int hl = F5RoomMerge.HalfLen, dh = F5RoomMerge.DoorH;
        foreach (var (bx, ys) in F5RoomMerge.Tunnels)
        {
            int n = 0;
            for (int x = bx - hl; x < bx + hl; x++)
            {
                if (!Get(solid, x, ys - 1)) log.Add($"CẢNH BÁO hầm x={bx}: cột {x} không có sàn ở y={ys - 1}");
                var col = new List<int>();
                int y = ys + dh;
                while (!Get(solid, x, y) && col.Count < 40) { col.Add(y); y++; }
                if (col.Count >= 40) { log.Add($"CẢNH BÁO hầm x={bx}: cột {x} không có trần, bỏ qua"); continue; }
                foreach (int yy in col)
                {
                    Set(solid, x, yy, true); Set(plat, x, yy, false); Set(house, x, yy, false); Set(tent, x, yy, false);
                    tunnelCells.Add(new Vector2Int(x, yy)); n++;
                }
            }
            var area = new RectInt(bx - hl, ys, 2 * hl, 40);
            foreach (var h in houses)
                if (new RectInt(h.x0, h.y0, h.w, 5 * h.floors).Overlaps(area)) log.Add($"CẢNH BÁO hầm x={bx}: đè lên nhà {h.room}@{h.x0}");
            log.Add($"Hầm x={bx} y={ys}: đắp {n} ô");
        }
    }

    /// Đồ trang trí / đuốc có tâm nằm trong đá hầm thì xóa (dữ liệu đặt đồ đã né sẵn, đây chỉ là lưới an toàn).
    static void RemovePropsInTunnels(Scene scene)
    {
        var art = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "______Art_F5_____");
        if (art == null || tunnelCells.Count == 0) return;
        foreach (var grp in new[] { "Props", "Torches" })
        {
            var g = art.transform.Find(grp);
            if (g == null) continue;
            foreach (Transform t in g.Cast<Transform>().ToList())
            {
                var sr = t.GetComponentInChildren<SpriteRenderer>();
                Vector3 c = sr != null ? sr.bounds.center : t.position;
                if (!tunnelCells.Contains(new Vector2Int(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.y)))) continue;
                log.Add($"Xóa đồ trang trí bị đá hầm lấp: {t.name} @({c.x:F1},{c.y:F1})");
                Object.DestroyImmediate(t.gameObject);
            }
        }
    }

    // ===================== TILE =====================
    static void MakeBlockTile()
    {
        string folder = Out;
        if (!AssetDatabase.IsValidFolder("Assets/BackGround")) AssetDatabase.CreateFolder("Assets", "BackGround");
        if (!AssetDatabase.IsValidFolder("Assets/BackGround/FifthFloor")) AssetDatabase.CreateFolder("Assets/BackGround", "FifthFloor");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/BackGround/FifthFloor", "F5_Greybox");
        string png = folder + "/F5_Block.png";
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
        string tilePath = folder + "/F5_Block.asset";
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

    static bool IsBossArea(int x, int y) => y < -130 && x < 90;                 // R42, R43 (hàng 13–14, cột 0–4)
    static Color ZoneColor(int x, int y) => y >= -30 ? ZoneA : y >= -60 ? ZoneB : y >= -80 ? ZoneC : IsBossArea(x, y) || y < -140 ? ZoneE : ZoneD;

    /// Vách ngăn mê cung tại các khoảng cách (tile) tính từ mép trái phòng; tự bỏ vách nào đè lên lỗ cửa trên sàn hoặc trần.
    static void MazeWalls(string id, params int[] offs)
    {
        var r = rooms[id];
        var holes = doors.Where(d => (d.side == "bottom" || d.side == "top") && (d.a == id || d.b == id))
                         .Select(d => CellLeft(d.cx) + d.off)
                         .Where(hx => hx >= r.r.xMin && hx < r.r.xMax).ToList();
        for (int i = 0; i < offs.Length; i++)
        {
            bool Bad(int o) => o < 5 || o > r.r.width - 6 || holes.Any(hx => r.r.xMin + o > hx - 4 && r.r.xMin + o < hx + 6);
            int off = offs[i];
            if (Bad(off)) off = new[] { 7, -7, 11, -11 }.Select(k => offs[i] + k).Where(o => !Bad(o)).DefaultIfEmpty(-1).First();   // dời vách ra khỏi lỗ cửa
            if (off < 0) { log.Add($"Bỏ vách {id}+{offs[i]} (sát cửa/mép)"); continue; }
            int x = r.r.xMin + off;
            if (i % 2 == 0) Fill(x, r.Floor + 3, 2, r.r.yMax - 1 - (r.Floor + 3));
            else Fill(x, r.Floor, 2, 3);
        }
    }

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
        var gridT = new GameObject("Grid_F5", typeof(Grid)).transform;
        int groundLayer = LayerMask.NameToLayer("Ground");

        if (!artOk)                                   // có hình ảnh thì vỏ nhà là sprite (Art), không cần khối tường màu
        {
            Paint(NewTilemap(gridT, "F5_HouseWall (nhà dân, không va chạm)", -10), house, (x, y) => HouseWall);
            Paint(NewTilemap(gridT, "F5_TentWall (lều orc, không va chạm)", -10), tent, (x, y) => TentWall);
        }

        var ground = NewTilemap(gridT, "F5_Ground", 0, groundLayer);
        if (artOk) PaintGroundArt(ground); else Paint(ground, solid, (x, y) => ZoneColor(x, y));
        var tc = ground.gameObject.AddComponent<TilemapCollider2D>();
        var rb = ground.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Static;
        ground.gameObject.AddComponent<CompositeCollider2D>();
#pragma warning disable 0618
        tc.usedByComposite = true;
#pragma warning restore 0618

        var platTm = NewTilemap(gridT, "F5_Platform_OneWay (sàn tầng nhà, bậc)", 0, groundLayer);
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

        int nHouse = 0, nPlat = 0;
        foreach (var b in house) if (b) nHouse++;
        foreach (var b in plat) if (b) nPlat++;
        log.Add($"Tile: tường nhà {nHouse}, sàn một chiều {nPlat}");
    }

    // ===================== ROOM (Module 5) =====================
    static GameObject Group(string name) => new GameObject(name);

    static void BuildRooms()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Room.prefab");
        if (prefab == null) { log.Add("THIẾU prefab Assets/Prefab/Room.prefab"); return; }
        var parent = Group("______Room_F5_____").transform;
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

    static void MakeCheckpoint(Transform parent, string id, float x, RoomDef room)
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
        Inst("Assets/Prefab/CampFire.prefab", go.transform, new Vector3(x, yStand + 0.5f, 0), "CampFire (bếp nấu)");
    }

    static void BuildDoorsAndInteract()
    {
        var doorsT = Group("______Door_F5_____").transform;
        var r01 = rooms["R01"].r;
        float ledgeStand = r01.yMax - 8;

        // Cửa lên tầng 4: đọc FourthFloorScene xem cửa nào trỏ về tầng 5 (F4_Exit → F5_Entry)
        var up = FindPartnerDoor("Assets/Scenes/FourthFloorScene.unity", SceneNameExpected);
        string myEntry = up.myId ?? "F5_Entry", f4Door = up.theirId ?? "F4_Exit";
        log.Add(up.theirId == null ? "CHƯA KHỚP cửa tầng 4: tạm đặt F5_Entry → F4_Exit." : $"Cửa tầng 4: {myEntry} ↔ {f4Door} (đọc từ FourthFloorScene)");
        MakeDoor(doorsT, myEntry, new Vector3(r01.xMin + 2.5f, ledgeStand + 1.5f, 0), "Assets/Scenes/FourthFloorScene.unity", f4Door, 1);

        // Cửa xuống tầng 6 (R30, mở sau boss)
        var down = FindPartnerDoor("Assets/Scenes/SixthFloorScene.unity", SceneNameExpected);
        string myExit = down.myId ?? "F5_Exit", f6Door = down.theirId ?? "F6_Entry";
        log.Add(down.theirId == null ? "CHƯA KHỚP cửa tầng 6: SixthFloorScene chưa có SceneDoor trỏ về tầng 5. Tạm đặt F5_Exit → F6_Entry." : $"Cửa tầng 6: {myExit} → {f6Door}");
        var r30 = rooms["R43"].r;
        MakeDoor(doorsT, myExit, new Vector3(r30.xMax - 3.5f, r30.yMin + 2.5f, 0), "Assets/Scenes/SixthFloorScene.unity", f6Door, 0);
        blockers.Add(("Gate_Boss_TruocCuaTang6", new RectInt(r30.xMax - 7, r30.yMin + 1, 1, 5), "boss"));

        // Điểm hồi sinh mặc định của scene: dời FifthFloorSpawnPoint có sẵn về R01 (không tạo cái thứ hai)
        var sp = GameObject.FindGameObjectsWithTag("SpawnPoint").FirstOrDefault(g => g.scene == SceneManager.GetActiveScene());
        if (sp == null) { sp = new GameObject("FifthFloorSpawnPoint"); sp.tag = "SpawnPoint"; sp.transform.SetParent(doorsT); }
        sp.transform.position = new Vector3(r01.xMin + 6.5f, ledgeStand + 1f, 0);
        log.Add($"Điểm hồi sinh '{sp.name}' dời về R01.");

        var it = Group("______Interact_F5_____").transform;
        MakeCheckpoint(it, "F5_CP01_TraiOrc", rooms["R05"].r.xMin + 62.5f, rooms["R05"]);
        MakeCheckpoint(it, "F5_CP02_QuanRuou", rooms["R13"].r.xMin + 10.5f, rooms["R13"]);
        MakeCheckpoint(it, "F5_CP03_DenTho", rooms["R27"].r.xMin + 9.5f, rooms["R27"]);
        MakeCheckpoint(it, "F5_CP04_TienPhongBoss", rooms["R42"].r.xMin + 26.5f, rooms["R42"]);
        foreach (var c in chests) MakeChest(it, c.id, c.x, c.y, c.coins);

        var boss = new GameObject("BossSpawn (boss tầng 5 - chưa thiết kế)");
        boss.transform.SetParent(it); boss.transform.position = new Vector3(rooms["R43"].r.center.x, rooms["R43"].Floor + 1, 0);

        var r05 = rooms["R05"];
        var safe = new GameObject("F5_SafeZone_TODO (Trại orc R05)");
        safe.transform.SetParent(it);
        safe.transform.position = new Vector3(r05.r.center.x, r05.r.center.y, 0);
        var sb = safe.AddComponent<BoxCollider2D>(); sb.isTrigger = true;
        sb.size = new Vector2(r05.r.width - 2, r05.r.height - 2);

        foreach (var dz in deadZones)
        {
            var go = new GameObject(dz.id); go.tag = "DeadZone";
            go.transform.SetParent(it);
            go.transform.position = new Vector3(dz.area.center.x, dz.area.center.y, 0);
            var c = go.AddComponent<BoxCollider2D>(); c.isTrigger = true; c.size = new Vector2(dz.area.width, dz.area.height);
        }
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

    static GameObject Block(Transform parent, string name, RectInt a, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = new Vector3(a.x + a.width / 2f, a.y + a.height / 2f, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = blockSprite; sr.color = tint; sr.sortingOrder = 1;
        Vector2 ss = blockSprite != null ? (Vector2)blockSprite.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(a.width / ss.x, a.height / ss.y, 1);
        go.AddComponent<BoxCollider2D>().size = ss;
        return go;
    }

    static void BuildHiddenBlockers()
    {
        var hid = Group("______Hidden_F5_____").transform;
        foreach (var b in blockers)
        {
            string note = b.kind == "fake" ? " (tường giả - chưa có script phá)"
                        : b.kind == "boss" ? " (mở khi hạ boss/Minotaurus - chưa có script)"
                        : " (cổng/orc gác/song sắt - chưa có script mở)";
            Color tint = b.kind == "fake" ? FakeWallTint : b.kind == "boss" ? BossGateTint : GateTint;
            Block(hid, b.name + note, b.area, tint);
        }
        foreach (var (id, rect) in gpFloors)
        {
            var gp = Block(hid, id + "_BreakableFloor", rect, GPTint);
            var so = new SerializedObject(gp.AddComponent<BreakableFloor>());
            so.FindProperty("floorId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        markers.Add(("Lever_L2_SongSat (đỉnh R41, mở lên R16)", new Vector3(rooms["R41"].r.xMin + 12.5f, rooms["R41"].r.yMax - 4f, 0)));
        markers.Add(("Lever_L3_SongSat (gờ R41, mở sang R25)", new Vector3(rooms["R41"].r.xMin + 13.5f, rooms["R41"].r.yMax - 9f, 0)));
        markers.Add(("Lift_ThangToiOrc_TODO (đáy R09, thay bậc leo bằng thang tời + cần gạt)", new Vector3(rooms["R09"].r.xMin + 4.5f, rooms["R09"].Floor + 1, 0)));
    }

    static void BuildFogZones(Scene scene)
    {
        GameObject overlay = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "FogOfWarCanvas") overlay = t.gameObject;
        if (overlay == null) { log.Add("KHÔNG thấy FogOfWarCanvas: bỏ qua vùng fog."); return; }
        overlay.SetActive(false);                                   // mặc định tắt; FogOfWarZone bật khi vào mê cung
        var parent = Group("______FogZone_F5_____").transform;
        foreach (var id in Enumerable.Range(22, 20).Select(n => "R" + n))
        {
            var r = rooms[id].r;
            var go = new GameObject("FogZone_" + id);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(r.center.x, r.center.y, 0);
            var c = go.AddComponent<BoxCollider2D>(); c.isTrigger = true; c.size = new Vector2(r.width - 1, r.height - 1);
            var so = new SerializedObject(go.AddComponent<FogOfWarZone>());
            so.FindProperty("fogOverlay").objectReferenceValue = overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        log.Add("Fog of war: tắt mặc định, bật trong R22–R41 (FogOfWarZone).");
    }

    static void BuildTodoMarkers()
    {
        var todo = Group("______TODO_F5_____").transform;
        void M(string name, string room, float dx, float dy) => markers.Add((name, new Vector3(rooms[room].r.xMin + dx, rooms[room].Floor + dy, 0)));
        M("NPC_OrcLinhGac (chỉ đường, không đánh)", "R02", 18, 1);
        M("NPC_OrcThuongNhan (dùng ShopNpc, sprite orc)", "R03", 30, 1);
        M("NPC_OrcGacNapHam (đứng trên Gate_OrcGacHam)", "R03", 9, 1);
        M("NPC_TocTruongOrc (giao nhiệm vụ Bao lương → Ground Pound)", "R05", 10, 1);
        M("NPC_DanOrc (đi lại, trẻ con chạy quanh)", "R05", 42, 1);
        M("Quai_Goblin + Doi", "R07", 26, 1); M("Quai_Goblin + Nhen", "R08", 22, 1);
        M("Quai_BoXuongLinhGac", "R10", 34, 1); M("Quai_Undead", "R12", 48, 1);
        M("Quai_NamQuai (nguồn ăn: nấm)", "R17", 16, 1); M("Quai_Undead", "R18", 28, 1);
        M("Quai_QuyLua (dấu vết rồng: vảy, vết cháy)", "R20", 14, 1);
        M("Quai_Slime + Doi", "R22", 18, 1); M("Quai_Doi", "R23", 40, 1); M("Quai_BoXuong", "R25", 40, 1);
        M("Quai_Undead", "R26", 30, 1); M("Quai_Nhen", "R28", 40, 1); M("Quai_Nhen (rơi từ trần)", "R29", 9, 20);
        M("Quai_Slime", "R30", 9, 1); M("Quai_BoXuong + Undead", "R31", 40, 1); M("Quai_NhenLon x2 (ổ nhện)", "R32", 18, 1);
        M("Quai_Doi x2 (trong hố)", "R33", 18, 6); M("Quai_Undead (đứng im, giống tượng)", "R34", 44, 1); M("Quai_Slime", "R35", 30, 1);
        M("Quai_Slime x2", "R36", 24, 1); M("Quai_BoXuong + NhenLon", "R37", 60, 1); M("Quai_Undead x2", "R39", 54, 1); M("Quai_Nhen", "R40", 24, 1);
        M("R43: phòng boss trống", "R43", 27, 2);
        foreach (var (name, pos) in markers) { var go = new GameObject(name); go.transform.SetParent(todo); go.transform.position = pos; }
    }
}
