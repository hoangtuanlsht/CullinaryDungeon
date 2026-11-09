using System.Linq;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEngine;

/// Công cụ tạm: in cấu trúc khung phòng (Room collider, CamBounds, Confiner, VCam) của scene đang mở.
public static class RoomReport
{
    [MenuItem("Tools/Culinaria/Room Report")]
    public static void Run()
    {
        Debug.Log("[RR] start, rooms=" + Object.FindObjectsOfType<Room>(true).Length);
        foreach (var room in Object.FindObjectsOfType<Room>(true).OrderBy(r => r.name))
        {
            var sb = new StringBuilder();
            sb.Append($"[RR] {room.name} layer={LayerMask.LayerToName(room.gameObject.layer)} pos={room.transform.position}");
            foreach (var c in room.GetComponents<Collider2D>()) sb.Append($" | ROOT {Describe(c)}");
            foreach (Transform ch in room.transform)
            {
                foreach (var c in ch.GetComponents<Collider2D>()) sb.Append($" | CHILD {ch.name} layer={LayerMask.LayerToName(ch.gameObject.layer)} {Describe(c)}");
                var vc = ch.GetComponent<CinemachineVirtualCamera>();
                if (vc != null)
                {
                    var conf = ch.GetComponent<CinemachineConfiner2D>();
                    sb.Append($" | VCAM {ch.name} pos={ch.position} ortho={vc.m_Lens.OrthographicSize} prio={vc.Priority} confiner={(conf != null && conf.m_BoundingShape2D != null ? conf.m_BoundingShape2D.name + "@" + conf.m_BoundingShape2D.transform.parent?.name : "none")} damp={(conf != null ? conf.m_Damping : -1)} maxWin={(conf != null ? conf.m_MaxWindowSize : -1)}");
                    var ft = vc.GetCinemachineComponent<CinemachineFramingTransposer>();
                    if (ft != null) sb.Append($" FT(xd={ft.m_XDamping},yd={ft.m_YDamping},dzW={ft.m_DeadZoneWidth},dzH={ft.m_DeadZoneHeight},sx={ft.m_ScreenX},sy={ft.m_ScreenY},la={ft.m_LookaheadTime})");
                    var tr = vc.GetCinemachineComponent<CinemachineTransposer>();
                    if (tr != null) sb.Append($" TR(off={tr.m_FollowOffset},xd={tr.m_XDamping},yd={tr.m_YDamping})");
                    var body = vc.GetCinemachineComponent(CinemachineCore.Stage.Body);
                    var aim = vc.GetCinemachineComponent(CinemachineCore.Stage.Aim);
                    sb.Append($" body={(body ? body.GetType().Name : "none")} aim={(aim ? aim.GetType().Name : "none")}");
                }
            }
            var so = new SerializedObject(room);
            var vp = so.FindProperty("vcam");
            sb.Append($" | room.vcam={(vp.objectReferenceValue ? vp.objectReferenceValue.name : "null")}");
            Debug.Log(sb.ToString());
        }
        var brain = Object.FindObjectOfType<CinemachineBrain>(true);
        if (brain) Debug.Log($"[RR] Brain blend={brain.m_DefaultBlend.m_Style} {brain.m_DefaultBlend.m_Time}s in {brain.gameObject.scene.name}");
    }

    static string Describe(Collider2D c)
    {
        string s = $"{c.GetType().Name} trig={c.isTrigger} worldBounds=({c.bounds.min.x:F1},{c.bounds.min.y:F1})-({c.bounds.max.x:F1},{c.bounds.max.y:F1})";
        if (c is PolygonCollider2D p)
        {
            s += $" paths={p.pathCount} pts=";
            for (int i = 0; i < p.pathCount; i++) s += "[" + string.Join(" ", p.GetPath(i).Select(v => { var w = p.transform.TransformPoint(v + p.offset); return $"({w.x:F1},{w.y:F1})"; })) + "]";
        }
        return s;
    }
}
