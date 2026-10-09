using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 7] Sàn nứt: chỉ Ground Pound phá được. Là GameObject riêng (sprite + BoxCollider2D),
/// KHÔNG vẽ bằng Tilemap — Tilemap gộp collider nên không phá từng mảng được.
[RequireComponent(typeof(Collider2D))]
public class BreakableFloor : MonoBehaviour
{
    [SerializeField] private string floorId;                  // duy nhất toàn game, vd "F1_R07_Floor"

    private void Start()
    {
        if (string.IsNullOrEmpty(floorId)) Debug.LogError($"{name}: chưa đặt Floor Id", this);
        else if (WorldProgress.HasFlag(floorId)) Destroy(gameObject);      // đã phá, scene vừa load lại
    }

    public void Break()
    {
        WorldProgress.SetFlag(floorId);
        GetComponent<Collider2D>().enabled = false;           // tắt ngay: Destroy chỉ có hiệu lực cuối frame
        Destroy(gameObject);
    }
}
