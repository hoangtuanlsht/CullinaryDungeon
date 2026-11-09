using System.Linq;
using UnityEditor;
using UnityEngine;

/// [Tầng 5] In khung phòng (Room + vcam) của scene đang active ra Console (chỉ đọc). Xoá sau khi dùng.
public static class F5RoomProbe
{
    [MenuItem("Tools/Culinaria/F5 - In khung phòng của scene active")]
    public static void Probe()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log($"[ROOM] Scene {scene.name}");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var room in root.GetComponentsInChildren<global::Room>(true))
        {
            var t = room.transform;
            var so = new SerializedObject(room);
            string id = so.FindProperty("roomId")?.stringValue;
            var cols = room.GetComponents<Collider2D>();
            string shape = string.Join(" | ", cols.Select(c =>
            {
                if (c is PolygonCollider2D pc)
                    return "Poly[" + string.Join(" ", Enumerable.Range(0, pc.pathCount).Select(p => "(" + string.Join(";", pc.GetPath(p).Select(v => { var w = t.TransformPoint(v + pc.offset); return $"{w.x:0.##},{w.y:0.##}"; })) + ")")) + "]";
                if (c is BoxCollider2D bc) { var b = bc.bounds; return $"Box[{b.min.x:0.##},{b.min.y:0.##} → {b.max.x:0.##},{b.max.y:0.##}]"; }
                return c.GetType().Name;
            }));
            string extra = "";
            foreach (var p in new[] { "vcam", "roomBounds", "bounds", "confiner" })
            {
                var sp = so.FindProperty(p);
                if (sp != null && sp.propertyType == SerializedPropertyType.ObjectReference) extra += $" {p}={(sp.objectReferenceValue ? sp.objectReferenceValue.name : "-")}";
            }
            var it = so.GetIterator(); string fields = "";
            if (it.NextVisible(true)) do { if (it.name != "m_Script") fields += it.name + ","; } while (it.NextVisible(false));
            Debug.Log($"[ROOM] {t.name} id={id} pos={t.position} scale={t.localScale} active={t.gameObject.activeInHierarchy} {shape}{extra} fields={fields}");
            foreach (var vc in t.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Name == "CinemachineVirtualCamera"))
            {
                var vso = new SerializedObject(vc);
                var lens = vso.FindProperty("m_Lens.OrthographicSize");
                var follow = vso.FindProperty("m_Follow");
                Debug.Log($"[ROOM]   vcam {vc.name} pos={vc.transform.position} ortho={(lens != null ? lens.floatValue : -1)} follow={(follow != null && follow.objectReferenceValue ? follow.objectReferenceValue.name : "-")} prio={vso.FindProperty("m_Priority")?.intValue}");
                foreach (var ext in vc.GetComponents<Component>().Where(c => c != null && c.GetType().Name.StartsWith("Cinemachine") && c != vc))
                {
                    var eso = new SerializedObject(ext); var ei = eso.GetIterator(); string ef = "";
                    if (ei.NextVisible(true)) do { if (ei.propertyType == SerializedPropertyType.Float || ei.propertyType == SerializedPropertyType.Boolean || ei.propertyType == SerializedPropertyType.ObjectReference || ei.propertyType == SerializedPropertyType.Integer)
                        ef += $"{ei.name}={(ei.propertyType == SerializedPropertyType.Float ? ei.floatValue.ToString("0.##") : ei.propertyType == SerializedPropertyType.Boolean ? ei.boolValue.ToString() : ei.propertyType == SerializedPropertyType.Integer ? ei.intValue.ToString() : (ei.objectReferenceValue ? ei.objectReferenceValue.name : "-"))} "; } while (ei.NextVisible(false));
                    Debug.Log($"[ROOM]     {ext.GetType().Name}: {ef}");
                }
                foreach (Transform ch in vc.transform)
                    foreach (var comp in ch.GetComponents<Component>().Where(c => c != null && c.GetType().Name.StartsWith("Cinemachine")))
                    {
                        var cso = new SerializedObject(comp); var ci = cso.GetIterator(); string cf = "";
                        if (ci.NextVisible(true)) do { if (ci.propertyType == SerializedPropertyType.Float || ci.propertyType == SerializedPropertyType.Boolean) cf += $"{ci.name}={(ci.propertyType == SerializedPropertyType.Float ? ci.floatValue.ToString("0.##") : ci.boolValue.ToString())} "; } while (ci.NextVisible(false));
                        Debug.Log($"[ROOM]     body {comp.GetType().Name}: {cf}");
                    }
            }
        }
    }
}
