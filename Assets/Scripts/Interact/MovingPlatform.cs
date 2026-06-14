using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;
    public float speed = 2f;
    Vector3 target;
    void Start()
    {
        transform.position = pointA.position;
        target = pointB.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if(Vector3.Distance(transform.position, pointA.position) < 0.1f)
        {
            target = pointB.position;
        }
        else if(Vector3.Distance(transform.position, pointB.position) < 0.1f)
        {
            target = pointA.position;
        }
    }
}
