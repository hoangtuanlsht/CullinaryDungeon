using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Cinemachine.DocumentationSortingAttribute;

public class Boss_Minotaurus : Enemy
{
    public GameObject levelUp;
    public Player player;
    public void Awake()
    {
        player = FindObjectOfType<Player>();
    }
    public override void OnInit()
    {
        base.OnInit();
    }
    public override void OnDeath()
    {
        player.jumpForce = 10f;
        player.damage = 12f;
        levelUp.SetActive(true);
        base.OnDeath();

    }
}
