using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapManager :MonoBehaviour, Interact
{
    public CinemachineVirtualCamera map1Cam;
    public CinemachineVirtualCamera map2Cam;
    public GameObject teleport;
    public GameObject player;
    public bool isNextMap=false;
    public void InteractWithObject()
    {
        if (map1Cam != null && map2Cam != null)
        {
            map1Cam.Priority = 0;
            map2Cam.Priority = 10;
        }
        player.transform.position = teleport.transform.position;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.gameObject;
        }
        if (isNextMap) 
        {
            player.transform.position = teleport.transform.position;
            map1Cam.Priority = 0;
            map2Cam.Priority = 10;
        }
    }
}
