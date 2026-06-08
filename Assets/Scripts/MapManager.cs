using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public CinemachineVirtualCamera map1Cam;
    public CinemachineVirtualCamera map2Cam;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            map1Cam.Priority = 0;
            map2Cam.Priority = 10;
        }
    }
}
