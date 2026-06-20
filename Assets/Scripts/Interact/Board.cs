using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board :MonoBehaviour, Interact
{
    public void InteractWithObject()
    {
        Debug.Log("Bảng thông báo! Đây là một bảng thông báo.");
        // Viết thêm code hiển thị thông báo hoặc mở menu ở đây...
        // Tương tác xong thì hủy hoặc tắt Collider để không bấm lại được nữa
        GetComponent<Collider2D>().enabled = false;
    }
}
