using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TextManager : MonoBehaviour
{
    public List<GameObject> listText;

    [SerializeField] private float timeTextAppear = 2f; // Thời gian giãn cách giữa các chữ
    private float timeText;
    private int currentIndex = 0; // Biến lưu vị trí của text đang hiện

    private void Start()
    {
        // Khởi tạo: Ẩn tất cả các text đi, chỉ bật text đầu tiên
        for (int i = 0; i < listText.Count; i++)
        {
            listText[i].SetActive(i == 0);
        }

        timeText = timeTextAppear;
        currentIndex = 0;
    }

    private void Update()
    {
        // Đếm ngược thời gian
        if (timeText > 0)
        {
            timeText -= Time.deltaTime;
        }
        else
        {
            // Khi thời gian <= 0 thì gọi hàm chuyển chữ
            TextAppear();
        }
    }

    public void TextAppear()
    {
        // Kiểm tra an toàn để tránh lỗi nếu list trống
        if (listText == null || listText.Count == 0) return;
        // 1. Tắt GameObject chữ hiện tại
        listText[currentIndex].SetActive(false);
        // Kiểm tra xem đã chạy đến cuối List chưa
        if (currentIndex + 1 >= listText.Count)
        {
            Destroy(gameObject);
            return;
        }

        // 2. Tăng index lên 1 để chuyển sang chữ tiếp theo
        currentIndex++;
        

        // 3. Bật GameObject chữ mới lên
        listText[currentIndex].SetActive(true);

        // 4. Reset lại đồng hồ đếm ngược
        timeText = timeTextAppear;
    }
}
