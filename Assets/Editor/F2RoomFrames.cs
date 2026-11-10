// Chia lại khung phòng tầng 2 theo cách đã làm ở FirstFloorScene 1. Chạy: Tools > Culinaria > Reframe Floor 2 Rooms.
// Mỗi phòng F2_Rxx (trong ______Room_____ đang bật) được chỉnh như sau:
//  - Collider của Room (trigger nhận diện phòng): hình chữ nhật của phòng nới thêm 2 ô mỗi phía,
//    nên phòng kề nhau giao nhau một dải. RoomTracker dùng dải này làm vùng đệm, camera không nhảy qua lại ở cửa.
//  - Con "CamBounds" (PolygonCollider2D trigger, layer Room): ôm sát khoảng trống chơi được (+1 ô mép đất),
//    dùng làm Bounding Shape của CinemachineConfiner2D. Camera không còn lộ khối đất dày ở mép phòng.
//  - VCam_Room được đổi tên thành VCam_<RoomId> và trỏ Confiner sang CamBounds.
// Dữ liệu khung: Assets/BackGround/SecondFloor/F2Data/f2_v3_frames.txt. Chạy lại được.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class F2RoomFrames
{
    const string DataFile = "Assets/BackGround/SecondFloor/F2Data/f2_v3_frames.txt";

    [MenuItem("Tools/Culinaria/Reframe Floor 2 Rooms")]
    public static void Apply()
    {
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .FirstOrDefault(o => o.name == "______Room_____" && o.activeSelf);
        if (root == null) { Debug.LogError("[F2 Frames] Không thấy ______Room_____ đang bật"); return; }

        int done = 0;
        foreach (var line in File.ReadAllLines(DataFile))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split('|');
            var room = root.transform.Find(parts[0]);
            if (room == null) { Debug.LogWarning("[F2 Frames] Thiếu phòng " + parts[0]); continue; }

            // 1) collider nhận diện phòng (có dải đệm)
            var poly = room.GetComponent<PolygonCollider2D>();
            Undo.RecordObject(poly, "Reframe F2");
            poly.isTrigger = true;
            poly.offset = Vector2.zero;
            poly.pathCount = 1;
            poly.SetPath(0, Parse(parts[1]));

            // 2) CamBounds ôm sát khoảng chơi
            var cb = room.Find("CamBounds");
            if (cb == null)
            {
                var go = new GameObject("CamBounds");
                Undo.RegisterCreatedObjectUndo(go, "Reframe F2");
                go.transform.SetParent(room, false);
                cb = go.transform;
            }
            cb.gameObject.layer = room.gameObject.layer;
            cb.localPosition = Vector3.zero;
            var cpoly = cb.GetComponent<PolygonCollider2D>();
            if (cpoly == null) cpoly = cb.gameObject.AddComponent<PolygonCollider2D>();
            Undo.RecordObject(cpoly, "Reframe F2");
            cpoly.isTrigger = true;
            cpoly.offset = Vector2.zero;
            cpoly.pathCount = 1;
            cpoly.SetPath(0, Parse(parts[2]));

            // 3) vcam: đổi tên + Confiner trỏ CamBounds
            var vcam = room.Find("VCam_Room");
            if (vcam == null) vcam = room.Find("VCam_" + parts[0]);
            if (vcam != null)
            {
                Undo.RecordObject(vcam.gameObject, "Reframe F2");
                vcam.name = "VCam_" + parts[0];
                var confiner = vcam.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "CinemachineConfiner2D");
                if (confiner != null)
                {
                    var so = new SerializedObject(confiner);
                    var p = so.FindProperty("m_BoundingShape2D");
                    if (p != null) { p.objectReferenceValue = cpoly; so.ApplyModifiedProperties(); }
                }
                else Debug.LogWarning("[F2 Frames] " + parts[0] + ": vcam không có CinemachineConfiner2D");
            }
            done++;
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[F2 Frames] Đã chia lại khung {done} phòng.");
    }

    static Vector2[] Parse(string s) =>
        s.Split(';').Select(t => t.Split(',')).Select(a => new Vector2(
            float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray();
}
#endif
