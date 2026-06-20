using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chest : MonoBehaviour,Interact
{
    [SerializeField] private int coinAmount = 500; // Số vàng trong rương
    public void InteractWithObject()
    {
        Debug.Log("Mở rương! Nhận "+coinAmount+" vàng!");
        // Viết thêm code chạy hoạt ảnh mở rương hoặc cộng tiền ở đây...

        // Tương tác xong thì hủy hoặc tắt Collider để không bấm lại được nữa
        GetComponent<Collider2D>().enabled = false;
        SoundManager.Play("ChestOpen");
        Player.coin += coinAmount; // Cộng vàng cho người chơi
        UIManager.instance.SetCoin(Player.coin); // Cập nhật giao diện hiển thị vàng
    }
}
