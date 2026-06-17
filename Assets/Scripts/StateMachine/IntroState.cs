using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IntroState : IsState
{
    private float introTimer;
    private float introDuration = 3f;

    public void OnEnter(Enemy enemy)
    {
        enemy.ChangedAnim("Intro");

        // Bắt đầu đếm giờ
        introTimer = introDuration;
    }

    public void OnExecute(Enemy enemy)
    {
        introTimer -= Time.deltaTime;

        // Khi hết thời gian Intro, chuyển sang trạng thái Idle
        if (introTimer <= 0)
        {
            enemy.ChangeState(new IdleState());
        }
    }

    public void OnExit(Enemy enemy)
    {
    }
}
