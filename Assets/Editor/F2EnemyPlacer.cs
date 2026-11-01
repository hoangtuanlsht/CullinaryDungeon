// Đặt quái cho tầng 2 (Mushroom, Plant3, Slime). Chạy: Tools > Culinaria > Place Floor 2 Enemies.
// Mỗi con gồm: prefab quái + thanh máu (Canvas-HealthBar) + 2 EnemyWall chặn hai đầu đường tuần tra.
// Chạy lại được: mỗi lần xoá và tạo lại ______EnemyF2______.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class F2EnemyPlacer
{
    const string EnemyDir = "Assets/Prefab/Enemy/";
    const string HealthBarPrefab = "Assets/Prefab/Canvas-HealthBar.prefab";

    // phòng, loại quái, x, mặt đất (y chân), tường trái, tường phải
    static readonly (string room, string type, float x, float feetY, float wallL, float wallR)[] Spawns =
    {
        ("F2_R02", "Plant3",   28.5f, -76, 26.2f, 30.8f),   // trên bậc đá
        ("F2_R02", "Mushroom", 38.5f, -78, 34.5f, 43.5f),   // trước miệng hố xuống lối tắt
        ("F2_R02", "Slime",    51.5f, -78, 48.5f, 64.5f),   // lối sang đầm lầy
        ("F2_R03", "Plant3",   71.5f, -78, 70.2f, 73.8f),   // giữa hai đầm
        ("F2_R05", "Plant3",   98.0f, -78, 96.2f, 99.8f),   // canh cửa vào hub
        ("F2_R05", "Mushroom",110.0f, -75, 106.5f, 113.5f), // trên gò giữa phòng
        ("F2_R05", "Slime",   120.5f, -67, 116.5f, 124.5f), // gờ tán cây bên phải
        ("F2_R07", "Slime",   128.0f, -78, 126.2f, 129.8f), // đầu đầm lầy sâu
        ("F2_R07", "Plant3",  136.5f, -78, 135.2f, 137.8f), // mỏm đất giữa đầm
        ("F2_R07", "Mushroom",160.0f, -78, 158.2f, 161.8f), // cuối đầm, sát tàn tích
        ("F2_R09", "Plant3",   54.0f, -88, 48.5f, 59.5f),   // lối tắt
        ("F2_R09", "Mushroom", 71.5f, -88, 64.5f, 79.5f),
        ("F2_R09", "Slime",    92.5f, -88, 85.5f, 99.5f),
    };

    [MenuItem("Tools/Culinaria/Place Floor 2 Enemies")]
    public static void Place()
    {
        var old = GameObject.Find("______EnemyF2______");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("______EnemyF2______");
        Undo.RegisterCreatedObjectUndo(root, "Place Floor 2 Enemies");

        var hbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefab);
        int i = 0, missing = 0;
        foreach (var s in Spawns)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDir + s.type + ".prefab");
            if (prefab == null) { missing++; Debug.LogWarning("Thiếu prefab " + s.type); continue; }
            i++;
            var group = new GameObject($"{s.room}_{s.type}_{i:00}");
            group.transform.SetParent(root.transform);

            // quái: thả cao hơn mặt đất 1 ô, trọng lực sẽ kéo xuống
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            enemy.name = s.type;
            enemy.transform.position = new Vector3(s.x, s.feetY + 1f, 0f);

            // thanh máu riêng cho từng con, gán vào Character.healthBar
            if (hbPrefab != null)
            {
                var hb = (GameObject)PrefabUtility.InstantiatePrefab(hbPrefab, group.transform);
                hb.name = "HealthBar_" + s.type;
                hb.transform.position = enemy.transform.position + Vector3.up;
                var ch = enemy.GetComponent<Character>();
                if (ch != null)
                {
                    var so = new SerializedObject(ch);
                    var prop = so.FindProperty("healthBar");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = hb.GetComponent<HealthBar>();
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            MakeWall(group.transform, "EnemyWall_L", s.wallL, s.feetY);
            MakeWall(group.transform, "EnemyWall_R", s.wallR, s.feetY);
        }

        // các marker Enemy_* cũ không còn cần
        var markers = GameObject.Find("F2_Markers");
        if (markers != null)
            for (int k = markers.transform.childCount - 1; k >= 0; k--)
            {
                var c = markers.transform.GetChild(k).gameObject;
                if (c.name.StartsWith("Enemy_")) Undo.DestroyObjectImmediate(c);
            }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[F2EnemyPlacer] Đã đặt {i} quái cho tầng 2. Thiếu prefab: {missing}");
    }

    static void MakeWall(Transform parent, string name, float x, float feetY)
    {
        var w = new GameObject(name);
        w.tag = "EnemyWall";
        w.transform.SetParent(parent);
        w.transform.position = new Vector3(x, feetY + 1.5f, 0f);
        var col = w.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.5f, 3f);   // cao 3 ô để chắc chắn chạm thân quái
    }
}
#endif
