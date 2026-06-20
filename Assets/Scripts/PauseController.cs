using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseController : MonoBehaviour
{
    public static bool IsGamePause {  get; private set; } =false;

    public static void SetPause(bool pause)
    {
        IsGamePause = pause;
        if (pause)
        {
            Time.timeScale = 0f; // Đóng băng toàn bộ vật lý, animation và thời gian
        }
        else
        {
            Time.timeScale = 1f; // Đưa thời gian chạy lại bình thường
        }
    }
}
