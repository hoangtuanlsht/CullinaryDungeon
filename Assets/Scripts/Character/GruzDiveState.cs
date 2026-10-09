using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GruzDiveState : IGruzState
{
    private bool hasStartedDive;

    public void OnEnter(GruzMother boss)
    {
        hasStartedDive = false;
        boss.TriggerAnim("AttackPlayer");
    }

    public void OnExecute(GruzMother boss)
    {
        boss.AttackPlayer();

        if (boss.HasPlayerPos)
        {
            hasStartedDive = true;
        }
        else if (hasStartedDive)
        {
            // AttackPlayer() đã tự đặt hasPlayerPos = false khi chạm tường/đất -> cú lao xong
            boss.ChangeState(new GruzPatrolState());
        }
        boss.FlipTowardsPlayer();
    }

    public void OnExit(GruzMother boss) { }
}
