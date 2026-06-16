using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Character
{
    public float speed = 5f;
    public float jumpForce = 10f;
    private float horizontalInput;
    private float verticalInput;
    private bool isGrounded=true;
    private bool isJumping=false;
    private bool isAttacking=false;
    private bool isDead=false;  
    public LayerMask groundLayer;

    [SerializeField] public static int coin=0;

    [SerializeField]public Rigidbody2D rb;

    [SerializeField] private Vector3 savePoint;
    [SerializeField] private GameObject attackArea;
    [SerializeField] public GameObject interactUI;
    [SerializeField] private Interact currentInteract;
    [SerializeField] private ItemClass currentItem;
    [SerializeField]private InventoryManager recycleableInventoryManager;
    public void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        savePoint = transform.position;
        coin = PlayerPrefs.GetInt("coin", 0);
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
        recycleableInventoryManager = GameObject.Find("InventoryManager").GetComponent<InventoryManager>();
    }

    // Update is called once per frame
    void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        //Jumping
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isJumping = true;
        }

        //attack
        if (Input.GetKeyDown(KeyCode.C))
        {
            Attack();
        }
        if(Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }   
    void FixedUpdate()
    {
        isGrounded = CheckGrounded();
        if(isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        if (isAttacking)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        if (isGrounded)
        {
            if (isJumping)
            {
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
                }
                else
                {
                    ChangedAnim("Idle");
                }
            }
            //throw
        }

        //change fall
        if (!isGrounded && rb.velocity.y < 0)
        {
            isJumping = false;
            ChangedAnim("Fall");
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
        //health = 100f;
        base.OnInit();
        isJumping = false;
        isAttacking = false;
        isDead = false;
        coin = 0;
        transform.position = savePoint;
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
        OnInit();
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
            Invoke("OnInit", 2f);
        }
        Interact interact = collision.GetComponent<Interact>();
        if (interact != null)
        {
            currentInteract = interact;
            if (interactUI != null)
            {
                interactUI.SetActive(true);
                interactUI.transform.position = currentInteract.transform.position + new Vector3(0, 0.5f,0);
            }
        }
        ItemClass item = collision.GetComponent<ItemClass>();
        if (item != null) 
        {
            currentItem = item;

            interactUI.SetActive(true);
            interactUI.transform.position = currentItem.transform.position + new Vector3(0, 0.5f, 0);
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
        ItemItem item = collision.GetComponent <ItemItem>();
        if (item != null && item == currentItem)
        {
            currentItem = null;
            if(interactUI != null) interactUI.SetActive(false);
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
    }
}
