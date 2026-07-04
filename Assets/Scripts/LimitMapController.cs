using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LimitMapController : MonoBehaviour
{
    public GameObject limitMap;
    public GameObject limitMap1;
    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            limitMap.SetActive(limitMap.activeSelf);
            limitMap1.SetActive(limitMap1.activeSelf);
        }
    }
}
