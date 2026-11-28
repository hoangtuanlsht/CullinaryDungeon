using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// [Tầng 5] Hiệu ứng bản đồ PixelFX (skill culinaria-map-fx).
///  - Tạo prefab mới nếu chưa có: torch, brazier, campfire (sheet → sprite cắt lưới, .anim lặp, .controller, prefab + Light2D cam ấm).
///  - Đặt vào con "MapFX" của từng Room (hiệu ứng đi theo phòng). Chạy lại được: MapFX được dựng lại mỗi lần.
///  - Đuốc cũ (______Art_F5_____/Torches) KHÔNG bị xóa: chỉ tắt tạm. Đuốc PixelFX mới đặt đúng 12 chỗ đuốc cũ (tên "torch (thay đuốc cũ)").
///    Menu "F5 FX - Dùng đuốc cũ" / "F5 FX - Dùng đuốc mới" để bật qua lại so sánh.
/// Không đụng RoomGlow, collider phòng, EnemyWall, vcam.
public static class F5MapFX
{
    const string SceneName = "FifthFloorScene";
    const string SheetDir = "Assets/Resouces/pixelfx 1/pixelfx-main/sheets/";
    const string AnimDir = "Assets/Animation/PixelFX/";
    const string PrefabDir = "Assets/Prefab/pixelFX/";
    const string OldTorches = "Torches";
    const string ReplaceName = "torch (thay đuốc cũ)";
    const float PPU = 100f;

    // Sprite PixelFX ở PPU 100 nhỏ hơn đồ tầng 5 (16 px/ô): phóng to để đuốc cao ~2,6 ô như đuốc cũ, lò lửa ~3 ô
    const float TorchScale = 2.2f, BrazierScale = 2f, CampfireScale = 2f;
    static readonly Color Warm = new Color(1f, 0.74f, 0.42f);   // cùng màu CampLight (đèn bạn đặt ở đuốc cũ R42)

    // ===================== MENU =====================
    [MenuItem("Tools/Culinaria/F5 Map FX (PixelFX)")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("[F5FX] Hãy mở FifthFloorScene trước."); return; }
        var log = new List<string>();

        var torch = EnsurePrefab("fire", "torch", true, 6f, 1.6f, log);
        var brazier = EnsurePrefab("fire", "brazier", true, 7f, 1.6f, log);
        var campfire = EnsurePrefab("fire", "campfire", true, 9f, 1.6f, log);   // = CampLight: I 1,6, bán kính 9
        var fog = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "fog.prefab");
        var beam = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "respawn-beam.prefab");
        var glint = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "key-glint.prefab");
        var portal = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "spawn-portal.prefab");

        var roots = scene.GetRootGameObjects();
        var rooms = roots.SelectMany(g => g.GetComponentsInChildren<Room>(true)).ToList();
        foreach (var r in rooms)
            foreach (Transform ch in r.transform.Cast<Transform>().ToList())
                if (ch.name == "MapFX") Object.DestroyImmediate(ch.gameObject);

        int n = 0;
        void Put(GameObject prefab, string label, Vector3 pos, float scale = 1f, float alpha = 1f, bool active = true, float fitWidth = 0f)
        {
            if (prefab == null) { log.Add("Thiếu prefab cho " + label); return; }
            var room = RoomAt(rooms, pos);
            if (room == null) { log.Add($"{label} @({pos.x},{pos.y}) không nằm trong phòng nào"); return; }
            var fx = room.transform.Find("MapFX");
            if (fx == null) { fx = new GameObject("MapFX").transform; fx.SetParent(room.transform, false); fx.localPosition = Vector3.zero; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = label;
            go.transform.SetParent(fx, true);
            go.transform.position = pos;
            go.transform.localScale = prefab.transform.localScale * scale;   // giữ tỉ lệ gốc của prefab
            foreach (var lt in go.GetComponentsInChildren<Light2D>(true))                // đèn điểm bị phóng theo tỉ lệ cha → bù lại để bán kính tính bằng ô thật
                lt.transform.localScale = Vector3.one / Mathf.Max(0.01f, go.transform.lossyScale.x);
            if (fitWidth > 0f)
            {
                var r0 = go.GetComponentInChildren<SpriteRenderer>();
                if (r0 != null && r0.bounds.size.x > 0.01f) go.transform.localScale *= fitWidth / r0.bounds.size.x;
            }
            if (prefab.name == "fog") foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingOrder = -90;   // trên ảnh nền phòng (-97..-93), dưới mọi thứ khác
            if (alpha < 1f) foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true)) { var c = sr.color; c.a = alpha; sr.color = c; }
            go.SetActive(active);
            n++;
        }

        // 1) Đuốc mới đặt đúng chỗ đuốc cũ; đuốc cũ tắt tạm
        var art = roots.FirstOrDefault(g => g.name == "______Art_F5_____");
        var old = art ? art.transform.Find(OldTorches) : null;
        if (old != null)
        {
            foreach (Transform t in old) Put(torch, ReplaceName, t.position + new Vector3(0, 0.6f, 0), TorchScale);
            old.gameObject.SetActive(false);
            log.Add($"Tắt tạm {old.childCount} đuốc cũ (không xóa)");
        }

        // 2) Hiệu ứng theo bảng đã duyệt (toạ độ thế giới; mặt sàn = đáy khung + 1)
        // Trại orc
        Put(campfire, "campfire (giữa hai lều R05)", new Vector3(191f, -18.7f, 0), CampfireScale);
        Put(campfire, "campfire (chợ orc R03)", new Vector3(108f, -18.7f, 0), CampfireScale);
        Put(brazier, "brazier (trạm gác R02)", new Vector3(22f, -18.8f, 0), BrazierScale);
        Put(brazier, "brazier (trạm gác R02)", new Vector3(50f, -18.8f, 0), BrazierScale);
        // Hầm nhiệm vụ
        Put(torch, "torch (hầm lương R07)", new Vector3(68f, -26.7f, 0), TorchScale);
        Put(torch, "torch (ổ goblin R08)", new Vector3(138f, -26.7f, 0), TorchScale);
        // Phố trên
        Put(torch, "torch (cổng thành R10)", new Vector3(215f, -56f, 0), TorchScale);
        Put(torch, "torch (cổng thành R10)", new Vector3(250f, -56f, 0), TorchScale);
        Put(torch, "torch (phố rèn R12)", new Vector3(150f, -56f, 0), TorchScale);
        Put(torch, "torch (quán rượu R13)", new Vector3(90f, -56f, 0), TorchScale);
        Put(brazier, "brazier (trước lò rèn R12)", new Vector3(137f, -58.8f, 0), BrazierScale);
        // Chợ cũ
        Put(brazier, "brazier (quảng trường R15)", new Vector3(24f, -58.8f, 0), BrazierScale);
        Put(brazier, "brazier (quảng trường R15)", new Vector3(46f, -58.8f, 0), BrazierScale);
        // Phố dưới
        Put(torch, "torch (nhà dân R17)", new Vector3(58f, -76f, 0), TorchScale);
        Put(torch, "torch (hẻm thịt R18)", new Vector3(92f, -76f, 0), TorchScale);
        Put(torch, "torch (mép vực R19)", new Vector3(163f, -76f, 0), TorchScale);
        Put(torch, "torch (mép vực R19)", new Vector3(178f, -76f, 0), TorchScale);
        // Mê cung: KHÔNG đặt sương — sheet fog của PixelFX là xám tối (độ sáng ~45/255), không thấy trên nền mê cung tối
        // Phòng boss
        Put(brazier, "brazier (cạnh ngai R43)", new Vector3(50f, -148.8f, 0), BrazierScale);
        Put(brazier, "brazier (cạnh ngai R43)", new Vector3(76f, -148.8f, 0), BrazierScale);

        // 3) Checkpoint, rương, cửa tầng 6
        foreach (var cp in roots.SelectMany(g => g.GetComponentsInChildren<CheckPoint>(true)))
            Put(beam, "respawn-beam (checkpoint)", cp.transform.position);
        foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("F5_Chest")))
            Put(glint, "key-glint (" + t.name + ")", t.position + new Vector3(0, 1f, 0));
        var exit = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name.Contains("F5_Exit"));
        if (exit != null) Put(portal, "spawn-portal (cửa tầng 6, bật sau boss)", exit.position, 1f, 1f, false);
        else log.Add("Không thấy F5_Exit: chưa đặt portal");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[F5FX] Đặt {n} hiệu ứng. " + string.Join(" | ", log));
    }

    [MenuItem("Tools/Culinaria/F5 FX - Dùng đuốc cũ")]
    static void UseOld() => Swap(true);
    [MenuItem("Tools/Culinaria/F5 FX - Dùng đuốc mới")]
    static void UseNew() => Swap(false);

    static void Swap(bool useOld)
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("[F5FX] Hãy mở FifthFloorScene trước."); return; }
        var roots = scene.GetRootGameObjects();
        var art = roots.FirstOrDefault(g => g.name == "______Art_F5_____");
        var old = art ? art.transform.Find(OldTorches) : null;
        if (old != null) old.gameObject.SetActive(useOld);
        int k = 0;
        foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == ReplaceName))
        { t.gameObject.SetActive(!useOld); k++; }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[F5FX] Đang dùng đuốc {(useOld ? "CŨ" : "MỚI (PixelFX)")}: {k} đuốc mới {(useOld ? "tắt" : "bật")}.");
    }

    // ===================== PREFAB =====================
    static Room RoomAt(List<Room> rooms, Vector3 p)
    {
        foreach (var r in rooms)
            foreach (var (_, rect) in F5RoomFrames.RoomRects(r))
                if (p.x >= rect.xMin && p.x < rect.xMax && p.y >= rect.yMin && p.y < rect.yMax) return r;
        return null;
    }

    /// Đồng bộ đèn của prefab đã có với thông số trong Run (đổi số ở Run rồi chạy lại menu là đủ).
    static void TuneLight(string prefabPath, float radius, float intensity, List<string> log)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var l = root.GetComponentInChildren<Light2D>(true);
        if (l != null)
        {
            l.pointLightOuterRadius = radius; l.intensity = intensity; l.color = Warm;
            l.falloffIntensity = 0.4f; l.shadowsEnabled = false;
            l.volumeIntensityEnabled = true; l.volumeIntensity = 0.03f;          // quầng sáng trong không khí
            var so = new SerializedObject(l);                                     // chiếu lên mọi sorting layer
            var layers = so.FindProperty("m_ApplyToSortingLayers");
            if (layers != null)
            {
                var all = SortingLayer.layers; layers.arraySize = all.Length;
                for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (l.GetComponent<LightFlicker2D>() == null)
            {
                var fso = new SerializedObject(l.gameObject.AddComponent<LightFlicker2D>());
                fso.FindProperty("amplitude").floatValue = 0.15f; fso.FindProperty("speed").floatValue = 6f;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            log.Add($"Đèn {System.IO.Path.GetFileNameWithoutExtension(prefabPath)}: I {intensity}, bán kính {radius}, mọi sorting layer, quầng, nhấp nháy");
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    static GameObject EnsurePrefab(string group, string name, bool loop, float lightRadius, float lightIntensity, List<string> log)
    {
        string prefabPath = PrefabDir + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) { TuneLight(prefabPath, lightRadius, lightIntensity, log); return existing; }

        string png = SheetDir + group + "/" + name + ".png";
        string json = File.ReadAllText(SheetDir + group + "/" + name + ".json");
        int[] Arr(string key) => Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)").Groups.Cast<Group>().Skip(1).Select(g => int.Parse(g.Value)).ToArray();
        int Num(string key) => int.Parse(Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)").Groups[1].Value);
        var size = Arr("frameSize"); var pivot = Arr("pivot");
        int fw = size[0], fh = size[1], frames = Num("frames"), cols = Num("columns"), fps = Num("fps");

        // Import: Multiple, Point, không nén, PPU 100, cắt lưới theo json
        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        ti.GetSourceTextureWidthAndHeight(out int texW, out int texH);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.spritePixelsPerUnit = PPU;
        ti.maxTextureSize = Mathf.Max(ti.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(texW, texH)));
        var piv = new Vector2(pivot[0] / (float)fw, 1f - pivot[1] / (float)fh);
        var metas = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
        {
            int c = i % cols, r = i / cols;
            metas[i] = new SpriteMetaData { name = $"{name}_{i}", rect = new Rect(c * fw, texH - (r + 1) * fh, fw, fh), alignment = (int)SpriteAlignment.Custom, pivot = piv };
        }
#pragma warning disable 618
        ti.spritesheet = metas;
#pragma warning restore 618
        ti.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>()
            .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

        // AnimationClip lặp
        if (!AssetDatabase.IsValidFolder("Assets/Animation")) AssetDatabase.CreateFolder("Assets", "Animation");
        if (!AssetDatabase.IsValidFolder("Assets/Animation/PixelFX")) AssetDatabase.CreateFolder("Assets/Animation", "PixelFX");
        var clip = new AnimationClip { frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / (float)fps, value = s }).ToList();
        keys.Add(new ObjectReferenceKeyframe { time = sprites.Length / (float)fps, value = sprites[0] });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var st = AnimationUtility.GetAnimationClipSettings(clip); st.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, st);
        AssetDatabase.CreateAsset(clip, AnimDir + name + ".anim");
        var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(AnimDir + name + ".controller", clip);

        // Prefab: SpriteRenderer + Animator (Cull Completely) + Light2D cam ấm. Không Collider.
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0];
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (unlit != null) sr.sharedMaterial = unlit;   // như phần còn lại của tầng 5 (Sprite-Lit sẽ đen nếu thiếu đèn)
        var refPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "waterfall_0.prefab");
        var refSr = refPrefab ? refPrefab.GetComponent<SpriteRenderer>() : null;
        if (refSr != null) { sr.sortingLayerID = refSr.sortingLayerID; sr.sortingOrder = refSr.sortingOrder; }
        var an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = new Vector3(0, 0.4f, 0);
        var l = lgo.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = Warm;
        l.intensity = lightIntensity;
        l.pointLightInnerRadius = 0.3f;
        l.pointLightOuterRadius = lightRadius;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        log.Add($"Tạo prefab mới {prefabPath} ({frames} khung {fw}×{fh}, {fps} fps)");
        return prefab;
    }
}
