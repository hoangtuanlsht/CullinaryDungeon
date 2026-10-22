using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat
{
    private readonly Player owner;
    private readonly GameObject attackArea;
    private const float AttackDuration = 0.35f;
    private const float DefendDuration = 0.5f;

    private Cooldown attackCd;         // KHÔNG đặt readonly: struct readonly thì Start() chạy trên bản sao
    private Cooldown defendCd;
    private Coroutine attackRoutine;
    private Coroutine defendRoutine;
    private readonly float attackCooldown;
    private readonly float defendCooldown;
    private const float AttackBufferWindow = 0.15f;                 // (b) cửa sổ buffer
    private float lastAttackPressTime = float.NegativeInfinity;     // (a) bộ nhớ: thời điểm bấm gần nhất
    private int attackIndex = 0;
    private const float resetAttackIndexTime = 1f;
    private float lastAttackTime = 0f;
    private bool HasBufferedAttack => Time.time - lastAttackPressTime <= AttackBufferWindow;
    private bool hasHitThisSwing = false;

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
    /// Player gọi khi bấm phím Chém: CHỈ ghi nhớ ý định, không chém ngay.
    public void BufferAttack()
    {
        lastAttackPressTime = Time.time;
    }

    /// Xoá ý định đang nhớ (bị đau, chết, respawn...).
    public void ClearAttackBuffer()
    {
        lastAttackPressTime = float.NegativeInfinity;
    }

    /// Player gọi MỖI FRAME. canAttack = các điều kiện bên ngoài (không defend, không pound, không đau...).
    public void Tick(bool canAttack)
    {
        if (!canAttack) return;
        if (!HasBufferedAttack) return;       // không có ý định nào còn mới
        if (!attackCd.IsReady) return;        // còn cooldown → giữ ý định lại, frame sau thử tiếp
        ClearAttackBuffer();                  // (c) tiêu thụ: xoá TRƯỚC khi chém
        PerformAttack();
    }

    private void PerformAttack()
    {
        attackCd.Start(attackCooldown);
        hasHitThisSwing = false;
        SoundManager.Play("Attack");
        if (attackIndex >= attackList.Count || Time.time - lastAttackTime > resetAttackIndexTime)
        {
            attackIndex = 0;
        }
        owner.ChangedAnim(attackList[attackIndex]);
        attackIndex = (attackIndex + 1) % attackList.Count;
        lastAttackTime = Time.time;
        IsAttacking = true;
        if (attackArea != null) attackArea.SetActive(false);   // tắt hitbox của đòn trước nếu còn sót
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
    public bool TryRegisterHit()
    {
        // nếu đã trúng rồi → trả về false
        if(hasHitThisSwing) return false;
        // nếu chưa → đánh dấu đã trúng, trả về true
        hasHitThisSwing = true;
        return true;
        
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
        hasHitThisSwing = false;
        if (attackArea != null) attackArea.SetActive(false);
        ClearAttackBuffer();
    }
    private void Stop(ref Coroutine routine)
    {
        if (routine != null) { owner.StopCoroutine(routine); routine = null; }
    }
}
