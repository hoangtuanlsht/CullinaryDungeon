using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoBindFogOfWar : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 0f, 0f);
    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if(player!=null)
        {
            transform.SetParent(player.transform);
            transform.localPosition = offset;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }

}
