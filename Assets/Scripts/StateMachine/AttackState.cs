using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : IsState
{
    public float timer;
    public void OnEnter(Enemy enemy)
    {
        if(enemy.Target != null)
        {
            //doi huong enemy toi player
            enemy.ChangeDirection(enemy.Target.transform.position.x > enemy.transform.position.x);
        }
        enemy.StopMoving();
        enemy.Attack();
        timer = 0;
    }

    public void OnExecute(Enemy enemy)
    {
        timer+= Time.deltaTime;
        if (timer >= 1.5f)
        {
            if (enemy.Target == null)
            {
                enemy.ChangeState(new IdleState());
            }
            else if (enemy.isAttackInRange())
            {
                enemy.ChangeState(new AttackState()); // Đánh tiếp
            }
            else
            {
                enemy.ChangeState(new PatrolState()); // Đuổi theo
            }

        }
    }

    public void OnExit(Enemy enemy)
    {
    }
}
