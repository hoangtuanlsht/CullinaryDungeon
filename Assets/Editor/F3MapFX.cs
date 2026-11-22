using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// Hiệu ứng môi trường PixelFX cho ThirdFloorScene (theo skill culinaria-map-fx, mẫu F2MapFX).
/// Tạo prefab còn thiếu (fog, soul-rise) trong Assets/Prefab/pixelFX rồi đặt vào con "MapFX" của từng phòng.
/// Phòng được nhận theo id KHUNG GỐC (F3RoomFrames.RoomRects) nên đổi tên phòng không sao.
/// Không đụng RoomGlow, collider phòng, EnemyWall, camera. Chạy lại được (MapFX được tạo lại).
/// Chạy: Tools > Culinaria > F3 Map FX (prefab + đặt vào scene).
public static class F3MapFX
{
    const string Sheets = "Assets/Resouces/pixelfx 1/pixelfx-main/sheets/";
    const string AnimDir = "Assets/Animation/PixelFX/";
    const string PrefabDir = "Assets/Prefab/pixelFX/";
    const string ScenePath = "Assets/Scenes/ThirdFloorScene.unity";
    const float PPU = 100f;          // giống waterfall.png
    const float PixelScale = 6.25f;  // 1 pixel của pack = 1 pixel tile 16px
    const float D = 18.7f;           // khoảng cách camera

    // tên, nhóm, hệ số kích thước, loop, thứ tự vẽ (cộng vào order của waterfall), bắt đầu ngẫu nhiên
    static readonly (string name, string group, float size, bool loop, int order, bool rand)[] Fx =
    {
        ("drip", "water", 1f, true, 1, true),
        ("fog", "weather", 1f, true, 1, true),
        ("soul-rise", "spawn", 0.7f, true, 1, true),
        ("loot-beam", "pickups", 0.6f, true, 1, false),
        ("key-glint", "pickups", 0.5f, true, 2, false),
        ("respawn-beam", "spawn", 0.7f, false, 1, false),
        ("spawn-portal", "spawn", 0.6f, false, 1, false),
    };

    [Serializable] class JRect { public int x, y, w, h; }
    [Serializable] class JFrame { public string filename; public JRect frame; public int duration; }
    [Serializable] class JPfx { public int fps; public bool loop; public int[] pivot; public int[] frameSize; }
    [Serializable] class JMeta { public JPfx pixelfx; }
    [Serializable] class JRoot { public JFrame[] frames; public JMeta meta; }

    [MenuItem("Tools/Culinaria/F3 Map FX (prefab + đặt vào scene)")]
    static void Run()
    {
        var made = new List<string>();
        foreach (var f in Fx)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + f.name + ".prefab") == null && BuildPrefab(f)) made.Add(f.name);
        Debug.Log("[F3FX] Prefab mới: " + (made.Count > 0 ? string.Join(", ", made) : "không có (đã có sẵn)"));
        Place();
    }

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    // ---------- prefab (giống F2MapFX) ----------
    static bool BuildPrefab((string name, string group, float size, bool loop, int order, bool rand) f)
    {
        string png = Sheets + f.group + "/" + f.name + ".png";
        string jsonPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), Sheets + f.group + "/" + f.name + ".json");
        if (!File.Exists(jsonPath)) { Debug.LogError("[F3FX] thiếu " + jsonPath); return false; }
        var j = JsonUtility.FromJson<JRoot>(File.ReadAllText(jsonPath));
        var imp = AssetImporter.GetAtPath(png) as TextureImporter;
        if (imp == null || j?.frames == null || j.frames.Length == 0) { Debug.LogError("[F3FX] lỗi đọc " + png); return false; }

        int texH = 0;
        foreach (var fr in j.frames) texH = Mathf.Max(texH, fr.frame.y + fr.frame.h);
        int fw = j.frames[0].frame.w, fh = j.frames[0].frame.h;
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        if (j.meta?.pixelfx?.pivot != null && j.meta.pixelfx.pivot.Length == 2)
            pivot = new Vector2(j.meta.pixelfx.pivot[0] / (float)fw, 1f - j.meta.pixelfx.pivot[1] / (float)fh);

        var metas = new SpriteMetaData[j.frames.Length];
        for (int i = 0; i < j.frames.Length; i++)
        {
            var r = j.frames[i].frame;
            metas[i] = new SpriteMetaData { name = f.name + "_" + i, rect = new Rect(r.x, texH - r.y - r.h, r.w, r.h), alignment = (int)SpriteAlignment.Custom, pivot = pivot };
        }
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spritePixelsPerUnit = PPU;
        imp.maxTextureSize = 8192;
#pragma warning disable 618
        imp.spritesheet = metas;
#pragma warning restore 618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>()
            .Select(s => (s, idx: int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out var k) ? k : -1))
            .Where(t => t.idx >= 0).OrderBy(t => t.idx).Select(t => t.s).ToArray();
        if (sprites.Length == 0) { Debug.LogError("[F3FX] không cắt được sprite " + png); return false; }

        Directory.CreateDirectory(AnimDir); Directory.CreateDirectory(PrefabDir);
        int fps = j.meta?.pixelfx != null && j.meta.pixelfx.fps > 0 ? j.meta.pixelfx.fps : 30;
        var clip = new AnimationClip { frameRate = fps };
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / (float)fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / (float)fps, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = f.loop; AnimationUtility.SetAnimationClipSettings(clip, st);
        string clipPath = AnimDir + f.name + ".anim";
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);
        string ctrlPath = AnimDir + f.name + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);

        var go = new GameObject(f.name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0];
        var wf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "waterfall_0.prefab");
        var wsr = wf != null ? wf.GetComponentInChildren<SpriteRenderer>() : null;
        if (wsr != null) { sr.sortingLayerID = wsr.sortingLayerID; sr.sortingOrder = wsr.sortingOrder + f.order; sr.sharedMaterial = wsr.sharedMaterial; }
        else sr.sortingOrder = f.order;
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        if (f.rand) { var t = FindType("AnimRandomStart"); if (t != null) go.AddComponent(t); }
        float s = PixelScale * f.size;
        go.transform.localScale = new Vector3(s, s, 1);
        PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + f.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log($"[F3FX] {f.name}: {sprites.Length} khung {fw}x{fh}px, {fps} fps, pivot {pivot}");
        return true;
    }

    // ---------- đặt vào scene ----------
    static Tilemap ground, kGround, lava, plat;
    static bool Plat(int x, int y) => plat != null && plat.HasTile(new Vector3Int(x, y, 0));
    static Scene scene;

    static void Place()
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var roots = scene.GetRootGameObjects();
        foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (tm.name == "F3_Ground") ground = tm;
            if (tm.name == "F3_Ground_KnightDungeon") kGround = tm;
            if (tm.name == "F3_DeadZone_Lava") lava = tm;
            if (tm.name == "F3_Platform_OneWay") plat = tm;
        }
        if (ground == null) { Debug.LogError("[F3FX] không thấy F3_Ground"); return; }

        // id khung gốc → (Room, các khung của Room đó)
        var rooms = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        var byRect = new Dictionary<string, (Room room, RectInt r)>();
        foreach (var rm in rooms) foreach (var (id, r) in F3RoomFrames.RoomRects(rm)) byRect[id] = (rm, r);
        var fxRoot = new Dictionary<Room, Transform>();
        Transform FX(Room rm)
        {
            if (fxRoot.TryGetValue(rm, out var t)) return t;
            var old = rm.transform.Find("MapFX");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("MapFX");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(rm.transform, false);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var ls = rm.transform.lossyScale; go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f);
            return fxRoot[rm] = go.transform;
        }
        Room RoomAt(Vector2 p) => rooms.FirstOrDefault(rm => F3RoomFrames.RoomRects(rm).Any(q => q.r.Contains(Vector2Int.FloorToInt(p))));
        GameObject Obj(string n) => roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n)?.gameObject;

        var count = new Dictionary<string, int>();
        void Add(Room rm, int n) { if (rm == null || n == 0) return; count.TryGetValue(rm.name, out var c); count[rm.name] = c + n; }

        // Tạo trước MapFX cho mọi phòng sẽ có hiệu ứng (để chạy lại không sót bản cũ)
        foreach (var rm in rooms) { var old = rm.transform.Find("MapFX"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }

        // giọt nước dưới nhũ đá
        void Drip(string[] ids, int k) { var rs = ids.Where(byRect.ContainsKey).Select(i => byRect[i]).ToList(); if (rs.Count == 0) return; var rm = rs[0].room; Add(rm, PutDrip(rs.Select(x => x.r).ToArray(), k, FX(rm))); }
        Drip(new[] { "F3_R03", "F3_R04", "F3_R05", "F3_R06" }, 3);
        Drip(new[] { "F3_R07" }, 3);
        Drip(new[] { "F3_R09", "F3_R10" }, 2);
        Drip(new[] { "F3_R11", "F3_R12" }, 2);

        // sương trên hố dung nham + sát sàn phòng boss, hầm mộ nhện
        foreach (var id in new[] { "F3_R05", "F3_R10" })
            if (byRect.TryGetValue(id, out var q)) Add(q.room, PutLavaFog(q.r, FX(q.room)));
        foreach (var id in new[] { "F3_R18", "F3_R19" })
            if (byRect.TryGetValue(id, out var q)) Add(q.room, PutFloorFog(q.r, FX(q.room)));

        // hồn bốc lên ở hành lang kỵ sĩ và hành lang đá
        foreach (var (id, t) in new[] { ("F3_R14", 0.55f), ("F3_R15", 0.3f), ("F3_R22", 0.6f), ("F3_R24", 0.25f) })
            if (byRect.TryGetValue(id, out var q)) Add(q.room, PutOnFloor("soul-rise", q.r, t, FX(q.room)));

        // checkpoint, rương
        foreach (var (obj, fx) in new[] { ("F3_CP01", "respawn-beam"), ("F3_CP02", "respawn-beam"), ("F3_CP03", "respawn-beam"),
                                          ("F3_Chest02", "loot-beam"), ("F3_Chest04", "loot-beam"), ("F3_Chest06", "loot-beam"),
                                          ("F3_Chest03", "key-glint"), ("F3_Chest05", "key-glint") })
        {
            var g = Obj(obj);
            if (g == null) { Debug.LogWarning("[F3FX] không thấy " + obj); continue; }
            var rm = RoomAt(g.transform.position);
            if (rm == null) { Debug.LogWarning("[F3FX] " + obj + " không nằm trong phòng nào"); continue; }
            Add(rm, PutMarker(fx, g.transform.position, FX(rm)));
        }

        // cổng lên tầng 4: con của cửa F3_Up (cửa chỉ bật sau khi hạ boss → cổng hiện theo)
        var up = Obj("F3_Up");
        if (up != null)
        {
            var old = up.transform.Find("spawn-portal");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "spawn-portal.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
            go.transform.SetParent(up.transform, true);
            go.transform.position = new Vector3(up.transform.position.x, FloorBelow(up.transform.position), 0f);
            var ls = up.transform.lossyScale;
            go.transform.localScale = new Vector3(pf.transform.localScale.x / ls.x, pf.transform.localScale.y / ls.y, 1f);
            go.name = "spawn-portal";
        }
        else Debug.LogWarning("[F3FX] không thấy cửa F3_Up");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[F3FX] Đặt hiệu ứng: " + string.Join(" | ", count.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value)) + (up != null ? " | F3_Up: spawn-portal" : ""));
    }

    static bool Solid(int x, int y) { var p = new Vector3Int(x, y, 0); return ground.HasTile(p) || (kGround != null && kGround.HasTile(p)); }
    static bool Lava(int x, int y) => lava != null && lava.HasTile(new Vector3Int(x, y, 0));

    static float FloorBelow(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        int yy = y; while (yy > y - 12 && !Solid(x, yy)) yy--;
        return Solid(x, yy) ? yy + 1 : p.y;
    }

    static GameObject Spawn(string fx, Transform p, Vector3 pos, float extraScale = 1f, string suffix = "")
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        if (pf == null) { Debug.LogError("[F3FX] thiếu prefab " + fx); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
        go.transform.SetParent(p, true);
        go.transform.position = pos;
        go.transform.localScale = pf.transform.localScale * extraScale;
        go.name = fx + suffix;
        return go;
    }

    // Đặt sao cho mép dưới sprite (frame 0) nằm ở y = bottom
    static GameObject SpawnBottom(string fx, Transform p, float cx, float bottom, float z, float extra, string suffix)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        var sr = pf != null ? pf.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null) return null;
        float sy = pf.transform.localScale.y * extra;
        return Spawn(fx, p, new Vector3(cx, bottom - sr.sprite.bounds.min.y * sy, z), extra, suffix);
    }

    static float SpriteWidth(string fx)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        var sr = pf != null ? pf.GetComponent<SpriteRenderer>() : null;
        return sr != null && sr.sprite != null ? sr.sprite.bounds.size.x * pf.transform.localScale.x : 20f;
    }

    // Sương trên các đoạn mặt dung nham trong khung (phủ bề ngang đoạn, lùi sau tile một chút)
    static int PutLavaFog(RectInt r, Transform p)
    {
        if (lava == null) return 0;
        var runs = new List<(int x0, int x1, int y)>();
        for (int y = r.yMin; y < r.yMax; y++)
        {
            int start = int.MinValue;
            for (int x = r.xMin; x <= r.xMax; x++)
            {
                bool top = x < r.xMax && Lava(x, y) && !Lava(x, y + 1) && !Solid(x, y + 1);
                if (top && start == int.MinValue) start = x;
                if (!top && start != int.MinValue) { runs.Add((start, x - 1, y)); start = int.MinValue; }
            }
        }
        int n = 0;
        foreach (var run in runs.Where(q => q.x1 - q.x0 >= 3))
            n += FogStrip(run.x0, run.x1 + 1, run.y + 1, p, n, new Color(1f, 0.55f, 0.4f, 0.6f));   // khói ấm ánh dung nham
        return n;
    }

    // Sương sát sàn dọc phòng (sàn = ô trống thấp nhất ở giữa phòng)
    static int PutFloorFog(RectInt r, Transform p)
    {
        int x = r.xMin + r.width / 2, y = r.yMin;
        while (y < r.yMax && Solid(x, y)) y++;
        return FogStrip(r.xMin + 2, r.xMax - 2, y, p, 0, new Color(0.75f, 0.85f, 1f, 0.7f));
    }

    static int FogStrip(float x0, float x1, float floorY, Transform p, int idx, Color tint)
    {
        const float z = 2f;
        float depth = (D + z) / D;
        float w = SpriteWidth("fog") * depth;
        int k = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / (w * 0.85f)));
        float extra = Mathf.Clamp((x1 - x0) / (k * w * 0.85f), 0.5f, 1.3f) * depth;
        int n = 0;
        for (int i = 0; i < k; i++)
        {
            float cx = x0 + (i + 0.5f) * (x1 - x0) / k;
            var go = SpawnBottom("fog", p, cx, floorY - 0.6f, z, extra, "_" + (idx + n));
            if (go != null) { go.GetComponent<SpriteRenderer>().color = tint; n++; }
        }
        return n;
    }

    // Đặt trên sàn tại vị trí t (0..1) theo bề ngang khung
    static int PutOnFloor(string fx, RectInt r, float t, Transform p)
    {
        int x = Mathf.RoundToInt(Mathf.Lerp(r.xMin + 2, r.xMax - 3, t));
        int y = r.yMin + r.height / 2;
        while (y > r.yMin && !Solid(x, y - 1)) y--;
        while (y < r.yMax && Solid(x, y)) y++;
        return SpawnBottom(fx, p, x + 0.5f, y, 0f, 1f, "_" + x) != null ? 1 : 0;
    }

    // giọt nước: rơi từ trần xuống sàn/bục (khoảng trống 3–10 ô)
    static int PutDrip(RectInt[] rs, int k, Transform p)
    {
        var cands = new List<(int x, int floor, int gap)>();
        foreach (var r in rs)
            for (int x = r.xMin + 3; x < r.xMax - 3; x++)
                for (int y = r.yMax - 1; y > r.yMin; y--)
                {
                    if (!Solid(x, y) || Solid(x, y - 1)) continue;
                    int yy = y - 1;
                    while (yy > r.yMin - 1 && !Solid(x, yy) && !Plat(x, yy) && !Lava(x, yy)) yy--;   // bục một chiều cũng là chỗ giọt rơi xuống
                    int gap = y - 1 - yy;
                    if (gap >= 3 && gap <= 10 && (Solid(x, yy) || Plat(x, yy))) cands.Add((x, yy + 1, gap));
                    break;
                }
        if (cands.Count == 0) { Debug.LogWarning("[F3FX] không tìm được chỗ đặt drip"); return 0; }
        cands = cands.OrderBy(c => c.x).ToList();
        int placed = 0;
        var used = new HashSet<int>();
        for (int i = 0; i < k; i++)
        {
            var c = cands[Mathf.Clamp(Mathf.RoundToInt((i + 0.5f) / k * cands.Count - 0.5f), 0, cands.Count - 1)];
            if (!used.Add(c.x)) continue;
            // khung 88px = 5.5 đơn vị; giọt bắt đầu ở ~15% khung, chân ở pivot (78/88)
            float H = c.gap / (78f / 88f - 0.15f);
            float extra = Mathf.Clamp(H / 5.5f, 0.6f, 2.6f);
            if (Spawn("drip", p, new Vector3(c.x + 0.5f, c.floor, 0), extra, "_" + i) != null) placed++;
        }
        return placed;
    }

    static int PutMarker(string fx, Vector3 m, Transform p) => Spawn(fx, p, new Vector3(m.x, FloorBelow(m), 0), 1f, "_" + Mathf.RoundToInt(m.x)) != null ? 1 : 0;
}
