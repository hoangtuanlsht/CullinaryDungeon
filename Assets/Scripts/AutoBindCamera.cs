using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoBindCamera : MonoBehaviour
{
    private void Start()
    {
        // Tìm GameObject có tên là "Player" hoặc bạn có thể tìm qua Tag "Player"
        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            CinemachineVirtualCamera vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam != null)
            {
                vcam.Follow = player.transform;
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy Player để gán cho Virtual Camera!");
        }
    }
}
