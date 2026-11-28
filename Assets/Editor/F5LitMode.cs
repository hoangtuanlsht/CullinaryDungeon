using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// [Tầng 5] Ánh sáng thật (giống tầng 6): môi trường dùng Sprite-Lit-Default + một Global Light mờ, đuốc/lò lửa rọi sáng tường và nhà.
///  - "Bật Lit": địa hình, nhà, đồ trang trí, ảnh nền, tường giả/cổng chặn, rương → Sprite-Lit-Default.
///    Nguồn sáng tự phát sáng (đuốc cũ/mới, lò lửa, lửa trại, bếp checkpoint, đèn thờ, vệt sáng chuyển phòng, cột sáng, lấp lánh) → Sprites-Default.
///    Vòng tròn fog of war (layer FogOfWar/FogOfWarLayer2) không đụng tới.
///    Đèn: GlobalLight trong ______Lighting_F5_____; đèn của đuốc/lò lửa/lửa trại chiếu lên mọi sorting layer, có quầng sáng và nhấp nháy.
///  - "Tắt Lit": mọi renderer đang Lit trong các nhóm trên → Sprites-Default, tắt GlobalLight. Tầng 5 trở về như trước.
/// Không đụng RoomGlow, collider, camera. Không lưu scene.
public static class F5LitMode
{
    const string SceneName = "FifthFloorScene";
    const string LitMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    const string LightRoot = "______Lighting_F5_____";
    static readonly Color GlobalColor = new Color(0.80f, 0.84f, 0.98f);   // lạnh, hợp đá xanh xám thành phố cũ
    const float GlobalIntensity = 0.55f;

    /// Root chứa thứ cần NHẬN sáng
    static readonly string[] LitRoots = { "Grid_F5", "______Art_F5_____", "______SceneBG_F5_____", "______Hidden_F5_____", "______Door_F5_____", "______Room_F5_____", "______Interact_F5_____" };
    /// Root chỉ chứa thứ TỰ phát sáng
    static readonly string[] GlowRoots = { "______HoleGlow_____", "______TunnelGlow_____" };

    static bool SelfLit(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            string n = p.name;
            if (n == "Torches" || n.StartsWith("Braziers") || n.StartsWith("Torch") || n.StartsWith("torch") || n.StartsWith("brazier") || n.StartsWith("campfire")
                || n.StartsWith("CampFire") || n.StartsWith("respawn-beam") || n.StartsWith("key-glint") || n.StartsWith("spawn-portal") || n.StartsWith("Glow"))
                return true;
        }
        return false;
    }

    static bool FogLayer(GameObject g) { string l = LayerMask.LayerToName(g.layer); return l.StartsWith("FogOfWar"); }

    [MenuItem("Tools/Culinaria/F5 Ánh sáng - Bật Lit (ánh sáng thật)")]
    public static void On() => Apply(true);
    [MenuItem("Tools/Culinaria/F5 Ánh sáng - Tắt Lit (về như cũ)")]
    public static void Off() => Apply(false);

    static void Apply(bool on)
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("[F5Lit] Hãy mở FifthFloorScene trước."); return; }
        if (on && !CulinariaUrp2D.IsActive) { Debug.LogError("[F5Lit] URP 2D chưa bật (Tools > Culinaria > Lighting)."); return; }
        var lit = AssetDatabase.LoadAssetAtPath<Material>(LitMatPath);
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (lit == null || unlit == null) { Debug.LogError("[F5Lit] Thiếu material Sprite-Lit-Default hoặc Sprites-Default."); return; }

        var roots = scene.GetRootGameObjects();
        int nLit = 0, nGlow = 0;
        foreach (var root in roots)
        {
            bool litRoot = LitRoots.Contains(root.name), glowRoot = GlowRoots.Contains(root.name);
            if (!litRoot && !glowRoot) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SpriteRenderer || r is TilemapRenderer) || FogLayer(r.gameObject)) continue;
                if (r.sharedMaterial != null && r.sharedMaterial != lit && r.sharedMaterial != unlit) continue;   // material riêng (vd fog of war) giữ nguyên
                bool wantLit = on && litRoot && !SelfLit(r.transform);
                var m = wantLit ? lit : unlit;
                if (r.sharedMaterial == m) continue;
                r.sharedMaterial = m; EditorUtility.SetDirty(r);
                if (wantLit) nLit++; else nGlow++;
            }
        }

        // Đèn nền
        var lr = roots.FirstOrDefault(g => g.name == LightRoot);
        if (lr == null && on)
        {
            lr = new GameObject(LightRoot);
            SceneManager.MoveGameObjectToScene(lr, scene);
            var g = new GameObject("GlobalLight (độ sáng nền của cả tầng)");
            g.transform.SetParent(lr.transform, false);
            var l = g.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Global; l.color = GlobalColor; l.intensity = GlobalIntensity;
            AllLayers(l);
        }
        if (lr != null) lr.SetActive(on);

        // Đèn của đuốc/lò lửa/lửa trại (cả đèn CampLight cũ): chiếu lên mọi sorting layer, có quầng và nhấp nháy
        int nLights = 0;
        foreach (var l in roots.SelectMany(g => g.GetComponentsInChildren<Light2D>(true)).Where(l => l.lightType == Light2D.LightType.Point))
        {
            AllLayers(l);
            l.falloffIntensity = 0.4f; l.shadowsEnabled = false;
            l.volumeIntensityEnabled = true; l.volumeIntensity = 0.03f;
            if (l.GetComponent<LightFlicker2D>() == null)
            {
                var fso = new SerializedObject(l.gameObject.AddComponent<LightFlicker2D>());
                var a = fso.FindProperty("amplitude"); if (a != null) a.floatValue = 0.15f;
                var s = fso.FindProperty("speed"); if (s != null) s.floatValue = 6f;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(l); nLights++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(on
            ? $"[F5Lit] BẬT: {nLit} renderer nhận sáng, {nGlow} nguồn sáng giữ tự phát sáng, GlobalLight {GlobalIntensity}, {nLights} đèn điểm chiếu mọi layer. Chưa lưu scene."
            : $"[F5Lit] TẮT: {nGlow} renderer về Sprites-Default, GlobalLight tắt. Chưa lưu scene.");
    }

    static void AllLayers(Light2D l)
    {
        var so = new SerializedObject(l);
        var layers = so.FindProperty("m_ApplyToSortingLayers");
        if (layers == null) return;
        var all = SortingLayer.layers; layers.arraySize = all.Length;
        for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
