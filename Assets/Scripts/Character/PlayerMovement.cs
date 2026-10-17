using UnityEngine;

public class PlayerMovement
{
    [Header("reference")]
    private readonly Player owner;
    private readonly Rigidbody2D rb;
    private readonly Collider2D body;
    private readonly PlayerInputReader input;

    [Header("Ground & Slope")]
    private readonly LayerMask groundLayer;
    private readonly PhysicsMaterial2D fullFriction;
    private readonly PhysicsMaterial2D noFriction;
    private readonly float maxSlopeAngle;
    private readonly float slopeCheckDistance;
    private bool isOnSlope;
    private Vector2 slopePerpendicular;

    [Header("Raycast")]
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
    private const float StepTolerance = 0.25f;

    public bool IsGrounded { get; private set; }
    public bool IsRunning;

    [Header("Jump & Movement")]
    private bool isJumping;
    private float horizontalInput;
    private float currentSpeed;
    private bool canCutJump;      // mỗi lần nhảy chỉ được cắt 1 lần
    private bool cutRequested;    // đã nhả Space trước khi lực nhảy kịp áp
    private bool lockAnim;

    [Header("Sound")]
    private bool isPlayFootStep;
    private enum MoveState { Normal, WallSlide, WallJumpLock, PoundWindup, PoundFall, PoundLand }

    [Header("State machine (Module 7)")]
    private MoveState state = MoveState.Normal;
    private float stateTimer;             // đếm ngược cho trạng thái có thời hạn
    private int wallDir;                  // +1: tường bên phải, -1: bên trái
    private float wallCoyote;             // vừa rời tường vẫn bật được trong khoảnh khắc ngắn
    private bool wallJumpRequested;       // Update ghi nhận, FixedUpdate thực hiện
    private const float WallCoyoteTime = 0.1f;
    public bool IsPounding => state == MoveState.PoundWindup || state == MoveState.PoundFall || state == MoveState.PoundLand;
    private bool CanWallJump => state == MoveState.WallSlide || wallCoyote > 0f;


    public PlayerMovement(Player owner, Rigidbody2D rb, LayerMask groundLayer,
        PhysicsMaterial2D fullFriction, PhysicsMaterial2D noFriction,
        float maxSlopeAngle, float slopeCheckDistance, Collider2D body, PlayerInputReader input)
    {
        this.owner = owner;
        this.rb = rb;
        this.groundLayer = groundLayer;
        this.fullFriction = fullFriction;
        this.noFriction = noFriction;
        this.maxSlopeAngle = maxSlopeAngle;
        this.slopeCheckDistance = slopeCheckDistance;
        this.body = body;
        this.input = input;
    }

    /// Gọi trong Update() của Player — chỉ đọc input nhảy, y hệt logic gốc.
    public void ReadJumpInput()
    {
        if (IsGrounded && rb.velocity.y <= 0.1f)
        {
            owner.isDoubleJump = true;
        }
        if (IsPounding) return;
        if (input.JumpPressed)
        {
            if (IsGrounded)
            {
                isJumping = true;
            }
            else if (CanWallJump)                                 // MỚI: ưu tiên bật tường trước nhảy đôi
            {
                wallJumpRequested = true;
            }
            else if (owner.Abilities.Has(Ability.DoubleJump) && owner.isDoubleJump)
            {
                isJumping = true;
                owner.isDoubleJump = false;
            }
        }
        if (input.JumpReleased)
        {
            if (isJumping) cutRequested = true;                         // nhả trước khi Jump() chạy
            else if (canCutJump && rb.velocity.y > 0f) CutJump();
        }
    }

    /// Gọi trong FixedUpdate() của Player, sau khi các cờ trạng thái (dead/hurt/attack/defend)
    /// đã được tính. blockMovement = true khi Player đang dead/hurt/attack/defend — lúc đó
    /// combat/hurt handling đã tự set velocity, movement không được ghi đè lên.
    public void PhysicsTick(bool blockMovement, bool animLocked)
    {
        horizontalInput = input.MoveX;
        IsGrounded = CheckGrounded();
        lockAnim = animLocked;
        SlopeCheck();
        HandleSlopeFriction();

        if (blockMovement)
        {
            // Bị đánh / chết / đang đánh giữa chừng → huỷ trạng thái đặc biệt
            if (state != MoveState.Normal) SetState(MoveState.Normal);
            wallJumpRequested = false;
            return;
        }
        if (TickSpecialStates()) return;
        if (isJumping)
        {
            if (isPlayFootStep) StopFootStep();
            Jump();
        }

        if (IsGrounded)
        {
            if (rb.velocity.y <= 0.1f)
            {
                if (Mathf.Abs(horizontalInput) > 0.1f)
                {
                    if (!IsRunning)
                    {
                        currentSpeed = owner.speedWalk;
                        PlayAnim("Run");
                    }
                    else
                    {
                        currentSpeed = owner.speedRun;
                        PlayAnim("Fast");
                    }
                    if (!isPlayFootStep) StartFootStep();
                }
                else
                {
                    PlayAnim("Idle");
                    StopFootStep();
                }
            }
        }
        if(!IsGrounded && rb.velocity.y > 0.1f)
        {
            PlayAnim("Jump");
            if (isPlayFootStep) StopFootStep();
        }
        if (!IsGrounded && rb.velocity.y < 0)
        {
            isJumping = false;
            PlayAnim("Fall");
            if (isPlayFootStep) StopFootStep();
        }

        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            if (isOnSlope && IsGrounded && rb.velocity.y <= 0.2f)
            {
                float direction = horizontalInput > 0 ? -1f : 1f;
                rb.velocity = direction * slopePerpendicular * currentSpeed * Time.fixedDeltaTime;
            }
            else
            {
                float vx = horizontalInput * currentSpeed * Time.fixedDeltaTime;
                if (!IsGrounded && IsBlockedAhead(horizontalInput)) vx = 0f;
                rb.velocity = new Vector2(vx, rb.velocity.y);
            }
            if(!lockAnim) owner.transform.localScale = new Vector3(horizontalInput > 0 ? 1f : -1f, 1f, 1f);
        }
        else if (IsGrounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }

    /// Dùng khi Player dead/defend cần dừng hẳn (thay cho việc FixedUpdate cũ set rb.velocity = 0).
    public void StopFootStepIfPlaying()
    {
        if (isPlayFootStep) StopFootStep();
    }
    private void SetState(MoveState next, float duration = 0f)
    {
        state = next;
        stateTimer = duration;
    }
    /// Gọi từ Player.OnInit() khi respawn — reset các cờ chuyển tiếp, y như OnInit() bản gốc
    /// từng làm với isJumping.
    public void ResetTransientState()
    {
        isJumping = false;
        canCutJump = false; 
        cutRequested = false;
        state = MoveState.Normal;
        wallJumpRequested = false;
        wallCoyote = 0f;
        StopFootStepIfPlaying();
    }
    private bool CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(owner.transform.position, Vector2.down, 0.3f, groundLayer);
        return hit.collider != null;
    }
    private bool IsBlockedAhead(float dir) => IsBlockedAhead(dir, groundLayer);
    private bool IsBlockedAhead(float dir, LayerMask mask)
    {
        if (body == null) return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = false;

        float sign = Mathf.Sign(dir);
        // Chỉ quét đúng collider thân (không quét AttackArea hay trigger khác gắn trên Player)
        int count = body.Cast(new Vector2(sign, 0f), filter, wallHits, 0.05f);
        float feetY = body.bounds.min.y;

        for (int i = 0; i < count; i++)
        {
            bool facesAgainstMove = wallHits[i].normal.x * sign < -0.1f;          // tường/góc chắn phía trước
            bool aboveStep = wallHits[i].point.y - feetY > StepTolerance;          // không phải mặt dốc dưới chân
            if (facesAgainstMove && aboveStep) return true;
        }
        return false;
    }
    private void SlopeCheck()
    {
        RaycastHit2D hit = Physics2D.Raycast(owner.transform.position, Vector2.down, slopeCheckDistance, groundLayer);
        if (hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            slopePerpendicular = Vector2.Perpendicular(hit.normal).normalized;
            isOnSlope = slopeAngle > 0f && slopeAngle <= maxSlopeAngle;
        }
        else
        {
            isOnSlope = false;
        }
    }

    private void HandleSlopeFriction()
    {
        rb.sharedMaterial = (isOnSlope && IsGrounded && Mathf.Abs(horizontalInput) < 0.1f) ? fullFriction : noFriction;
    }
    public void PlayAnim(string animName)
    {
        if (lockAnim) return;
        owner.ChangedAnim(animName);
    }
    private void Jump()
    {
        PlayAnim("Jump");
        isJumping = false;
        rb.velocity = new Vector2(rb.velocity.x, 0);

        float factor = 1f;
        if (cutRequested) { cutRequested = false; canCutJump = false; factor = 0.5f; }
        else canCutJump = true;
        rb.AddForce(owner.jumpForce * factor * Vector2.up, ForceMode2D.Impulse);
    }
    private bool TickSpecialStates()
    {
        stateTimer -= Time.fixedDeltaTime;
        wallCoyote -= Time.fixedDeltaTime;

        if (state == MoveState.Normal && ShouldWallSlide()) SetState(MoveState.WallSlide);

        switch (state)
        {
            case MoveState.WallSlide:
                if (wallJumpRequested) { WallJump(); return true; }
                if (!ShouldWallSlide())
                {
                    wallCoyote = WallCoyoteTime;
                    SetState(MoveState.Normal);
                    return false;
                }
                owner.isDoubleJump = true;                                          // chạm tường trả lại nhảy đôi
                rb.velocity = new Vector2(0f, Mathf.Max(rb.velocity.y, -owner.wallSlideSpeed));
                owner.transform.localScale = new Vector3(-wallDir, 1f, 1f);
                PlayAnim("Fall");
                return true;

            case MoveState.WallJumpLock:
                if (stateTimer <= 0f) { SetState(MoveState.Normal); return false; }
                return true;                                                        // giữ vận tốc đã đặt

            case MoveState.PoundWindup:
                rb.velocity = Vector2.zero;                               // treo trên không
                if (stateTimer <= 0f) SetState(MoveState.PoundFall);
                return true;

            case MoveState.PoundFall:
                rb.velocity = new Vector2(0f, -owner.poundSpeed);
                if (IsGrounded)
                {
                    bool broke = owner.OnPoundLanded();
                    if (!broke)                                           // phá được sàn thì lao tiếp xuống dưới
                    {
                        rb.velocity = Vector2.zero;
                        SetState(MoveState.PoundLand, owner.poundLandLock);
                    }
                }
                return true;

            case MoveState.PoundLand:
                rb.velocity = new Vector2(0f, rb.velocity.y);
                if (stateTimer <= 0f) { SetState(MoveState.Normal); return false; }
                return true;

            default:    // Normal
                if (wallJumpRequested)
                {
                    if (wallCoyote > 0f) { WallJump(); return true; }
                    wallJumpRequested = false;
                }
                return false;
        
        }
    }

    /// Đang rơi + đang ấn VÀO tường + có kỹ năng. Ghi luôn wallDir khi đúng.
    private bool ShouldWallSlide()
    {
        if (!owner.Abilities.Has(Ability.WallJump)) return false;
        if (IsGrounded || rb.velocity.y > 0f) return false;
        if (Mathf.Abs(horizontalInput) < 0.1f) return false;
        if (!IsBlockedAhead(horizontalInput, owner.wallLayer)) return false;

        wallDir = (int)Mathf.Sign(horizontalInput);
        return true;
    }

    private void WallJump()
    {
        wallJumpRequested = false;
        wallCoyote = 0f;
        canCutJump = false;                                       // bật tường có độ cao cố định
        rb.velocity = new Vector2(-wallDir * owner.wallJumpVelocity.x, owner.wallJumpVelocity.y);
        owner.transform.localScale = new Vector3(-wallDir, 1f, 1f);
        PlayAnim("Jump");
        SetState(MoveState.WallJumpLock, owner.wallJumpLockTime);
    }
    public bool TryStartPound()
    {
        if (!owner.Abilities.Has(Ability.GroundPound)) return false;
        if (IsGrounded || state != MoveState.Normal) return false;
        if (input.MoveY >= 0f) return false;                      // phải đang giữ Xuống
        isJumping = false;
        wallJumpRequested = false;
        PlayAnim("Fall");
        SetState(MoveState.PoundWindup, owner.poundWindup);
        return true;
    }
    private void CutJump()
    {
        canCutJump = false;
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
    }
    private void StartFootStep()
    {
        isPlayFootStep = true;
        owner.StartFootStepLoop();
    }
    private void StopFootStep()
    {
        isPlayFootStep = false;
        owner.StopFootStepLoop();
    }
}
