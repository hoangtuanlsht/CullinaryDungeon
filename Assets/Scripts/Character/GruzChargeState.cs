using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GruzChargeState : IGruzState
{
    private float timer;
    private float randomTime;

    public void OnEnter(GruzMother boss)
    {
        timer = 0f;
        randomTime = Random.Range(2f, 3f);
        boss.TriggerAnim("AttackUpAndDown");
        boss.FlipTowardsPlayer();
    }

    public void OnExecute(GruzMother boss)
    {
        boss.AttackUpAndDownState();
        timer += Time.deltaTime;
        if (timer >= randomTime)
        {
            boss.ChangeState(new GruzDiveState());
        }
    }

    public void OnExit(GruzMother boss) { }
}
