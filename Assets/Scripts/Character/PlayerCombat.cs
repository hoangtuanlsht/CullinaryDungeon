using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat
{
    private readonly Player owner;
    private readonly GameObject attackArea;
    private const float AttackDuration = 0.5f;
    private const float DefendDuration = 0.5f;

    private Cooldown attackCd;         // KHÔNG đặt readonly: struct readonly thì Start() chạy trên bản sao
    private Cooldown defendCd;
    private Coroutine attackRoutine;
    private Coroutine defendRoutine;
    private readonly float attackCooldown;
    private readonly float defendCooldown;
    private readonly List<string> attackList = new List<string> { "Attack", "Attack1", "Attack2" };
    public bool IsAttacking { get; private set; }
    public bool IsDefending { get; private set; }

    public PlayerCombat(Player owner, GameObject attackArea, float attackCooldown, float defendCooldown)
    {
        this.owner = owner;
        this.attackArea = attackArea;
        this.attackCooldown = attackCooldown;
        this.defendCooldown = defendCooldown;
    }
    /// Điều kiện gọi (phím C, !isDefending) vẫn nằm ở Player.Update() như bản gốc;
    /// hàm này chỉ giữ đúng phần thân logic của Attack() cũ.
    public void TryAttack()
    {
        if (!attackCd.IsReady) return;
        attackCd.Start(attackCooldown);
        SoundManager.Play("Attack");
        owner.ChangedAnim(attackList[Random.Range(0, attackList.Count)]);

        IsAttacking = true;
        Stop(ref attackRoutine);
        attackRoutine = owner.StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(AttackDuration);
        IsAttacking = false;
        if (attackArea != null) attackArea.SetActive(false);
        attackRoutine = null;
    }
    /// Gọi từ Animation Event (qua Player). Chỉ có tác dụng khi đang trong một đòn đánh.
    public void SetHitbox(bool on)
    {
        if (attackArea != null) attackArea.SetActive(on && IsAttacking);
    }

    /// Điều kiện gọi (phím F giữ, isGrounded, !isAttacking, !isHurt, !isDead, !isDefending)
    /// vẫn được Player.Update() kiểm tra trước khi gọi hàm này, y như bản gốc.
    public void TryDefend()
    {
        if (!defendCd.IsReady) return;
        defendCd.Start(defendCooldown);
        IsDefending = true;
        owner.ChangedAnim("Defend");
        SoundManager.Play("Defend");
        Stop(ref defendRoutine);
        defendRoutine = owner.StartCoroutine(DefendRoutine());
    }

    private IEnumerator DefendRoutine()
    {
        yield return new WaitForSeconds(DefendDuration);
        IsDefending = false;
        defendRoutine = null;
    }

    /// Dùng khi OnInit() cần đảm bảo attackArea tắt, isAttacking/isDefending về false ngay lập
    /// tức (tương đương DeactiveAttack() + reset isAttacking/isDefending trong OnInit() bản gốc).
    public void ForceIdle()
    {
        Stop(ref attackRoutine);
        Stop(ref defendRoutine);
        IsAttacking = false;
        IsDefending = false;
        if (attackArea != null) attackArea.SetActive(false);
    }

    private void Stop(ref Coroutine routine)
    {
        if (routine != null) { owner.StopCoroutine(routine); routine = null; }
    }
}
