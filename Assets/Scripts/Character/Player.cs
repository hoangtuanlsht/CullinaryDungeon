using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.RuleTile.TilingRuleOutput;

[RequireComponent(typeof(PlayerInputReader))]
public class Player : Character
{
    [Header("Movement")]
    public float speedWalk = 5f;
    public float speedRun = 5f;
    public float jumpForce = 10f;
    public bool isDoubleJump = true;
    public float footStepSpeed = 0.5f;
    public LayerMask groundLayer;
    [SerializeField] private CapsuleCollider2D bodyCollider;
    private PlayerInputReader input;


    [Header("Combat")]
    public float damage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float defendCooldown = 2f;


    [Header("State Flags")]
    private bool isGrounded = true;
    private bool isDead = false;
    private bool isHurt = false;

    [Header("References")]
    [SerializeField] public Rigidbody2D rb;
    [SerializeField] private GameObject attackArea;
    [SerializeField] public GameObject interactUI;
    [SerializeField] private InventoryManager recycleableInventoryManager;
    [SerializeField] private GameObject cooking;

    [Header("Respawn")]
    [SerializeField] private Vector3 currentRespawnPosition;
    private string currentRespawnCameraName; // Ten VCam tuong ung voi Save Point hien tai
    private Coroutine respawnRoutine;
    private Coroutine hurtRoutine;

    [Header("Slope")]
    [SerializeField] private PhysicsMaterial2D fullFriction;
    [SerializeField] private PhysicsMaterial2D noFriction;
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slopeCheckDistance = 0.5f;


    [Header("Knockback")]
    [SerializeField] private float knockbackForceX = 3f;

    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerInteraction interaction;
    private Coroutine footStepLoop;


    [Header("Abilities")]
    [SerializeField] private PlayerAbilities abilities = new PlayerAbilities();
    public PlayerAbilities Abilities => abilities;

    [Header("Wall Jump (Module 7)")]
    public LayerMask wallLayer;                                  // tường nào bám được
    public float wallSlideSpeed = 2f;                            // ô/giây khi trượt
    public Vector2 wallJumpVelocity = new Vector2(4f, 8f);       // x: bật ra khỏi tường, y: bật lên
    public float wallJumpLockTime = 0.15f;                       // bỏ qua phím ngang trong lúc này
    private bool controlLocked;

    [Header("Ground Pound (Module 7)")]
    public float poundWindup = 0.15f;        // khựng lại trên không trước khi lao xuống
    public float poundSpeed = 20f;
    public float poundLandLock = 0.2f;       // đứng khựng sau khi chạm đất
    [SerializeField] private float poundDamage = 20f;
    [SerializeField] private float poundRadius = 1.5f;
    [SerializeField] private LayerMask poundHitLayer;            // tick Ground + layer của Enemy
    private readonly Collider2D[] poundHits = new Collider2D[16];

    [Header("Hunger(Module 8)")]
    [SerializeField] private PlayerHunger playerHunger = new PlayerHunger();
    public PlayerHunger PlayerHunger => playerHunger;
    [SerializeField] private float starveDamagePerSecond = 2f;   // mất máu mỗi giây khi Starving
    [SerializeField] private float fullRegenPerSecond = 1f;      // hồi máu mỗi giây khi Full
    [SerializeField] [Range(0f,1f)] private float starveHealthFloor = 0.2f;     // sàn: 20% máu tối đa
    private float timeHungerEffect = 0f;              // đếm thời gian để áp dụng hiệu ứng đói
    [SerializeField][Range(0f, 1f)] float respawnHungerRatio = 0.5f;
    public void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // trong Awake(), ngay sau rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInputReader>();
        PlayerWallet.Load();
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
        if (recycleableInventoryManager == null) Debug.LogError("Chưa gán InventoryManager!", this);

        // dòng tạo movement: thêm tham số cuối
        movement = new PlayerMovement(this, rb, groundLayer, fullFriction, noFriction, maxSlopeAngle, slopeCheckDistance, bodyCollider, input);
        combat = new PlayerCombat(this, attackArea, attackCooldown, defendCooldown);
        interaction = new PlayerInteraction(interactUI, recycleableInventoryManager);

        abilities.OnUnlocked += a => Debug.Log($"Mở khóa kỹ năng: {a}");

    }
    void Update()
    {
        if (controlLocked) return;
        movement.ReadJumpInput();

        if (input.AttackPressed && !combat.IsDefending && !movement.IsPounding)
        {
            if (!movement.TryStartPound()) combat.TryAttack();     // trên không + giữ Xuống → dậm; còn lại → đánh thường
        }
        if (input.DefendHeld && isGrounded && !combat.IsAttacking && !isHurt && !isDead && !combat.IsDefending)
        {
            combat.TryDefend();
        }
        if (input.CraftPressed && isGrounded && !combat.IsAttacking && !combat.IsDefending && !isDead)
        {
            cooking.SetActive(!cooking.activeSelf);
        }
        if (input.InteractPressed)
        {
            interaction.TryInteract();
        }
        if (PauseController.IsGamePause)
        {
            rb.velocity = Vector2.zero;
            movement.StopFootStepIfPlaying();
            return;
        }
        movement.IsRunning = input.RunHeld;
        if (!isDead)
        {
            playerHunger.UpdateHungerLevel(Time.deltaTime);
            ApplyHungerEffects(Time.deltaTime);
        }        
    }
    void FixedUpdate()
    {
        if (controlLocked) return;
        bool blockMovement = isDead || isHurt || combat.IsDefending || combat.IsAttacking;
        movement.PhysicsTick(blockMovement);
        float maxFallSpeed = movement.IsPounding ? poundSpeed : 15f; if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        }
        isGrounded = movement.IsGrounded;

        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        if (isHurt)
        {
            return;
        }
        if (combat.IsDefending)
        {
            rb.velocity = Vector2.zero;
            ChangedAnim("Defend");
            return;
        }
        if (combat.IsAttacking)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
            return;
        }
    }
    public override void OnInit()
    {
        base.OnInit();
        isDead = false;
        if (hurtRoutine != null) { StopCoroutine(hurtRoutine); hurtRoutine = null; }
        isHurt = false;
        ChangedAnim("Idle");
        combat.ForceIdle();
        movement.ResetTransientState();
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
    }
    public override void OnDeath()
    {
        base.OnDeath();
        isDead = true;
        //Invoke("OnInit",2f);
        ScheduleRespawn(2f);
    }
    public override void OnHit(float damage, UnityEngine.Transform attacker)
    {
        if (combat.IsDefending)
        {
            Debug.Log("Da do don! Khong nhan sat thuong.");
            return;
        }
        base.OnHit(damage, attacker);
        if (!IsDead)
        {
            Debug.Log("Player bi thuong!");
            ChangedAnim("Hurt");
            isHurt = true;

            rb.velocity = Vector2.zero;
            float knockbackDirection = transform.position.x < attacker.position.x ? -1f : 1f;
            rb.AddForce(new Vector2(knockbackDirection * knockbackForceX, 0), ForceMode2D.Impulse);
            if (hurtRoutine != null) StopCoroutine(hurtRoutine);
            hurtRoutine = StartCoroutine(HurtRoutine());
        }
    }
    public void AnimEvent_HitboxOn() => combat.SetHitbox(true);
    public void AnimEvent_HitboxOff() => combat.SetHitbox(false);
    /// PlayerMovement gọi khi cú dậm chạm đất. Trả về true nếu có sàn bị phá (để lao tiếp).

    internal bool OnPoundLanded()
    {
        SoundManager.Play("Attack");         // tạm dùng tiếng có sẵn
        bool broke = false;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, poundRadius, poundHits, poundHitLayer);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = poundHits[i];
            if (c.TryGetComponent(out BreakableFloor floor))
            {
                floor.Break();
                broke = true;
            }
            else if (c.CompareTag("Enemy") && c.TryGetComponent(out Character enemy))
            {
                enemy.OnHit(poundDamage, transform);
            }
        }
        return broke;
    }
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        SetHealth(health + amount);
    }
    public void Feed(float amount)
    {
        if (IsDead || amount <= 0f) return;
        PlayerHunger.EatFood(amount);
    }
    public bool TryConsumeFood(ConsumableItem item)
    {
        if(item == null || isDead) return false;
        bool canHeal = item.healthRecovery > 0f && health < MaxHealth;
        bool canFeed = item.hungerRecovery > 0f && !PlayerHunger.IsFull;

        if(!canHeal && !canFeed) return false;
        Feed(item.hungerRecovery);
        Heal(item.healthRecovery);
        return true;
    }
    private void ApplyHungerEffects(float deltaTime)
    {
        if (playerHunger.IsPaused) return;
        HungerState state = playerHunger.HungerState;
        if(state != HungerState.Full && state != HungerState.Starving)
        {
            timeHungerEffect = 0f;
            return;
        }
        timeHungerEffect += deltaTime;
        if(timeHungerEffect < 1f) return;        // áp dụng mỗi giây
        timeHungerEffect -= 1f;

        if (state == HungerState.Starving)
        {
            float floor = maxhealth * starveHealthFloor;
            if(health > floor)
            {
                float newHealth = health - starveDamagePerSecond;
                SetHealth(Mathf.Max(newHealth, floor));
            }
        }
        else if(state == HungerState.Full)
        {
            if(health < maxhealth)
            {
                SetHealth(health + fullRegenPerSecond);
            }
        }
    }
    // Cac ham noi bo duoi day duoc PlayerMovement/PlayerCombat (plain class, khong co Invoke/
    // InvokeRepeating rieng) nho Player thuc thi ho - giu dung co che timing goc.
    internal void StartFootStepLoop()
    {
        StopFootStepLoop();
        footStepLoop = StartCoroutine(FootStepRoutine());
    }
    internal void StopFootStepLoop()
    {
        if (footStepLoop != null) { StopCoroutine(footStepLoop); footStepLoop = null; }
    }
    private IEnumerator FootStepRoutine()
    {
        var wait = new WaitForSeconds(footStepSpeed);      // tạo 1 lần, không cấp phát lại mỗi vòng
        while (true)
        {
            SoundManager.Play(movement.IsRunning ? "Run" : "Step");   // sửa lỗi 3: hỏi lại mỗi nhịp
            yield return wait;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Coin"))
        {
            PlayerWallet.Add(1);
            PoolManager.Despawn(collision.gameObject);
        }
        if (collision.CompareTag("DeadZone"))
        {
            //health = 0;
            ChangedAnim("Dead");
            isDead = true;
            Debug.Log("Dead");
            //Invoke("OnInit", 2f);
            ScheduleRespawn(2f);
        }
        interaction.HandleTriggerEnter(collision);
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        interaction.HandleTriggerExit(collision);
    }
    private void RespawnAtSavePoint()
    {
        OnInit();
        playerHunger.AtLeastHunger(respawnHungerRatio);
        transform.position = currentRespawnPosition;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        StartCoroutine(ReloadMapAndRespawn());
    }
    public void UpdateSavePoint(Vector3 newSavePosition, string cameraName)
    {
        currentRespawnPosition = newSavePosition;

        if (!string.IsNullOrEmpty(cameraName))
        {
            currentRespawnCameraName = cameraName;
        }
        Debug.Log("Da cap nhat diem hoi sinh moi tai: " + currentRespawnPosition + " | Camera: " + currentRespawnCameraName);
    }
    private IEnumerator ReloadMapAndRespawn()
    {
        string currentSceneName = "";
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.name != "PersistentGameplay")
            {
                currentSceneName = scene.name;
                break;
            }
        }
        if (string.IsNullOrEmpty(currentSceneName))
        {
            Debug.LogWarning("Khong tim thay scene map nao de load lai!");
        }
        else
        {
            yield return SceneManager.UnloadSceneAsync(currentSceneName);
            yield return SceneManager.LoadSceneAsync(currentSceneName, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(currentSceneName));
        }

        transform.position = currentRespawnPosition;

        RestoreVirtualCameraForSavePoint();

        Debug.Log("Da tai lai map va hoi sinh thanh cong tai Save Point!" + currentSceneName);
    }
    private void RestoreVirtualCameraForSavePoint()
    {
        CinemachineVirtualCamera[] allCams = FindObjectsOfType<CinemachineVirtualCamera>();

        if (allCams.Length == 0 || string.IsNullOrEmpty(currentRespawnCameraName)) return;

        bool found = false;

        foreach (CinemachineVirtualCamera cam in allCams)
        {
            if (cam.name == currentRespawnCameraName)
            {
                cam.Priority = 10;
                found = true;
                Debug.Log("Da khoi phuc Virtual Camera theo Save Point: " + cam.name);
            }
            else
            {
                cam.Priority = 0;
            }
        }

        if (!found)
        {
            Debug.LogWarning("Khong tim thay Virtual Camera ten: " + currentRespawnCameraName + " sau khi reload scene.");
        }
    }
    private void ScheduleRespawn(float delay)
    {
        if (respawnRoutine != null) return;                // đã hẹn rồi thì không hẹn lần hai
        respawnRoutine = StartCoroutine(RespawnAfter(delay));
    }

    private IEnumerator RespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        respawnRoutine = null;
        RespawnAtSavePoint();
    }

    private IEnumerator HurtRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        isHurt = false;
        hurtRoutine = null;
    }
    public void BeginTransition()
    {
        input.SetGameplayEnabled(false);
        controlLocked = true;
        interaction.ClearAll();              // cửa/NPC ở scene cũ sắp bị huỷ, không được giữ tham chiếu
        movement.StopFootStepIfPlaying();
        rb.velocity = Vector2.zero;
        rb.simulated = false;                // không rơi trong lúc scene đang load
    }

    public void EndTransition(Vector3 position, ExitDirection dir, Vector2 velocityBefore)
    {
        transform.position = position;
        rb.simulated = true;

        if (dir == ExitDirection.Left || dir == ExitDirection.Right)
        {
            float sign = dir == ExitDirection.Left ? -1f : 1f;
            transform.localScale = new Vector3(sign, 1f, 1f);          // giữ hướng nhìn
            rb.velocity = new Vector2(sign * Mathf.Abs(velocityBefore.x), 0f);
        }
        else if (dir == ExitDirection.Up)
        {
            rb.velocity = new Vector2(0f, jumpForce);                  // chui lên từ lỗ sàn → bật lên qua mép (không bị cắt nhảy nếu đã sửa theo A1 mục 3)
        }
        else // Down
        {
            rb.velocity = new Vector2(0f, Mathf.Min(velocityBefore.y, 0f)); // giữ đà rơi
        }
        input.SetGameplayEnabled(true);
        controlLocked = false;
    }
}

