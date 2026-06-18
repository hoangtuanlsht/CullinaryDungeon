using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackArea : MonoBehaviour
{
    public Enemy enemy;
    private GruzMother gruzMother;
    private FlyEnemy flyEnemy;
    public void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        gruzMother = GetComponentInParent<GruzMother>();
        flyEnemy = GetComponentInParent<FlyEnemy>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(enemy != null && collision.CompareTag("Player"))
        {
            collision.GetComponent<Character>().OnHit(enemy.damager);
        }
        else if(collision.CompareTag("Enemy"))
        {
            collision.GetComponent<Character>().OnHit(10);
        }
        if (collision.CompareTag("Player") && gruzMother!= null)
        {
            collision.GetComponent<Character>().OnHit(gruzMother.dame);
        }
        if(collision.CompareTag("Player") && flyEnemy != null)
        {
            collision.GetComponent<Character>().OnHit(flyEnemy.dame);
        }
    }
}
