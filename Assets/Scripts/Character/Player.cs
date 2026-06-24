using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : Character
{
    public float speed = 5f;
    public float jumpForce = 10f;    
    [SerializeField] private float attackCooldown = 2f;
    public float healHealth;////
    private float attackTimer;
    [SerializeField] private float knockbackForceX = 3f; // Lực văng ngang

    private float horizontalInput;
    private float verticalInput;
    private bool isGrounded=true;
    private bool isJumping=false;
    private bool isAttacking=false;
    private bool isDead=false;
    private bool isHurt = false;
    private bool isDefending = false;
    private bool isPlayFootStep=false;
    public float footStepSpeed = 0.5f;
    public LayerMask groundLayer;

    [SerializeField] public static int coin=0;


    [SerializeField]public Rigidbody2D rb;

    [SerializeField] private GameObject attackArea;
    [SerializeField] public GameObject interactUI;
    [SerializeField] private Interact currentInteract;
    [SerializeField] private ItemClass currentItem;
    [SerializeField]private InventoryManager recycleableInventoryManager;
    [SerializeField]private GameObject cooking;
    [SerializeField] private ShopNPC currentShopNPC;

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
        //Jumping
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isJumping = true;
        }
        //attack
        if (Input.GetKeyDown(KeyCode.C) &&!isDefending)
        {
            Attack();
            
        }
        if (Input.GetKey(KeyCode.F) && isGrounded && !isAttacking && !isHurt && !isDead && !isDefending)
        {
            isDefending = true;
            SoundManager.Play("Defend");
            Invoke("ResetDefend", 0.5f);
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
        
    }   
    void FixedUpdate()
    {
        isGrounded = CheckGrounded();
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
            
            ChangedAnim("Defend"); // Chạy Animation đỡ đòn
            return;
        }
        if (isAttacking)
        {
            rb.velocity = new Vector2(0,rb.velocity.y);
            return;
        }
        if (isGrounded)
        {
            if (isJumping)
            {
                if (isPlayFootStep) StopFootStep();
                Jump();
                return;
            }

            //Change run
            if (rb.velocity.y <= 0.1f)
            {
                //Change run
                if (Mathf.Abs(horizontalInput) > 0.1f)
                {
                    ChangedAnim("Run");
                    if (!isPlayFootStep) StartFootStep();
                    
                }
                else
                {
                    ChangedAnim("Idle");
                    StopFootStep();
                }
            }
            //throw
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
            rb.velocity = new Vector2(horizontalInput * speed*Time.fixedDeltaTime, rb.velocity.y);
            if (horizontalInput > 0)
            {
                transform.localScale = new Vector3(1f, 1f, 1f);
            }
            else if (horizontalInput < 0)
            {
                transform.localScale = new Vector3(-1f, 1f, 1f);
            }
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
        coin = 0;
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
        Invoke("RespawnAtGuild", 2f);
    }
    public override void OnHit(float damage)
    {
        if (isDefending)
        {
            Debug.Log("Đã đỡ đòn! Không nhận sát thương.");
            // Tùy chọn: Bạn có thể thêm animation tia lửa hoặc âm thanh đỡ đòn ở đây
            // return ngay lập tức để bỏ qua việc trừ máu ở class base
            return;
        }
        base.OnHit(damage);
        if (!IsDead)
        {
            Debug.Log("Player bị thương!");
            ChangedAnim("Hurt");
            isHurt = true;

            rb.velocity = Vector2.zero;
            float knockbackDirection = transform.localScale.x > 0 ? -1f : 1f;
            rb.AddForce(new Vector2(knockbackDirection * knockbackForceX, 0),ForceMode2D.Impulse);
            // Dừng các hoạt động khác trong 0.5 giây (hoặc bằng thời gian của animation Hurt)
            Invoke(nameof(ResetHurt), 0.5f);
        }
    }
    public void Healing()////////
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
    private void ResetDefend()
    {
        isDefending = false;
    }
    private bool CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.01f, groundLayer);
        
        if (hit.collider != null)
        {
            return true;
        }
        return false;
    }
    private void Jump() 
    {
        ChangedAnim("Jump");
        isJumping = false;
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
        ChangedAnim("Attack");
        isAttacking = true;
        Invoke("ResetAttack", 0.5f);
        ActiveAttack();
        Invoke("DeactiveAttack", 0.5f);
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
            Invoke("RespawnAtGuild", 2f);
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
    private void RespawnAtGuild()
    {
        coin = 0;
        PlayerPrefs.SetInt("coin", 0);
        UIManager.instance.SetCoin(coin);

        // 2. Mất toàn bộ đồ trong túi
        if (recycleableInventoryManager != null)
        {
            recycleableInventoryManager.ClearInventory(); // Tùy thuộc vào hàm bạn đã viết ở bước trước
        }

        // 3. Khôi phục lại trạng thái sống và máu (gọi lại OnInit để reset anim/trạng thái)
        OnInit();
        // Giả sử class Character của bạn có biến máu, hãy buff đầy máu ở đây:
        // health = maxhealth;
        // healthBar.SetNewHP(health);

        // 4. Báo cho SpawnManager biết vị trí cần đứng khi load xong scene sảnh
        // Thay "GuildSpawnPoint" bằng đúng tên GameObject điểm hồi sinh bạn đặt trong scene sảnh
        SceneData.SpawnPointName = "GuildSpawnPoint";

        // 5. Load scene Sảnh chính
        // Thay "GuildScene" bằng đúng tên scene của bạn trong Build Settings
        SceneManager.LoadScene("GuildScene");
    }
    void StartFootStep()
    {
        isPlayFootStep = true;
        InvokeRepeating(nameof(PlayFootStep), 0f, footStepSpeed);
    }
    void StopFootStep()
    {
        isPlayFootStep = false;
        CancelInvoke(nameof(PlayFootStep));
    }
    void PlayFootStep()
    {
        SoundManager.Play("Step");
    }
}
