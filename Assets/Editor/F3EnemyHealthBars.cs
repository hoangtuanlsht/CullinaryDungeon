using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Mỗi quái thường của tầng 3 cần 1 Canvas-HealthBar riêng trong scene, gán vào trường healthBar của Character
/// (HealthBar tự bám theo quái; Enemy/FlyEnemy xóa nó khi chết). Builder tầng 3 trước đây chỉ đặt prefab quái
/// nên thiếu thanh máu (và quái chết sẽ báo lỗi Destroy(healthBar.gameObject)).
/// Chạy: Tools > Culinaria > F3 Add Enemy Health Bars. Builder tầng 3 cũng tự gọi. Chạy lại được (quái đã có thanh máu thì bỏ qua).
public static class F3EnemyHealthBars
{
    const string SceneName = "ThirdFloorScene";
    const string EnemyRoot = "______Enemy_____";
    const string BarRoot = "______EnemyHealthBar_____";
    const string BarPrefab = "Assets/Prefab/Canvas-HealthBar.prefab";

    [MenuItem("Tools/Culinaria/F3 Add Enemy Health Bars")]
    public static void Run()
    {
        var scene = SceneManager.GetSceneByName(SceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("F3 Health Bars", "Hãy mở ThirdFloorScene trước.", "OK");
            return;
        }
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void Apply(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarPrefab);
        if (prefab == null) { Debug.LogError("[F3HealthBar] Thiếu prefab " + BarPrefab); return; }
        var roots = scene.GetRootGameObjects();
        var enemyRoot = roots.FirstOrDefault(g => g.name == EnemyRoot);
        if (enemyRoot == null) { Debug.LogError("[F3HealthBar] Không thấy " + EnemyRoot); return; }

        var barRoot = roots.FirstOrDefault(g => g.name == BarRoot);
        if (barRoot == null)
        {
            barRoot = new GameObject(BarRoot);
            SceneManager.MoveGameObjectToScene(barRoot, scene);
        }

        int added = 0, had = 0;
        foreach (var ch in enemyRoot.GetComponentsInChildren<Character>(true))
        {
            var so = new SerializedObject(ch);
            var p = so.FindProperty("healthBar");
            if (p == null) continue;
            if (p.objectReferenceValue != null) { had++; continue; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, barRoot.transform);
            go.name = "HB_" + ch.name;
            var bar = go.GetComponentInChildren<HealthBar>(true);
            if (bar == null) { Object.DestroyImmediate(go); Debug.LogError("[F3HealthBar] Prefab không có HealthBar"); return; }
            // đặt sẵn đúng chỗ trong editor (lúc chạy HealthBar tự bám theo quái)
            var off = new SerializedObject(bar).FindProperty("offset");
            go.transform.position = ch.transform.position + (off != null ? off.vector3Value : Vector3.up);
            p.objectReferenceValue = bar;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ch);
            added++;
        }
        Debug.Log($"[F3HealthBar] Thêm {added} thanh máu, {had} quái đã có sẵn.");
    }
}
