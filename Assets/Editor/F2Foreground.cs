using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Lớp tiền cảnh (parallax trước tilemap) cho SecondFloorScene_v2.
//  - Ảnh: Assets/BackGround/SecondFloor/Foreground/F2FG_*.png (bóng đen rừng, nền trong suốt).
//  - Mỗi phòng: object "Foreground" làm con của object nền (ô Background của Room) → chỉ hiện khi đứng trong phòng đó.
//  - Z âm (gần camera hơn tile) nên trôi nhanh hơn mặt đất. Vẽ trên cùng (sorting layer cao nhất, order 100).
//  - Không đặt đè lên lỗ trần/sàn, vật đứng lẻ tránh xa cửa/hầm/vệt sáng.
// Chạy lại được: xóa "Foreground" cũ của từng phòng rồi đặt lại.
public static class F2Foreground
{
    const string ScenePath = "Assets/Scenes/SecondFloorScene_v2.unity";
    const string Dir = "Assets/BackGround/SecondFloor/Foreground/";
    const float PPU = 80f;            // 5 px ảnh = 1 pixel gốc = 1/16 ô
    const int Order = 100;

    // strip: G = cỏ dưới đáy, V = dây leo trên trần; props: vật đứng lẻ theo thứ tự ưu tiên
    static readonly Dictionary<string, (RectInt[] rects, string strips, string[] props)> Spec = new Dictionary<string, (RectInt[], string, string[])>
    {
        { "F2_R01", (new[] { new RectInt(0, -100, 144, 20) }, "GV", new[] { "DeadTree", "Bush", "Mushrooms", "BigLeaves" }) },
        { "F2_R04", (new[] { new RectInt(108, -80, 24, 40) }, "V", new[] { "Pillar" }) },
        { "F2_R05", (new[] { new RectInt(144, -110, 54, 40) }, "G", new[] { "DeadTree", "Fern" }) },
        { "F2_R06", (new[] { new RectInt(162, -70, 36, 20) }, "V", new[] { "Pillar" }) },
        { "F2_R07", (new[] { new RectInt(198, -100, 54, 20) }, "GV", new[] { "Log", "Mushrooms" }) },
        { "F2_R08", (new[] { new RectInt(216, -80, 24, 40) }, "V", new[] { "RootArch" }) },
        { "F2_R09", (new[] { new RectInt(54, -120, 90, 20) }, "GV", new[] { "Thorns", "Bush", "BigLeaves" }) },
        { "F2_R10", (new[] { new RectInt(252, -100, 36, 20) }, "V", new[] { "Pillar" }) },
        { "F2_R11", (new[] { new RectInt(288, -110, 54, 40) }, "G", new[] { "DeadTree" }) },
        { "F2_R13", (new[] { new RectInt(144, -130, 54, 20), new RectInt(198, -130, 54, 30) }, "GV", new[] { "Log", "Mushrooms", "Fern" }) },
        { "F2_R14", (new[] { new RectInt(36, -80, 72, 20) }, "V", new[] { "Fern" }) },
        { "F2_R15", (new[] { new RectInt(240, -80, 48, 20) }, "G", new[] { "Mushrooms", "Bush" }) },
        { "F2_R16", (new[] { new RectInt(342, -110, 24, 40) }, "V", new[] { "Thorns" }) },
    };

    // tên → (tỉ lệ, z, pivot đáy? (false = đỉnh))
    static readonly Dictionary<string, (float scale, float z)> Props = new Dictionary<string, (float, float)>
    {
        { "DeadTree", (0.55f, -5.5f) }, { "RootArch", (0.7f, -5f) }, { "Pillar", (0.6f, -5f) },
        { "Bush", (0.7f, -4.5f) }, { "Fern", (0.7f, -4.5f) }, { "Mushrooms", (0.6f, -4.5f) },
        { "Log", (0.7f, -4.5f) }, { "Thorns", (0.7f, -4.5f) }, { "BigLeaves", (0.45f, -7f) },
    };
    const float StripZ = -4f, GrassScale = 0.75f, VineScale = 0.6f;

    static Tilemap ground, swamp;
    static readonly List<(float x0, float x1, float y)> holes = new List<(float, float, float)>();   // lỗ trần/sàn
    static readonly List<(float x, float y)> doors = new List<(float, float)>();                      // cửa ngang

    [MenuItem("Tools/Culinaria/F2 Foreground Parallax")]
    static void Run()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
        var sprites = PrepareSprites();
        var roots = scene.GetRootGameObjects();
        ground = swamp = null;
        foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (tm.name == "F2_Ground") ground = tm;
            if (tm.name == "F2_Swamp") swamp = tm;
        }
        holes.Clear(); doors.Clear();
        var gateRoot = roots.FirstOrDefault(g => g.name == "______RoomGate_____");
        if (gateRoot != null)
            foreach (Transform t in gateRoot.transform)
            {
                if (!t.name.StartsWith("Gate_")) continue;
                var bc = t.GetComponent<BoxCollider2D>(); if (bc == null) continue;
                Vector2 p = t.position;
                if (bc.size.x > bc.size.y) holes.Add((p.x - bc.size.x / 2f, p.x + bc.size.x / 2f, p.y));
                else doors.Add((p.x, p.y));
            }

        int topLayer = SortingLayer.layers.Last().id;
        var rooms = roots.SelectMany(r => r.GetComponentsInChildren<Room>(true)).ToDictionary(r => r.name, r => r);
        var log = new List<string>();
        foreach (var kv in Spec)
        {
            if (!rooms.TryGetValue(kv.Key, out var room)) { log.Add(kv.Key + ": không thấy phòng"); continue; }
            var bg = new SerializedObject(room).FindProperty("background")?.objectReferenceValue as GameObject;
            Transform parent = bg != null ? bg.transform : room.transform;
            foreach (var old in parent.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Foreground").ToList())
                Undo.DestroyObjectImmediate(old.gameObject);
            var fg = new GameObject("Foreground");
            Undo.RegisterCreatedObjectUndo(fg, "Foreground");
            SceneManager.MoveGameObjectToScene(fg, scene);
            fg.transform.SetParent(parent, false);
            fg.transform.position = Vector3.zero;
            var ls = parent.lossyScale; fg.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f);

            var (rects, strips, props) = kv.Value;
            int n = 0;
            foreach (var r in rects)
            {
                if (strips.Contains('G')) n += Strip(fg.transform, sprites["GrassStrip"], r, true, topLayer);
                if (strips.Contains('V')) n += Strip(fg.transform, sprites["VineStrip"], r, false, topLayer);
            }
            n += PlaceProps(fg.transform, sprites, rects, props, topLayer);
            log.Add($"{kv.Key}:{n}{(bg == null ? "(không có nền, gắn vào phòng)" : "")}");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[F2FG] " + string.Join(" | ", log));
        if (opened) EditorSceneManager.CloseScene(scene, true);
    }

    static Dictionary<string, Sprite> PrepareSprites()
    {
        var res = new Dictionary<string, Sprite>();
        foreach (var name in Props.Keys.Concat(new[] { "GrassStrip", "VineStrip" }))
        {
            string path = Dir + "F2FG_" + name + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogError("[F2FG] thiếu " + path); continue; }
            var pivot = name == "VineStrip" ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.textureType = TextureImporterType.Sprite;
            st.spriteMode = (int)SpriteImportMode.Single;
            st.spriteAlignment = (int)SpriteAlignment.Custom;
            st.spritePivot = pivot;
            st.spritePixelsPerUnit = PPU;
            st.filterMode = FilterMode.Point;
            st.mipmapEnabled = false;
            st.alphaIsTransparency = true;
            st.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(st);
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 4096;
            imp.SaveAndReimport();
            res[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return res;
    }

    static bool Solid(int x, int y) => ground.HasTile(new Vector3Int(x, y, 0));
    static bool Wet(int x, int y) => swamp != null && swamp.HasTile(new Vector3Int(x, y, 0));

    // khoảng không khí lớn nhất của cột x trong r → sàn (lo) và trần (hi+1)
    static bool AirRun(int x, RectInt r, out int lo, out int hi)
    {
        lo = 0; hi = -1; int start = int.MinValue;
        for (int y = r.yMin; y <= r.yMax; y++)
        {
            bool air = y < r.yMax && !Solid(x, y) && !Wet(x, y);
            if (air && start == int.MinValue) start = y;
            if (!air && start != int.MinValue) { if (y - start > hi - lo + 1) { lo = start; hi = y - 1; } start = int.MinValue; }
        }
        return hi >= lo;
    }

    static float Median(List<int> v) { if (v.Count == 0) return float.NaN; v.Sort(); return v[v.Count / 2]; }

    static SpriteRenderer Put(Transform p, Sprite s, Vector3 pos, float scale, bool flip, int layer, int order, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.flipX = flip;
        sr.sortingLayerID = layer; sr.sortingOrder = order;
        return sr;
    }

    // dải cỏ/dây leo phủ hết bề ngang phòng, mỗi đoạn bám theo sàn/trần riêng; bỏ đoạn đè lên lỗ trần/sàn
    static int Strip(Transform p, Sprite s, RectInt r, bool grass, int layer)
    {
        if (s == null) return 0;
        float scale = grass ? GrassScale : VineScale;
        float w = s.bounds.size.x * scale;
        int n = Mathf.Max(1, Mathf.CeilToInt(r.width / (w * 0.92f)));
        float step = r.width / (float)n;
        int placed = 0;
        for (int i = 0; i < n; i++)
        {
            float x0 = r.xMin + i * step, x1 = x0 + step, cx = (x0 + x1) / 2f;
            var ys = new List<int>();
            for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
                if (AirRun(x, r, out var lo, out var hi)) ys.Add(grass ? lo : hi + 1);
            float y = Median(ys);
            if (float.IsNaN(y)) continue;
            if (holes.Any(h => Mathf.Abs(h.y - y) < 2.5f && h.x1 > x0 - 2f && h.x0 < x1 + 2f)) continue;
            // cỏ: ngọn cao nhất ở sàn + 1,2 ô (vì Z âm, khi camera ở trên thì hiện ra ngay dưới mặt sàn); dây leo: đỉnh ở trần
            float h = s.bounds.size.y * scale;
            // đoạn có vũng đầm lầy: hạ cỏ xuống để không che mặt nước (vũng là DeadZone, người chơi phải nhìn thấy)
            int yi = Mathf.RoundToInt(y);
            bool wet = grass && Enumerable.Range(Mathf.FloorToInt(x0), Mathf.Max(1, Mathf.CeilToInt(x1) - Mathf.FloorToInt(x0)))
                .Any(xx => Enumerable.Range(yi - 4, 6).Any(yy => Wet(xx, yy)));
            var pos = new Vector3(cx, grass ? y + (wet ? -1.2f : 1.2f) - h : y + 0.2f, StripZ);
            Put(p, s, pos, scale, i % 2 == 1, layer, Order, (grass ? "Grass_" : "Vines_") + placed);
            placed++;
        }
        return placed;
    }

    // vật đứng lẻ: chọn chỗ trống xa cửa/hầm/lỗ, trải đều theo bề ngang
    static int PlaceProps(Transform p, Dictionary<string, Sprite> sprites, RectInt[] rects, string[] props, int layer)
    {
        var cands = new List<(float x, float floor, RectInt r)>();
        foreach (var r in rects)
            for (int x = r.xMin + 3; x < r.xMax - 3; x += 2)
            {
                if (!AirRun(x, r, out var lo, out var hi)) continue;
                if (hi - lo < 5) continue;
                if (doors.Any(d => Mathf.Abs(d.x - x) < 10f && d.y > r.yMin - 1 && d.y < r.yMax + 1)) continue;
                if (holes.Any(h => x > h.x0 - 8f && x < h.x1 + 8f && h.y >= r.yMin - 1 && h.y <= r.yMax + 1)) continue;
                cands.Add((x + 0.5f, lo, r));
            }
        int placed = 0;
        var used = new List<float>();
        foreach (var name in props)
        {
            if (!sprites.TryGetValue(name, out var s) || s == null || cands.Count == 0) continue;
            (float x, float floor, RectInt r) best = cands[0]; float bestD = -1f;
            if (name == "BigLeaves") { best = cands.OrderBy(c => c.x).First(); bestD = 1f; }
            else
                foreach (var c in cands)
                {
                    float d = used.Count == 0 ? Mathf.Min(c.x - rects.Min(rr => rr.xMin), rects.Max(rr => rr.xMax) - c.x) * 0.5f
                                              : used.Min(u => Mathf.Abs(u - c.x));
                    if (d > bestD) { bestD = d; best = c; }
                }
            if (used.Count > 0 && bestD < 6f && name != "BigLeaves") continue;
            var (scale, z) = Props[name];
            float yb = best.floor + (name == "BigLeaves" ? -1.5f : 0.4f);
            float x = name == "BigLeaves" ? rects.Min(rr => rr.xMin) + 2f : best.x;
            Put(p, s, new Vector3(x, yb, z), scale, (placed % 2) == 1, layer, Order + 1, name + "_" + placed);
            used.Add(x);
            placed++;
        }
        return placed;
    }
}
