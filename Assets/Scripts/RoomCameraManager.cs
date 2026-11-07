using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomCameraManager : MonoBehaviour
{
    public static RoomCameraManager Instance { get; private set; }
    CinemachineVirtualCamera current;

    void Awake() => Instance = this;

    public void Activate(CinemachineVirtualCamera cam)
    {
        if (cam == current) return;
        if (current != null) current.Priority = 0;
        cam.Priority = 10;
        current = cam;
    }
}
