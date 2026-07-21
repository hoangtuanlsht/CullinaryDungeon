using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CheckPoint : MonoBehaviour
{
    public CinemachineVirtualCamera areaCamera;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                // Lấy tên camera nếu areaCamera đã được gán
                string camName = areaCamera != null ? areaCamera.name : "";

                // Cập nhật UpdateSavePoint để truyền thêm tên camera
                player.UpdateSavePoint(transform.position, camName);
            }
        }
    }
}
