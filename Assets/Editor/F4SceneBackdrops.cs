using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// [Tầng 4] Đặt ảnh cảnh nền "the_drowned" (Firepious, 256×144) vào vài phòng mốc của thành phố chìm.
/// - KHÔNG đụng nền có sẵn: mọi thứ nằm dưới root riêng "______SceneBG_F4_____" (FourthFloorBuilder không xóa root này khi dựng lại).
/// - Ảnh được chép vào Assets/BackGround/FourthFloor/F4_Scenes và import riêng (PPU 14,4 → một ảnh = đúng một khung 18×10 ô).
/// - Bản lặp ngang lật gương xen kẽ để hai mép liền nhau.
/// Chạy: Tools > Culinaria > F4 Scene Backdrops (the_drowned). Chạy lại được. Gỡ: xóa root "______SceneBG_F4_____".
public static class F4SceneBackdrops
{
    const string SceneName = "FourthFloorScene";
    const string ScenePath = "Assets/Scenes/FourthFloorScene.unity";
    const string RootName = "______SceneBG_F4_____";
    const string SrcDir = "Assets/Resouces/the_drowned/1x/";
    const string DstDir = "Assets/BackGround/FourthFloor/F4_Scenes";
    const int Order = -27;      // trên các lớp nền chung (-40..-28), dưới địa hình và mọi thứ khác

    /// (tên, x trái, y đáy, số khung lặp ngang, ảnh, độ sáng). Tọa độ tile theo khung gốc trong tài liệu thiết kế tầng 4.
    static readonly (string name, int x, int y, int n, string img, float light)[] Scenes =
    {
        ("R16_CongThanhChim", 90, -110, 2, "01_the_descent", 0.85f),
        ("R19_ThapChuongChim", 54, -120, 1, "02_the_tower", 0.90f),          // khung dưới của tháp (cửa từ đại lộ)
        ("R18_DaiLoChim", 72, -120, 3, "03_the_carillon_field", 0.80f),
        ("R25_HoSau", 126, -160, 1, "04_the_silence", 0.90f),                // khung dưới của hố sâu
    };

    [MenuItem("Tools/Culinaria/F4 Scene Backdrops (the_drowned)")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        bool opened = false;
        if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        var log = new List<string>(); int placed = 0;
        foreach (var sc in Scenes)
        {
            var sp = CopySingle(SrcDir + sc.img + ".png", "F4_Scene_" + sc.img + ".png", log);
            if (sp == null) continue;
            for (int i = 0; i < sc.n; i++)
            {
                var go = new GameObject($"Scene_{sc.name}_{i}"); go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(sc.x + 18 * (i + 0.5f), sc.y + 5f, 0);
                go.transform.localScale = new Vector3(18f / sp.bounds.size.x, 10f / sp.bounds.size.y, 1);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = Order; sr.flipX = i % 2 == 1;
                sr.color = new Color(sc.light, sc.light, sc.light, 1);
                placed++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[F4Scenes] Đặt {placed} ảnh cảnh nền vào {Scenes.Length} phòng. " + string.Join(" | ", log));
    }

    static Sprite CopySingle(string src, string fileName, List<string> log)
    {
        if (!System.IO.File.Exists(src)) { log.Add("Không có " + src); return null; }
        if (!AssetDatabase.IsValidFolder(DstDir)) AssetDatabase.CreateFolder("Assets/BackGround/FourthFloor", "F4_Scenes");
        string dst = DstDir + "/" + fileName;
        if (!System.IO.File.Exists(dst))
        {
            System.IO.File.Copy(src, dst);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            log.Add("Chép " + src + " → " + dst);
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
        var set = new TextureImporterSettings(); ti.ReadTextureSettings(set);
        if (set.textureType != TextureImporterType.Sprite || set.spritePixelsPerUnit != 14.4f || set.spriteMode != (int)SpriteImportMode.Single
            || set.filterMode != FilterMode.Point || set.spriteAlignment != (int)SpriteAlignment.Center)
        {
            set.textureType = TextureImporterType.Sprite; set.spriteMode = (int)SpriteImportMode.Single;
            set.spritePixelsPerUnit = 14.4f; set.filterMode = FilterMode.Point; set.mipmapEnabled = false;
            set.alphaIsTransparency = true; set.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(set);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(dst);
    }
}
