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

/// Hiệu ứng môi trường PixelFX cho FourthFloorScene (skill culinaria-map-fx, mẫu F3MapFX).
/// Tạo prefab còn thiếu (ripples, whirlpool, fish-school, jellyfish, wisp) trong Assets/Prefab/pixelFX
/// — wisp và jellyfish có con "Light" (Light2D điểm, teal) — rồi đặt vào con "MapFX" của từng phòng.
/// Phòng nhận theo id KHUNG GỐC (F4RoomFrames.RoomRects) nên đổi tên phòng không sao.
/// Không đụng RoomGlow, collider phòng, EnemyWall, camera. Chạy lại được (MapFX được tạo lại).
/// Chạy: Tools > Culinaria > F4 Map FX (prefab + đặt vào scene).
public static class F4MapFX
{
    const string Sheets = "Assets/Resouces/pixelfx 1/pixelfx-main/sheets/";
    const string AnimDir = "Assets/Animation/PixelFX/";
    const string PrefabDir = "Assets/Prefab/pixelFX/";
    const string ScenePath = "Assets/Scenes/FourthFloorScene.unity";
    const float PPU = 100f;          // giống waterfall.png
    const float PixelScale = 6.25f;  // 1 pixel của pack = 1 pixel tile 16px
    const float D = 18.7f;           // khoảng cách camera
    static readonly Color Teal = new Color(0.35f, 1f, 0.9f);

    // tên, nhóm, hệ số kích thước, loop, thứ tự vẽ (cộng vào order của waterfall), bắt đầu ngẫu nhiên, đèn
    static readonly (string name, string group, float size, bool loop, int order, bool rand, bool light)[] Fx =
    {
        ("ripples", "water", 0.6f, true, 1, true, false),
        ("whirlpool", "water", 0.7f, true, 1, false, true),
        ("fish-school", "critters", 1f, true, 0, true, false),
        ("jellyfish", "critters", 0.5f, true, 1, true, true),
        ("wisp", "critters", 0.6f, true, 2, true, true),
        // đã có từ tầng 2/3 — chỉ dựng nếu thiếu
        ("bubbles", "water", 1f, true, 1, true, false),
        ("bubbles-clear", "water", 1f, true, 1, true, false),   // bọt không có cột nước và vệt mặt nước (dùng dưới biển)
        ("drip", "water", 1f, true, 1, true, false),
        ("loot-beam", "pickups", 0.6f, true, 1, false, false),
        ("key-glint", "pickups", 0.5f, true, 2, false, false),
        ("respawn-beam", "spawn", 0.7f, false, 1, false, false),
        ("spawn-portal", "spawn", 0.6f, false, 1, false, false),
    };

    [Serializable] class JRect { public int x, y, w, h; }
    [Serializable] class JFrame { public string filename; public JRect frame; public int duration; }
    [Serializable] class JPfx { public int fps; public bool loop; public int[] pivot; public int[] frameSize; }
    [Serializable] class JMeta { public JPfx pixelfx; }
    [Serializable] class JRoot { public JFrame[] frames; public JMeta meta; }

    [MenuItem("Tools/Culinaria/F4 Map FX (prefab + đặt vào scene)")]
    static void Run()
    {
        var made = new List<string>();
        foreach (var f in Fx)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + f.name + ".prefab") == null && BuildPrefab(f)) made.Add(f.name);
        Debug.Log("[F4FX] Prefab mới: " + (made.Count > 0 ? string.Join(", ", made) : "không có (đã có sẵn)"));
        foreach (var f in Fx.Where(x => x.light))
        {
            string pp = PrefabDir + f.name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(pp);
            var psr = root.GetComponent<SpriteRenderer>();
            if (psr != null && psr.sharedMaterial != Unlit) { psr.sharedMaterial = Unlit; PrefabUtility.SaveAsPrefabAsset(root, pp); }
            PrefabUtility.UnloadPrefabContents(root);
        }
        Place();
    }

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    // ---------- prefab ----------
    static bool BuildPrefab((string name, string group, float size, bool loop, int order, bool rand, bool light) f)
    {
        string png = Sheets + f.group + "/" + f.name + ".png";
        string jsonPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), Sheets + f.group + "/" + f.name + ".json");
        if (!File.Exists(jsonPath)) { Debug.LogWarning("[F4FX] chưa có " + jsonPath + " — bỏ qua " + f.name); return false; }
        var j = JsonUtility.FromJson<JRoot>(File.ReadAllText(jsonPath));
        var imp = AssetImporter.GetAtPath(png) as TextureImporter;
        if (imp == null || j?.frames == null || j.frames.Length == 0) { Debug.LogError("[F4FX] lỗi đọc " + png); return false; }

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
        if (sprites.Length == 0) { Debug.LogError("[F4FX] không cắt được sprite " + png); return false; }

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
        if (f.light) sr.sharedMaterial = Unlit;   // sinh vật/xoáy phát sáng: luôn sáng
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        if (f.rand) { var t = FindType("AnimRandomStart"); if (t != null) go.AddComponent(t); }
        float s = PixelScale * f.size;
        go.transform.localScale = new Vector3(s, s, 1);
        if (f.light)
        {
            // Đèn đặt ở tâm phần sáng của sprite (không phải pivot)
            AddLight(go.transform, new Vector3(0f, (j.meta?.pixelfx?.pivot != null ? (fh * 0.5f - (fh - j.meta.pixelfx.pivot[1])) : 0f) / PPU, 0f), s);
        }
        PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + f.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log($"[F4FX] {f.name}: {sprites.Length} khung {fw}x{fh}px, {fps} fps, pivot {pivot}{(f.light ? ", có Light2D" : "")}");
        return true;
    }

    static void AddLight(Transform parent, Vector3 localPos, float parentScale)
    {
        var lt = FindType("UnityEngine.Rendering.Universal.Light2D");
        if (lt == null) { Debug.LogWarning("[F4FX] không có URP Light2D, bỏ đèn"); return; }
        var lg = new GameObject("Light");
        lg.transform.SetParent(parent, false);
        lg.transform.localPosition = localPos;
        lg.transform.localScale = new Vector3(1f / parentScale, 1f / parentScale, 1f);   // bán kính đèn tính theo ô thế giới
        var l = lg.AddComponent(lt);
        var so = new SerializedObject(l);
        void SetF(string p, float v) { var sp = so.FindProperty(p); if (sp != null) sp.floatValue = v; }
        var type = so.FindProperty("m_LightType"); if (type != null) type.intValue = 3;   // Point
        var col = so.FindProperty("m_Color"); if (col != null) col.colorValue = Teal;
        SetF("m_Intensity", 0.7f);
        SetF("m_PointLightInnerRadius", 0.3f);
        SetF("m_PointLightOuterRadius", 2.5f);
        SetF("m_FalloffIntensity", 0.6f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- đặt vào scene ----------
    static Tilemap ground, plat, lake, swim;
    static Scene scene;
    static bool Solid(int x, int y) => ground.HasTile(new Vector3Int(x, y, 0));
    static bool Plat(int x, int y) => plat != null && plat.HasTile(new Vector3Int(x, y, 0));
    static bool Lake(int x, int y) => lake != null && lake.HasTile(new Vector3Int(x, y, 0));
    static bool Swim(int x, int y) => swim != null && swim.HasTile(new Vector3Int(x, y, 0));
    static bool Wet(int x, int y) => Lake(x, y) || Swim(x, y);
    static bool Air(int x, int y) => !Solid(x, y) && !Wet(x, y);

    static void Place()
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = false;
        if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
        var roots = scene.GetRootGameObjects();
        ground = plat = lake = swim = null;
        foreach (var tm in roots.SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            if (tm.name == "F4_Ground") ground = tm;
            if (tm.name == "F4_Platform_OneWay") plat = tm;
            if (tm.name.StartsWith("F4_Water_Lake")) lake = tm;
            if (tm.name.StartsWith("F4_Water_Swim")) swim = tm;
        }
        if (ground == null) { Debug.LogError("[F4FX] không thấy F4_Ground"); return; }

        var rooms = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        var byRect = new Dictionary<string, (Room room, RectInt r)>();
        foreach (var rm in rooms) foreach (var (id, r) in F4RoomFrames.RoomRects(rm)) byRect[id] = (rm, r);
        foreach (var rm in rooms) { var old = rm.transform.Find("MapFX"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
        var fxRoot = new Dictionary<Room, Transform>();
        Transform FX(Room rm)
        {
            if (fxRoot.TryGetValue(rm, out var t)) return t;
            var go = new GameObject("MapFX");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(rm.transform, false);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var ls = rm.transform.lossyScale; go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f);
            return fxRoot[rm] = go.transform;
        }
        Room RoomAt(Vector2 p) => rooms.FirstOrDefault(rm => F4RoomFrames.RoomRects(rm).Any(q => q.r.Contains(Vector2Int.FloorToInt(p))))
                                  ?? rooms.FirstOrDefault(rm => rm.GetComponents<Collider2D>().Any(c => c.OverlapPoint(p)));
        GameObject Obj(string n) => roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n)?.gameObject;

        var count = new Dictionary<string, List<string>>();
        void Add(Room rm, string what, int n)
        {
            if (rm == null || n == 0) return;
            if (!count.TryGetValue(rm.name, out var l)) count[rm.name] = l = new List<string>();
            l.Add(n > 1 ? what + "×" + n : what);
        }
        void Each(string[] ids, Action<Room, RectInt, Transform> act)
        { foreach (var id in ids) if (byRect.TryGetValue(id, out var q)) act(q.room, q.r, FX(q.room)); else Debug.LogWarning("[F4FX] không thấy khung " + id); }

        // --- Khu hồ A–C ---
        Each(new[] { "F4_R01", "F4_R02", "F4_R04", "F4_R05", "F4_R07", "F4_R08", "F4_R09", "F4_R11", "F4_R12", "F4_R14" },
            (rm, r, p) => Add(rm, "wisp", PutWisp(r, 0.55f, p)));
        Each(new[] { "F4_R10", "F4_R12", "F4_R13" }, (rm, r, p) => Add(rm, "ripples", PutRipples(r, 2, p)));
        Each(new[] { "F4_R04", "F4_R08", "F4_R10" }, (rm, r, p) => Add(rm, "drip", PutDrip(r, 2, p)));
        // bọt nước ở chân thác (thác do builder dựng: Waterfall_<Rxx>_k)
        var falls = roots.FirstOrDefault(g => g.name == "______Waterfall_____");
        if (falls != null)
            foreach (Transform w in falls.transform)
            {
                var rm = RoomAt(w.position);
                if (rm == null) continue;
                Add(rm, "bubbles(thác)", PutFallFoam(w, FX(rm)));
            }

        // --- Thành phố chìm D ---
        var sea = new[] { "F4_R15", "F4_R16", "F4_R18", "F4_R19", "F4_R20", "F4_R21", "F4_R22", "F4_R24", "F4_R25" };
        Each(sea, (rm, r, p) => Add(rm, "bubbles", PutSeaBubbles(r, 1, p)));
        Each(sea, (rm, r, p) => Add(rm, "jellyfish", PutJelly(r, 1, p)));
        Each(new[] { "F4_R15", "F4_R16", "F4_R18", "F4_R20", "F4_R24" }, (rm, r, p) => Add(rm, "fish-school", PutFish(r, p)));
        // Phòng boss
        Each(new[] { "F4_R27" }, (rm, r, p) => { Add(rm, "jellyfish", PutJelly(r, 2, p)); Add(rm, "bubbles", PutSeaBubbles(r, 3, p)); Add(rm, "fish-school", PutFish(r, p)); });
        // Dòng nước CUR: xoáy nước giữa ống
        var cur = rooms.FirstOrDefault(x => x.name == "F4_CUR");
        if (cur != null) Add(cur, "whirlpool", PutWhirl(cur, FX(cur))); else Debug.LogWarning("[F4FX] không thấy phòng F4_CUR");

        // --- Lòng cột E ---
        Each(new[] { "F4_R28", "F4_R29" }, (rm, r, p) => Add(rm, "wisp", PutWisp(r, 0.5f, p)));

        // --- Checkpoint, rương ---
        foreach (var cp in new[] { "F4_CP01", "F4_CP02", "F4_CP03", "F4_CP04" })
        {
            var g = Obj(cp); if (g == null) { Debug.LogWarning("[F4FX] không thấy " + cp); continue; }
            var rm = RoomAt(g.transform.position); if (rm == null) { Debug.LogWarning("[F4FX] " + cp + " không nằm trong phòng nào"); continue; }
            Add(rm, "respawn-beam", PutMarker("respawn-beam", g.transform.position, FX(rm)));
        }
        foreach (var ch in new[] { "F4_Chest01", "F4_Chest02", "F4_Chest03", "F4_Chest04", "F4_Chest05" })
        {
            var g = Obj(ch); if (g == null) { Debug.LogWarning("[F4FX] không thấy " + ch); continue; }
            var rm = RoomAt(g.transform.position); if (rm == null) { Debug.LogWarning("[F4FX] " + ch + " không nằm trong phòng nào"); continue; }
            string fx = rm.name.StartsWith("F4_H") || rm.name.StartsWith("F4P_") ? "loot-beam" : "key-glint";      // rương trong phòng/hốc bí mật: cột sáng
            Add(rm, fx + "(" + ch + ")", PutMarker(fx, g.transform.position, FX(rm)));
        }

        // --- Cổng xuống tầng 5: con của cửa trỏ sang FifthFloorScene ---
        string portalAt = null;
        var doors = roots.FirstOrDefault(g => g.name == "______Door_____");
        if (doors != null)
            foreach (Transform d in doors.transform)
            {
                if (d.name == "F4_Down" || d.name.Contains("SpawnPoint")) continue;
                if (d.GetComponent<Collider2D>() == null) continue;                  // cửa còn lại (không phải cửa lên tầng 3) = cửa xuống tầng 5
                var old = d.Find("spawn-portal"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "spawn-portal.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
                go.transform.SetParent(d, true);
                go.transform.position = new Vector3(d.position.x, FloorBelow(d.position), 0f);
                var ls = d.lossyScale; go.transform.localScale = new Vector3(pf.transform.localScale.x / ls.x, pf.transform.localScale.y / ls.y, 1f);
                go.name = "spawn-portal"; portalAt = d.name;
            }
        if (portalAt == null) Debug.LogWarning("[F4FX] không thấy cửa sang tầng 5");

        EditorSceneManager.MarkSceneDirty(scene);
        if (opened) { EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true); }
        else EditorSceneManager.SaveScene(scene);
        Debug.Log("[F4FX] Đặt hiệu ứng: " + string.Join(" | ", count.OrderBy(k => k.Key).Select(k => k.Key + ": " + string.Join(", ", k.Value))) + (portalAt != null ? " | " + portalAt + ": spawn-portal" : ""));
    }

    static float FloorBelow(Vector3 p)
    {
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        int yy = y; while (yy > y - 12 && !Solid(x, yy)) yy--;
        return Solid(x, yy) ? yy + 1 : p.y;
    }

    static Material unlit;
    static Material Unlit => unlit != null ? unlit : (unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat"));

    static GameObject Spawn(string fx, Transform p, Vector3 pos, float extraScale = 1f, string suffix = "", int? order = null)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        if (pf == null) { Debug.LogError("[F4FX] thiếu prefab " + fx); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, scene);
        go.transform.SetParent(p, true);
        go.transform.position = pos;
        go.transform.localScale = pf.transform.localScale * extraScale;
        go.name = fx + suffix;
        var sr = go.GetComponent<SpriteRenderer>();
        if (order.HasValue) sr.sortingOrder = order.Value;
        if (Unlit != null) sr.sharedMaterial = Unlit;   // hiệu ứng tự phát sáng: không phụ thuộc đèn 2D (nền tầng 4 cũng unlit)
        return go;
    }

    static float SpriteBottom(string fx, float extra)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + fx + ".prefab");
        var sr = pf != null ? pf.GetComponent<SpriteRenderer>() : null;
        return sr != null && sr.sprite != null ? sr.sprite.bounds.min.y * pf.transform.localScale.y * extra : 0f;
    }

    // Đốm sáng: lơ lửng 2,5 ô trên sàn khô, ở vị trí t theo bề ngang khung (tránh 4 ô sát mép = cửa)
    static int PutWisp(RectInt r, float t, Transform p)
    {
        int cx = Mathf.RoundToInt(Mathf.Lerp(r.xMin + 4, r.xMax - 5, t));
        for (int d = 0; d < r.width / 2 - 4; d++)
            foreach (int x in new[] { cx + d, cx - d })
            {
                if (x < r.xMin + 4 || x > r.xMax - 5) continue;
                for (int y = r.yMin; y < r.yMax - 5; y++)
                {
                    if (!(Solid(x, y) || Plat(x, y)) || !Air(x, y + 1)) continue;
                    bool open = true;
                    for (int k = 1; k <= 5 && open; k++) open = Air(x, y + k) && Air(x - 1, y + k) && Air(x + 1, y + k);
                    if (!open) continue;
                    return Spawn("wisp", p, new Vector3(x + 0.5f, y + 1 + 2.5f, 0f), 1f, "_" + x, 3) != null ? 1 : 0;
                }
            }
        return 0;
    }

    // Gợn sóng trên mặt hồ: các đoạn mặt nước ≥ 4 ô
    static int PutRipples(RectInt r, int k, Transform p)
    {
        var runs = new List<(int x0, int x1, int y)>();
        for (int y = r.yMin; y < r.yMax; y++)
        {
            int start = int.MinValue;
            for (int x = r.xMin; x <= r.xMax; x++)
            {
                bool top = x < r.xMax && Wet(x, y) && !Wet(x, y + 1) && !Solid(x, y + 1);
                if (top && start == int.MinValue) start = x;
                if (!top && start != int.MinValue) { runs.Add((start, x - 1, y)); start = int.MinValue; }
            }
        }
        int n = 0;
        foreach (var run in runs.Where(q => q.x1 - q.x0 >= 1).OrderByDescending(q => q.x1 - q.x0).Take(k))
        {
            float w = run.x1 + 1 - run.x0, extra = Mathf.Clamp(w / (136f * 0.0625f * 0.6f), 0.4f, 1f);   // không tràn ra ngoài mặt nước
            if (Spawn("ripples", p, new Vector3((run.x0 + run.x1 + 1) / 2f, run.y + 1 - 0.25f, 0f), extra, "_" + run.x0, 5) != null) n++;
        }
        return n;
    }

    // giọt nước: rơi từ trần xuống sàn/bục/mặt nước (khoảng trống 3–10 ô)
    static int PutDrip(RectInt r, int k, Transform p)
    {
        var cands = new List<(int x, int floor, int gap)>();
        for (int x = r.xMin + 3; x < r.xMax - 3; x++)
            for (int y = r.yMax - 1; y > r.yMin; y--)
            {
                if (!Solid(x, y) || Solid(x, y - 1)) continue;
                int yy = y - 1;
                while (yy > r.yMin - 1 && !Solid(x, yy) && !Plat(x, yy) && !Wet(x, yy)) yy--;
                int gap = y - 1 - yy;
                if (gap >= 3 && gap <= 10 && (Solid(x, yy) || Plat(x, yy) || Wet(x, yy))) cands.Add((x, yy + 1, gap));
                break;
            }
        if (cands.Count == 0) { Debug.LogWarning("[F4FX] không tìm được chỗ đặt drip trong " + r); return 0; }
        cands = cands.OrderBy(c => c.x).ToList();
        int placed = 0; var used = new HashSet<int>();
        for (int i = 0; i < k; i++)
        {
            var c = cands[Mathf.Clamp(Mathf.RoundToInt((i + 0.5f) / k * cands.Count - 0.5f), 0, cands.Count - 1)];
            if (!used.Add(c.x)) continue;
            float H = c.gap / (78f / 88f - 0.15f);
            float extra = Mathf.Clamp(H / 5.5f, 0.6f, 2.6f);
            if (Spawn("drip", p, new Vector3(c.x + 0.5f, c.floor, 0), extra, "_" + i) != null) placed++;
        }
        return placed;
    }

    // Bọt ở chân thác: từ đáy hồ dưới thác bốc lên tới mặt nước
    static int PutFallFoam(Transform w, Transform p)
    {
        var sr = w.GetComponent<SpriteRenderer>();
        float bx = sr != null ? sr.bounds.center.x : w.position.x, by = sr != null ? sr.bounds.min.y : w.position.y;
        int x = Mathf.FloorToInt(bx), y = Mathf.FloorToInt(by) + 1;
        int top = y; while (top > y - 12 && !Wet(x, top)) top--;            // mặt nước dưới chân thác
        if (!Wet(x, top)) return 0;
        int bot = top; while (bot > top - 12 && Wet(x, bot - 1)) bot--;      // đáy hồ
        float depth = top + 1 - bot;
        float extra = Mathf.Clamp(depth / 6.5f, 0.35f, 1f);
        float b = bot - SpriteBottom("bubbles", extra);
        return Spawn("bubbles", p, new Vector3(bx, b, 0f), extra, "_fall" + x, 5) != null ? 1 : 0;
    }

    // Bọt dưới biển (bubbles-clear): từ sàn có ít nhất 7 ô nước phía trên. Lệch sang 1/3 trái để không trùng sứa.
    static int PutSeaBubbles(RectInt r, int k, Transform p)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "bubbles-clear.prefab") == null) return 0;
        var cands = new List<(int x, int y)>();
        for (int x = r.xMin + 4; x < r.xMax - 4; x++)
            for (int y = r.yMin; y < r.yMax - 7; y++)
                if (Solid(x, y) && Enumerable.Range(1, 7).All(d => Swim(x, y + d) && !Solid(x, y + d))) { cands.Add((x, y + 1)); break; }
        if (cands.Count == 0) return 0;
        int n = 0; var used = new HashSet<int>();
        for (int i = 0; i < k; i++)
        {
            var c = cands[Mathf.Clamp(Mathf.RoundToInt((i + 0.3f) / k * cands.Count - 0.5f), 0, cands.Count - 1)];
            if (!used.Add(c.x)) continue;
            float b = c.y - SpriteBottom("bubbles-clear", 1f);
            if (Spawn("bubbles-clear", p, new Vector3(c.x + 0.5f, b, 0f), 1f, "_" + c.x, 3) != null) n++;
        }
        return n;
    }

    // Sứa: giữa vùng nước trống (3 ô quanh không có đá)
    static int PutJelly(RectInt r, int k, Transform p)
    {
        var cands = new List<(int x, int y)>();
        for (int x = r.xMin + 5; x < r.xMax - 5; x += 2)
            for (int y = r.yMin + 4; y < r.yMax - 3; y++)
            {
                bool ok = true;
                for (int dx = -2; dx <= 2 && ok; dx++) for (int dy = -3; dy <= 3 && ok; dy++) ok = Swim(x + dx, y + dy) && !Solid(x + dx, y + dy);
                if (ok) { cands.Add((x, y)); break; }
            }
        if (cands.Count == 0) return 0;
        int n = 0;
        for (int i = 0; i < k; i++)
        {
            var c = cands[Mathf.Clamp(Mathf.RoundToInt((i + 0.7f) / k * cands.Count - 0.5f), 0, cands.Count - 1)];
            if (Spawn("jellyfish", p, new Vector3(c.x + 0.5f, c.y + 1f, 0f), 1f, "_" + c.x, 3) != null) n++;
        }
        return n;
    }

    // Đàn cá: lớp nền phía sau (Z dương), nửa trên khung, phóng theo độ sâu để giữ cỡ nhìn
    static int PutFish(RectInt r, Transform p)
    {
        const float z = 4f;
        float k = (D + z) / D;
        return Spawn("fish-school", p, new Vector3(r.center.x, r.yMax - 3.5f, z), k, "_bg", -20) != null ? 1 : 0;
    }

    // Xoáy nước giữa ống nước của CUR
    static int PutWhirl(Room cur, Transform p)
    {
        var cb = cur.transform.Find("CamBounds");
        var c = cb ? cb.GetComponent<Collider2D>() : cur.GetComponent<Collider2D>();
        var b = c.bounds;
        int y = Mathf.FloorToInt(b.center.y);
        int x0 = int.MinValue, x1 = int.MinValue;
        for (int x = Mathf.FloorToInt(b.min.x); x <= Mathf.CeilToInt(b.max.x); x++)
            if (Swim(x, y)) { if (x0 == int.MinValue) x0 = x; x1 = x; }
        if (x0 == int.MinValue) { Debug.LogWarning("[F4FX] CUR không có nước bơi ở giữa"); return 0; }
        float w = x1 + 1 - x0;
        float extra = Mathf.Clamp(w / (128f * 0.0625f * 0.7f), 0.4f, 1.2f);
        return Spawn("whirlpool", p, new Vector3((x0 + x1 + 1) / 2f, b.center.y, 0f), extra, "_cur", 5) != null ? 1 : 0;
    }

    static int PutMarker(string fx, Vector3 m, Transform p) => Spawn(fx, p, new Vector3(m.x, FloorBelow(m), 0), 1f, "_" + Mathf.RoundToInt(m.x)) != null ? 1 : 0;
}
