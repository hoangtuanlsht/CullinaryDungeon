using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Player : Character
{
    [Header("Movement")]
    public float speedWalk = 5f;
    public float speedRun = 5f;
    public float jumpForce = 10f;
    public bool isDoubleJump = true;
    public float footStepSpeed = 0.5f;
    public LayerMask groundLayer;
    private float currentSpeed;
    private float horizontalInput;
    private float verticalInput;

    [Header("Combat")]
    public float damage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float defendCooldown = 2f;
    [SerializeField] private float knockbackForceX = 3f;
    private float attackTimer;
    private float defendTimer;

    [Header("Health & Healing")]
    public float healHealth;

    [Header("State Flags")]
    private bool isGrounded = true;
    private bool isJumping = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private bool isHurt = false;
    private bool isDefending = false;
    private bool isRunning = false;
    private bool isPlayFootStep = false;

    [Header("References")]
    [SerializeField] public Rigidbody2D rb;
    [SerializeField] private GameObject attackArea;
    [SerializeField] public GameObject interactUI;
    [SerializeField] private Interact currentInteract;
    [SerializeField] private ItemClass currentItem;
    [SerializeField] private InventoryManager recycleableInventoryManager;
    [SerializeField] private GameObject cooking;
    [SerializeField] private ShopNPC currentShopNPC;

    [Header("Respawn")]
    [SerializeField] private Vector3 currentRespawnPosition;
    private string currentRespawnCameraName; // Tên VCam tương ứng với Save Point hiện tại

    [Header("Slope")]
    [SerializeField] private PhysicsMaterial2D fullFriction;
    [SerializeField] private PhysicsMaterial2D noFriction;
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slopeCheckDistance = 0.5f;
    private bool isOnSlope = false;
    private float slopeAngle;
    private Vector2 slopePerpendicular;

    [Header("Economy")]
    [SerializeField] public static int coin = 0;
    private List<string> attackList = new List<string>() { "Attack", "Attack1", "Attack2" };
    public void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coin = PlayerPrefs.GetInt("coin", 0);
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
        recycleableInventoryManager = GameObject.Find("InventoryManager").GetComponent<InventoryManager>();

    }
    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
        if (defendCooldown>0)
        {
            defendTimer -= Time.deltaTime;
        }
        //Jumping
        if(isGrounded && rb.velocity.y <= 0.1f)
        {
            isDoubleJump = true;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
            {
                isJumping = true;
            }
            else if (isDoubleJump)
            {
                isJumping = true;
                isDoubleJump = false;
            }
        }
        //attack
        if (Input.GetKeyDown(KeyCode.C) &&!isDefending)
        {
            Attack();
            
        }
        if (Input.GetKey(KeyCode.F) && isGrounded && !isAttacking && !isHurt && !isDead && !isDefending)
        {
            Defend();
        }
        if (Input.GetKeyDown(KeyCode.G) && isGrounded && !isAttacking &&!isDefending && !isDead)
        {
            bool isCooking = !cooking.activeSelf;
            cooking.SetActive(isCooking);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
        if (PauseController.IsGamePause)
        {
            rb.velocity = Vector2.zero;
            StopFootStep();
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            isRunning = true;
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            isRunning = false;
        }
        
    }   
    void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        SlopeCheck();
        HandleSlopeFriction();
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        if (isHurt)
        {
            return;
        }
        if (isDefending)
        {
            rb.velocity = Vector2.zero;
            
            ChangedAnim("Defend"); 
            return;
        }
        if (isAttacking)
        {
            rb.velocity = new Vector2(0,rb.velocity.y);
            return;
        }
        if (isJumping)
        {
            if (isPlayFootStep) StopFootStep();
            Jump();
            //return;
        }
        if (isGrounded)
        {
            //Change run
            if (rb.velocity.y <= 0.1f)
            {
                //Change run
                if (Mathf.Abs(horizontalInput) > 0.1f )
                {
                    if (isRunning == false)
                    {
                        currentSpeed = speedWalk;
                        ChangedAnim("Run");
                        if (!isPlayFootStep || !IsInvoking(nameof(PlayFootStep)))
                        {
                            StopFootStep();
                            StartFootStep();
                        }
                    }
                    if(isRunning == true)
                    {
                        currentSpeed = speedRun;
                        ChangedAnim("Fast");
                        if (!isPlayFootStep || !IsInvoking(nameof(PlayFootRun)))
                        {
                            StopFootStep();
                            StartFootStep();
                        }
                    }
                                  
                }
                else
                {
                    ChangedAnim("Idle");
                    StopFootStep();
                }
            }
        }

        //change fall
        if (!isGrounded && rb.velocity.y < 0)
        {
            isJumping = false;
            ChangedAnim("Fall");
            if (isPlayFootStep) StopFootStep();
        }

        //Moving
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            if (isOnSlope && isGrounded && rb.velocity.y <= 0.1f)
            {
                float direction = horizontalInput > 0 ? -1f : 1f;
                rb.velocity = direction * slopePerpendicular * currentSpeed * Time.fixedDeltaTime;
            }
            else
            {
                rb.velocity = new Vector2(horizontalInput * currentSpeed * Time.fixedDeltaTime, rb.velocity.y);
            }
            if (horizontalInput > 0)
                transform.localScale = new Vector3(1f, 1f, 1f);
            else if (horizontalInput < 0)
                transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        else if (isGrounded)
        {
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }
    public override void OnInit()
    {
        base.OnInit();
        isJumping = false;
        isAttacking = false;
        isDead = false;
        isHurt = false;
        isDefending = false;
        ChangedAnim("Idle");
        DeactiveAttack();
        UIManager.instance.SetCoin(coin);
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
    }
    public override void OnDeath()
    {
        base.OnDeath();
        //Invoke("OnInit",2f);
        Invoke("RespawnAtSavePoint", 2f);
    }
    public override void OnHit(float damage, UnityEngine.Transform attacker)
    {
        if (isDefending)
        {
            Debug.Log("Đã đỡ đòn! Không nhận sát thương.");
            return;
        }
        base.OnHit(damage, attacker);
        if (!IsDead)
        {
            Debug.Log("Player bị thương!");
            ChangedAnim("Hurt");
            isHurt = true;

            rb.velocity = Vector2.zero;
            float knockbackDirection = transform.position.x < attacker.position.x ? -1f : 1f;
            rb.AddForce(new Vector2(knockbackDirection * knockbackForceX, 0),ForceMode2D.Impulse);
            Invoke(nameof(ResetHurt), 0.5f);
        }
    }
    public void Healing()
    {
        if (health+healHealth <=maxhealth)
        {
            health += healHealth;
            healthBar.SetNewHP(health);
        }
        
    }
    private void ResetHurt()
    {
        isHurt = false;
    }
    private bool CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.3f, groundLayer);
        
        if (hit.collider != null)
        {
            return true;
        }
        return false;
    }
    private void SlopeCheck()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, slopeCheckDistance, groundLayer);
        if (hit)
        {
            slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            slopePerpendicular = Vector2.Perpendicular(hit.normal).normalized;
            isOnSlope = slopeAngle > 0f && slopeAngle <= maxSlopeAngle;
        }
        else
        {
            isOnSlope = false;
            slopeAngle = 0f;
        }
    }
    private void HandleSlopeFriction()
    {
        if (isOnSlope && isGrounded && Mathf.Abs(horizontalInput) < 0.1f)
        {
            rb.sharedMaterial = fullFriction;
        }
        else
        {
            rb.sharedMaterial = noFriction;
        }
    }
    private void Jump() 
    {
        ChangedAnim("Jump");
        isJumping = false;
        rb.velocity = new Vector2(rb.velocity.x,0);
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);
    }
    private void Attack()
    {
        if (attackTimer > 0)
        {
            ChangedAnim("Idle");
            return;
        }
        attackTimer = attackCooldown;
        SoundManager.Play("Attack");
        RandomAttack(attackList);
        
        isAttacking = true;
        Invoke("ResetAttack", 0.5f);
        ActiveAttack();
        Invoke("DeactiveAttack", 0.5f);
    }

    private void RandomAttack(List<string> attackList)
    {
        int index = UnityEngine.Random.Range(0, attackList.Count);
        ChangedAnim(attackList[index]);
    }

    private void ResetAttack()
    {
        isAttacking = false;
    }
    private void ActiveAttack()
    {
        attackArea.SetActive(true);
    }
    private void DeactiveAttack()
    {
        attackArea.SetActive(false);
    }
    private void Defend()
    {
        if (defendTimer > 0)
        {
            ChangedAnim("Idle");
            return;
        }
        defendTimer = defendCooldown;
        isDefending = true;
        ChangedAnim("Defend");
        SoundManager.Play("Defend");
        Invoke("ResetDefend", 0.5f);
    }
    private void ResetDefend()
    {
        isDefending = false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Coin"))
        {
            coin++;
            PlayerPrefs.SetInt("coin", coin);
            UIManager.instance.SetCoin(coin);
            Destroy(collision.gameObject);
        }
        if(collision.CompareTag("DeadZone"))
        {
            //health = 0;
            ChangedAnim("Dead");
            isDead = true;
            Debug.Log("Dead");
            //Invoke("OnInit", 2f);
            Invoke("RespawnAtSavePoint", 2f);
        }
        Interact interact = collision.GetComponent<Interact>();
        if (interact != null)
        {
            currentInteract = interact;
            if (interactUI != null)
            {
                interactUI.SetActive(true);
                interactUI.transform.position = collision.transform.position + new Vector3(0, 0.5f,0);
            }
        }
        ItemClass item = collision.GetComponent<ItemClass>();
        if (item != null) 
        {
            currentItem = item;
            interactUI.SetActive(true);
            interactUI.transform.position = currentItem.transform.position + new Vector3(0, 0.5f, 0);
        }
        ShopNPC shop = collision.GetComponent<ShopNPC>();
        if (shop != null)
        {
            currentShopNPC = shop;
            if (interactUI != null)
            {
                interactUI.SetActive(true);
                interactUI.transform.position = collision.transform.position + new Vector3(0, 0.5f, 0);
            }
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        Interact interact = collision.GetComponent<Interact>();
        if (interact != null && interact == currentInteract)
        {
            currentInteract = null;
            if (interactUI != null) interactUI.SetActive(false);
        }
        ItemClass item = collision.GetComponent <ItemClass>();
        if (item != null && item == currentItem)
        {
            currentItem = null;
            if(interactUI != null) interactUI.SetActive(false);
        }
        ShopNPC shop = collision.GetComponent<ShopNPC>();
        if (shop != null && shop == currentShopNPC)
        {
            currentShopNPC = null;
            if (interactUI != null) interactUI.SetActive(false);

            // Tự động đóng Shop nếu người chơi đi ra xa
            if (ShopController.instance != null) ShopController.instance.CloseShop();
        }
    }
    private void TryInteract()
    {
        if (currentInteract != null)
        {
            currentInteract.InteractWithObject();
        }
        if(currentItem != null)
        {
            recycleableInventoryManager.AddItem(currentItem.itemData, currentItem.amount);
            Destroy(currentItem.gameObject);

            currentItem = null;
            if(interactUI != null) interactUI.SetActive(false);
        }
        if (currentShopNPC != null)
        {
            currentShopNPC.Interact(); // Gọi hàm mở giao diện Shop
        }
    }
    private void RespawnAtSavePoint()
    {
        OnInit(); 

        transform.position = currentRespawnPosition;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        StartCoroutine(ReloadMapAndRespawn());
    }
    public void UpdateSavePoint(Vector3 newSavePosition)
    {
        currentRespawnPosition = newSavePosition;

        // Lưu tên Virtual Camera đang active TẠI THỜI ĐIỂM chạm Save Point
        // Để khi respawn sẽ dùng đúng camera của khu vực Save Point, không phải camera lúc chết
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null && brain.ActiveVirtualCamera != null)
        {
            currentRespawnCameraName = brain.ActiveVirtualCamera.VirtualCameraGameObject.name;
        }

        Debug.Log("Đã cập nhật điểm hồi sinh mới tại: " + currentRespawnPosition + " | Camera: " + currentRespawnCameraName);
    }
    private IEnumerator ReloadMapAndRespawn()
    {
        // Lấy tên của màn chơi hiện tại (Màn chơi đang chứa quái, vật phẩm...)
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Chỉ thực hiện Unload nếu màn chơi không phải là gốc PersistentGameplay
        if (currentSceneName != "PersistentGameplay")
        {
            // Hủy Scene màn chơi hiện tại để dọn dẹp quái và đồ vật cũ
            yield return SceneManager.UnloadSceneAsync(currentSceneName);

            // Nạp lại Scene đó ở chế độ cộng dồn (Additive) để reset lại từ đầu
            yield return SceneManager.LoadSceneAsync(currentSceneName, LoadSceneMode.Additive);

            // Bắt buộc: Đặt màn chơi vừa load lại làm Active Scene để game nhận diện đúng không gian
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(currentSceneName));
        }

        // Sau khi map đã load lại xong toàn bộ, dịch chuyển Player về tọa độ Save Point
        transform.position = currentRespawnPosition;

        // Khôi phục Virtual Camera theo tên đã lưu tại Save Point (không phải lúc chết)
        RestoreVirtualCameraForSavePoint();

        Debug.Log("Đã tải lại map và hồi sinh thành công tại Save Point!");
    }

    /// <summary>
    /// Sau khi scene reload, tìm Virtual Camera có cùng tên đã lưu tại Save Point
    /// và đặt Priority cao nhất. Camera khác sẽ bị hạ Priority = 0.
    /// </summary>
    private void RestoreVirtualCameraForSavePoint()
    {
        CinemachineVirtualCamera[] allCams = FindObjectsOfType<CinemachineVirtualCamera>();

        if (allCams.Length == 0 || string.IsNullOrEmpty(currentRespawnCameraName)) return;

        bool found = false;

        // Hạ Priority tất cả camera, nâng camera trùng tên Save Point
        foreach (CinemachineVirtualCamera cam in allCams)
        {
            if (cam.name == currentRespawnCameraName)
            {
                cam.Priority = 10;
                found = true;
                Debug.Log("Đã khôi phục Virtual Camera theo Save Point: " + cam.name);
            }
            else
            {
                cam.Priority = 0;
            }
        }

        if (!found)
        {
            Debug.LogWarning("Không tìm thấy Virtual Camera tên: " + currentRespawnCameraName + " sau khi reload scene.");
        }
    }
    void StartFootStep()
    {
        isPlayFootStep = true;
        if(isRunning == false)
        {
            InvokeRepeating(nameof(PlayFootStep), 0f, footStepSpeed);
        }
        else if(isRunning == true)
        {
            InvokeRepeating(nameof(PlayFootRun), 0f, footStepSpeed);
        }
        
    }
    void StopFootStep()
    {
        isPlayFootStep = false;
        CancelInvoke(nameof(PlayFootStep));
        CancelInvoke(nameof(PlayFootRun));
    }
    void PlayFootStep()
    {
        SoundManager.Play("Step");
    }
    void PlayFootRun()
    {
        SoundManager.Play("Run");
    }
}
