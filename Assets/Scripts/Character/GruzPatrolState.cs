using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GruzPatrolState : IGruzState
{
    private float timer;
    private float randomTime;

    public void OnEnter(GruzMother boss)
    {
        timer = 0f;
        randomTime = Random.Range(3f, 5f);
        boss.TriggerAnim("Idle"); // MỚI: đảm bảo animation quay đúng về Idle,
                                  // không bị kẹt ở frame cuối AttackPlayer
    }

    public void OnExecute(GruzMother boss)
    {
        boss.IdleState();
        timer += Time.deltaTime;
        if (timer >= randomTime)
        {
            boss.ChangeState(new GruzChargeState());
        }
    }

    public void OnExit(GruzMother boss) { }
}