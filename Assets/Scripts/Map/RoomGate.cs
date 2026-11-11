using UnityEngine;

/// Đánh dấu chỗ chuyển phòng (do F3RoomFrames tạo). Trên object: trigger tag "EnemyWall" (quái tuần tra quay đầu).
/// Con "Barrier" (layer EnemyBarrier, collider đặc) chỉ va chạm với layer Enemy → quái bị chặn hẳn kể cả khi đang đuổi
/// hoặc bị đánh văng; Player đi xuyên qua. Đạn lửa (Flame) chạm vào đây thì nổ.
public class RoomGate : MonoBehaviour
{
    public const string BarrierLayer = "EnemyBarrier";

    /// Đảm bảo ma trận va chạm đúng cả khi Project Settings chưa lưu: EnemyBarrier chỉ va với Enemy.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetupCollisionMatrix()
    {
        int bl = LayerMask.NameToLayer(BarrierLayer), el = LayerMask.NameToLayer("Enemy");
        if (bl < 0 || el < 0) return;
        for (int i = 0; i < 32; i++) Physics2D.IgnoreLayerCollision(bl, i, i != el);
    }
}
