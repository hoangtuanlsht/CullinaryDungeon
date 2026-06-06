using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PatrolState : IsState
{
    float randomTime;
    float timer;
    public void OnEnter(Enemy enemy)
    {
        timer = 0f;
        randomTime = Random.Range(2.5f, 5.5f);
    }

    public void OnExecute(Enemy enemy)
    {
        timer += Time.deltaTime;
        if(enemy.Target != null)
        {
            //doi huong enemy theo target
            enemy.ChangeDirection(enemy.Target.transform.position.x > enemy.transform.position.x);
            if (enemy.isAttackInRange())
            {
                enemy.ChangeState(new AttackState());
                return;
            }
            else
            {
                enemy.Moving();
            }
        }
        else
        {
            if (timer < randomTime)
            {
                enemy.Moving();
            }
            else
            {
                enemy.ChangeState(new IdleState());
            }
        }       
    }

    public void OnExit(Enemy enemy)
    {
    }
}
