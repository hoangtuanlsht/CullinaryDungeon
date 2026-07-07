using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;

public class Boss_Slime : Enemy
{
    public GameObject nextMapPrefab;
    public GameObject backMapPrefab;
    public GameObject levelUp;
    public Player player;
    public void Awake()
    {
        player = FindObjectOfType<Player>();
    }
    public override void OnInit()
    {
        // Gọi base.OnInit() để chạy các thiết lập cơ bản của Enemy và Character
        // (Bao gồm việc set máu, tắt AttackArea, v.v.)
        base.OnInit();

        // Ngay sau đó, chuyển lập tức sang trạng thái Intro
        ChangeState(new IntroState());
    }
    public override void OnDeath()
    {
        player.damage = 12f;
        levelUp.SetActive(true);
        base.OnDeath();
        nextMapPrefab.SetActive(true);
        backMapPrefab.SetActive(true);
    }
}
