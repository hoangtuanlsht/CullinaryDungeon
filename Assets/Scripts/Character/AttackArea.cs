using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackArea : MonoBehaviour
{
    public Enemy enemy;
    private GruzMother gruzMother;
    private FlyEnemy flyEnemy;
    private Player player;
    public void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        gruzMother = GetComponentInParent<GruzMother>();
        flyEnemy = GetComponentInParent<FlyEnemy>();
        player = GetComponentInParent<Player>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(enemy != null && collision.CompareTag("Player"))
        {
            collision.GetComponent<Character>().OnHit(enemy.damager,transform.parent);
        }
        else if(collision.CompareTag("Enemy") && player != null)
        {
            collision.GetComponent<Character>().OnHit(player.damage, transform.parent);
        }
        if (collision.CompareTag("Player") && gruzMother!= null)
        {
            collision.GetComponent<Character>().OnHit(gruzMother.dame, transform.parent);
        }
        if(collision.CompareTag("Player") && flyEnemy != null)
        {
            collision.GetComponent<Character>().OnHit(flyEnemy.dame, transform.parent);
        }
    }
}
