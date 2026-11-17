using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// [Tầng 5] Ảnh nền nhiều lớp có chiều sâu cho trại orc, phố trên, chợ cũ, phố dưới, mê cung và khu boss (ảnh AI do người dùng tạo, đã xử lý: xóa dấu, tách nền, gom màu).
///  - Mọi thứ nằm dưới root riêng "______SceneBG_F5_____". FifthFloorBuilder KHÔNG xóa root này; tool không đụng nền cũ, RoomGlow hay phòng.
///  - Mỗi phòng có 1 object nền "BG_F5_Rxx" (gán vào Room.background). Bên trong mỗi lớp là một nhóm đặt ở độ sâu z riêng,
///    phóng (d+z)/d quanh tâm phòng, nên khi camera bị giới hạn trong phòng thì nền luôn phủ kín khung hình (quy trình culinaria-bg-depth-group).
///  - Cần vcam tầng 5 ở chế độ Perspective, FOV 30, Camera Distance = D (như tầng 1). Nếu chưa, tool chỉ báo, không sửa camera.
/// Chạy: Tools > Culinaria > F5 Scene Backdrops (trại orc). Chạy lại được. Gỡ: xóa root "______SceneBG_F5_____".
public static class F5SceneBackdrops
{
    const string SceneName = "FifthFloorScene";
    const string ScenePath = "Assets/Scenes/FifthFloorScene.unity";
    const string RootName = "______SceneBG_F5_____";
    const string SrcDir = "Assets/Resouces/F5_Backgrounds/";
    const float PPU = 9.6f;           // ảnh 341×190 → cao ~20 ô
    const float D = 18.7f;            // khoảng cách camera = 5 / tan(15°)

    /// Một lớp: ảnh, độ sâu z (theo D), chiều cao (ô) mỗi bản, y đáy (null = căn giữa phòng), khoảng hở giữa các bản, độ sáng, sortingOrder.
    struct Layer { public string img; public float z, height, gap, light; public float? yBottom; public int order; public float x0; public Color? tint; public RectInt? area; public bool tileV; }

    /// Phòng (tên Room sau khi gộp) → hình chữ nhật phủ phòng (tile) + các lớp.
    static readonly (string room, RectInt rect, Layer[] layers)[] Rooms =
    {
        ("F5_R01", new RectInt(0, -20, 270, 20), new[]       // trại orc gộp R01–R06
        {
            new Layer { img = "F5_BG_OrcCave_Far", z = 2f * D, height = 20f, light = 0.85f, order = -97 },
            new Layer { img = "F5_BG_OrcCamp_Mid", z = D, height = 8.6f, yBottom = -19f, gap = 9f, x0 = 4f, light = 1f, order = -95 },
        }),
        ("F5_R07", new RectInt(54, -30, 108, 10), new[]      // hầm nhiệm vụ R07+R08: chỉ vách hang, tối hơn
        {
            new Layer { img = "F5_BG_OrcCave_Far", z = D, height = 10f, light = 0.6f, order = -97 },
        }),
        ("F5_R10", new RectInt(72, -60, 216, 30), new[]      // phố trên gộp R10–R13 (R11 tháp cao tới -30)
        {
            new Layer { img = "F5_BG_OldCity_Far", z = 2f * D, height = 30f, light = 0.8f, order = -97 },
            new Layer { img = "F5_BG_OldCity_Mid", z = D, height = 13f, yBottom = -59.3f, light = 0.6f, order = -95 },
            new Layer { img = "F5_BG_OldCity_Near", z = 0.4f * D, height = 8f, yBottom = -59.7f, gap = 10f, x0 = 2f, light = 0.5f, order = -93 },
        }),
        ("F5_R15", new RectInt(0, -60, 72, 20), new[]        // chợ cũ, ổ Minotaurus: một bản ảnh, mặt đá cuội trùng mặt đường, thạch nhũ nằm trên trần
        {
            new Layer { img = "F5_BG_OldMarket", z = D, height = 35.96f, yBottom = -62.1f, light = 0.85f, order = -95 },
            new Layer { img = "F5_BG_OldCity_Near", z = 0.4f * D, height = 8f, yBottom = -59.7f, gap = 30f, x0 = 4f, light = 0.45f, order = -93 },
        }),
        ("F5_R16", new RectInt(0, -90, 288, 30), new[]       // phố dưới gộp R16–R21: khu nhà nghèo (xa), cống + hàng thịt (giữa), thánh đường R20
        {
            new Layer { img = "F5_BG_LowerCity_Far", z = 2f * D, height = 40f, yBottom = -90f, light = 0.8f, order = -97 },   // thạch nhũ phía trên nằm ngoài khung
            new Layer { img = "F5_BG_Sewer_Mid", z = D, height = 12f, yBottom = -79.6f, light = 0.6f, order = -95 },
            // Thánh đường R20 (54×10): một bản, phủ đúng vùng R20, không chiều sâu để không trôi lệch trong phòng gộp rộng 288 ô.
            new Layer { img = "F5_BG_Cathedral", z = 0f, height = 18f, yBottom = -81.34f, light = 0.9f, order = -94, area = new RectInt(216, -80, 54, 10) },
        }),
    };

    /// Mê cung (R22–R41) + khu boss: tường cống hoặc hầm mộ, lặp lật gương; R42 hầm mộ ngả đỏ; R43 phòng ngai.
    static IEnumerable<(string room, RectInt rect, Layer[] layers)> MazeAndBoss()
    {
        var sewer = new (string id, RectInt r)[] {
            ("F5_R22", new RectInt(234, -100, 36, 20)), ("F5_R23", new RectInt(144, -90, 90, 10)), ("F5_R26", new RectInt(144, -100, 72, 10)),
            ("F5_R30", new RectInt(270, -110, 18, 20)), ("F5_R32", new RectInt(234, -110, 36, 10)), ("F5_R35", new RectInt(144, -120, 72, 10)),
            ("F5_R36", new RectInt(234, -120, 54, 10)), ("F5_R39", new RectInt(180, -130, 108, 10)), ("F5_R40", new RectInt(180, -140, 72, 10)) };
        var catacomb = new (string id, RectInt r)[] {
            ("F5_R24", new RectInt(90, -90, 54, 10)), ("F5_R25", new RectInt(18, -90, 72, 10)), ("F5_R27", new RectInt(126, -110, 18, 20)),
            ("F5_R28", new RectInt(54, -110, 72, 20)), ("F5_R29", new RectInt(216, -120, 18, 30)), ("F5_R31", new RectInt(144, -110, 72, 10)),
            ("F5_R33", new RectInt(18, -120, 36, 20)), ("F5_R34", new RectInt(54, -120, 90, 10)), ("F5_R37", new RectInt(18, -130, 108, 10)),
            ("F5_R38", new RectInt(126, -140, 54, 20)), ("F5_R41", new RectInt(0, -130, 18, 50)) };
        // Tường cống: dải nước ở chân ảnh nằm sát mặt sàn; chỉ lặp ngang (mọi phòng cống cao ≤ 20 ô)
        foreach (var (id, r) in sewer)
            yield return (id, r, new[] { new Layer { img = "F5_BG_Maze_Sewer", z = D, height = 20f, yBottom = r.yMin, light = 0.75f, order = -97 } });
        // Hầm mộ: lặp cả ngang lẫn dọc (giếng R41 cao 50 ô)
        foreach (var (id, r) in catacomb)
            yield return (id, r, new[] { new Layer { img = "F5_BG_Maze_Catacomb", z = D, height = 20f, yBottom = r.yMin, light = 0.75f, order = -97, tileV = true } });
        yield return ("F5_R42", new RectInt(0, -140, 36, 10), new[]      // tiền phòng boss: hầm mộ ngả đỏ
            { new Layer { img = "F5_BG_Maze_Catacomb", z = D, height = 20f, yBottom = -140f, tint = new Color(0.8f, 0.5f, 0.5f), order = -97, tileV = true } });
        yield return ("F5_R43", new RectInt(36, -150, 54, 20), new[]      // phòng boss: mép trước sàn đá trùng mặt sàn, khe dung nham nằm khuất dưới nền đất
            { new Layer { img = "F5_BG_BossThrone", z = D, height = 30.4f, yBottom = -153.49f, light = 0.9f, order = -95 } });
    }

    [MenuItem("Tools/Culinaria/F5 Scene Backdrops")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

        var log = new List<string>();
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        var rooms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        int sprites = 0;
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (unlit == null) log.Add("Không tìm thấy material Sprites-Default");
        foreach (var (roomName, R, layers) in Rooms.Concat(MazeAndBoss()))
        {
            var bg = new GameObject("BG_" + roomName);
            bg.transform.SetParent(root.transform, false);
            Vector2 C = R.center;
            foreach (var L in layers)
            {
                var sp = Prepare(SrcDir + L.img + ".png", log);
                if (sp == null) continue;
                float k = (D + L.z) / D;
                var R2 = L.area ?? R;                       // vùng lớp này phủ (mặc định cả phòng)
                Vector2 C2 = L.area.HasValue ? L.area.Value.center : C;
                var grp = new GameObject($"Layer_{L.img}_z{L.z:0.#}");
                grp.transform.SetParent(bg.transform, false);
                grp.transform.position = new Vector3(C2.x, C2.y, L.z);
                grp.transform.localScale = new Vector3(k, k, 1);

                float s = L.height / sp.bounds.size.y, w = sp.bounds.size.x * s;
                float y = L.yBottom.HasValue ? L.yBottom.Value + L.height / 2f : C2.y;
                var xs = new List<float>();
                if (L.gap <= 0)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(R2.width / w - 0.01f));
                    float start = C2.x - n * w / 2f;
                    for (int i = 0; i < n; i++) xs.Add(start + w * (i + 0.5f));
                }
                else
                    for (float x = R2.xMin + L.x0; x + w <= R2.xMax + 0.01f; x += w + L.gap) xs.Add(x + w / 2f);

                int rows = L.tileV ? Mathf.Max(1, Mathf.CeilToInt((R2.yMax - (y - L.height / 2f)) / L.height - 0.01f)) : 1;
                for (int row = 0; row < rows; row++)
                for (int i = 0; i < xs.Count; i++)
                {
                    var go = new GameObject($"{L.img}_{row}_{i}");
                    go.transform.SetParent(grp.transform, false);
                    go.transform.localPosition = new Vector3(xs[i] - C2.x, y + row * L.height - C2.y, 0);   // vị trí trong "không gian phòng", nhóm cha lo phóng to
                    go.transform.localScale = new Vector3(s, s, 1);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp; sr.sortingOrder = L.order; sr.flipX = i % 2 == 1; sr.flipY = row % 2 == 1;   // lật gương xen kẽ → mép luôn khớp
                    if (unlit != null) sr.sharedMaterial = unlit;   // URP 2D: material mặc định là Sprite-Lit (đen khi không có Light2D) → dùng Sprites-Default như phần còn lại của tầng 5
                    sr.color = L.tint ?? new Color(L.light, L.light, L.light, 1);
                    sprites++;
                }
            }

            // Gán vào Room.background (chỉ khi ô đang trống hoặc đang trỏ tới nền cũ của tool này)
            var room = rooms.FirstOrDefault(r => r.name == roomName);
            if (room == null) { log.Add($"Không thấy phòng {roomName}: chưa gán nền"); continue; }
            var so = new SerializedObject(room);
            var p = so.FindProperty("background");
            if (p == null) { log.Add("Room.cs chưa có trường background"); continue; }
            var cur = p.objectReferenceValue as GameObject;
            // Map3 (R10) và Map4 (R20) được thay bằng ảnh mới theo yêu cầu ngày 16/11/2026 → được ghi đè.
            bool replaceable = cur != null && (cur.name == "Landmark_Map3_R10" || cur.name == "Landmark_Map4_R20");
            if (replaceable) log.Add($"{roomName}: thay '{cur.name}' bằng {bg.name}");
            if (cur != null && cur != bg && !cur.name.StartsWith("BG_F5_") && !replaceable) { log.Add($"{roomName}: ô Background đang là '{cur.name}', KHÔNG ghi đè"); continue; }
            p.objectReferenceValue = bg; so.ApplyModifiedPropertiesWithoutUndo();
            log.Add($"{roomName}.background = {bg.name}");

            // Báo cấu hình camera (không sửa)
            var vso = so.FindProperty("vcam").objectReferenceValue as Cinemachine.CinemachineVirtualCamera;
            if (vso != null)
            {
                var ft = vso.GetCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
                var lens = vso.m_Lens;
                log.Add($"{vso.name}: ModeOverride={lens.ModeOverride} FOV={lens.FieldOfView:0.#} Ortho={lens.OrthographicSize:0.#} Distance={(ft != null ? ft.m_CameraDistance.ToString("0.#") : "?")}");
            }
        }

        // Ảnh nền cũ Map3 (R10), Map4 (R20) vẽ đè lên các lớp mới → TẮT (không xóa). Muốn bật lại: tick lại object này.
        foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            if ((t.name == "Landmark_Map3_R10" || t.name == "Landmark_Map4_R20") && t.gameObject.activeSelf) { t.gameObject.SetActive(false); log.Add("Tắt " + t.name); }

        EditorSceneManager.MarkSceneDirty(scene);
        if (opened) { EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true); log.Add("Đã lưu và đóng FifthFloorScene"); }
        else log.Add("Scene đang mở: chưa lưu, kiểm tra rồi Ctrl+S");
        Debug.Log($"[F5Scenes] {sprites} ảnh nền. " + string.Join(" | ", log));
    }

    static Sprite Prepare(string path, List<string> log)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { log.Add("Không có " + path); return null; }
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        if (set.textureType != TextureImporterType.Sprite || set.spritePixelsPerUnit != PPU || set.spriteMode != (int)SpriteImportMode.Single
            || set.filterMode != FilterMode.Point || set.spriteAlignment != (int)SpriteAlignment.Center || ti.textureCompression != TextureImporterCompression.Uncompressed)
        {
            set.textureType = TextureImporterType.Sprite; set.spriteMode = (int)SpriteImportMode.Single;
            set.spritePixelsPerUnit = PPU; set.filterMode = FilterMode.Point; set.mipmapEnabled = false;
            set.alphaIsTransparency = true; set.spriteAlignment = (int)SpriteAlignment.Center;
            set.wrapMode = TextureWrapMode.Clamp;
            ti.SetTextureSettings(set);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
