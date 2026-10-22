using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Cinemachine;

public class CheckPoint : MonoBehaviour
{
    [Tooltip("Duy nhất toàn game, vd F2_CP01. Không đổi sau khi đã có save.")]
    [SerializeField] private string checkpointId;
    public CinemachineVirtualCamera areaCamera;
    public string CheckPointId => checkpointId;
    private void Awake()
    {
        if (string.IsNullOrEmpty(checkpointId)) Debug.LogError($"{name}:Chưa đặt CheckPoint Id", this);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(!collision.CompareTag("Player")) return;
        Player player = collision.GetComponent<Player>();
        if (player == null) return;
        // Lấy tên camera nếu areaCamera đã được gán
        string camName = areaCamera != null ? areaCamera.name : "";

        // Cập nhật UpdateSavePoint để truyền thêm tên camera
        player.UpdateSavePoint(transform.position, camName,checkpointId,gameObject.scene.name);
    }
}
