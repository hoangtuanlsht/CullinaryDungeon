using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackState : IsState
{
    public void OnEnter(Enemy enemy)
    {
        enemy.StopMoving();
        enemy.Attack();
    }
    public void OnExecute(Enemy enemy)
    {
        if (enemy.IsAttacking) return;                                   // đang ra đòn thì chờ đòn xong
        enemy.StopMoving();                                              // ← đòn xong: về Idle, để lần Attack() sau bắn được trigger
        if (enemy.Target == null) { enemy.ChangeState(new PatrolState()); return; }
        if (!enemy.isAttackInRange()) { enemy.ChangeState(new PatrolState()); return; }
        if (enemy.CanAttack) enemy.Attack();
        else enemy.ChangeDirection(enemy.Target.transform.position.x > enemy.transform.position.x);
    }
    public void OnExit(Enemy enemy)
    {
    }
}
