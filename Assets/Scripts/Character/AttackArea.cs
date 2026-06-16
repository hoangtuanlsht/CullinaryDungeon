using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackArea : MonoBehaviour
{
    public Enemy enemy;
    public void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(enemy != null && collision.CompareTag("Player"))
        {
            collision.GetComponent<Character>().OnHit(enemy.damager);
        }
        if(collision.CompareTag("Enemy"))
        {
            collision.GetComponent<Character>().OnHit(10);
        }
    }
}
