using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPoint : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem đối tượng chạm vào có phải là Player không
        if (collision.CompareTag("Player"))
        {
            Player player = collision.GetComponent<Player>();
            if (player != null)
            {
                // Truyền tọa độ của Save Point này cho Player
                player.UpdateSavePoint(transform.position);

                // Tùy chọn: Bạn có thể thêm code bật hiệu ứng sáng lên hoặc âm thanh save game ở đây
            }
        }
    }
}
