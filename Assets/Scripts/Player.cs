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

    [SerializeField] private int coin=0;

    [SerializeField]public Rigidbody2D rb;

    [SerializeField] private Vector3 savePoint;
    public void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        savePoint = transform.position;
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
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                ChangedAnim("Run");
            }
            else
            {
                ChangedAnim("Idle");
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
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
    }
    public override void OnDeath()
    {
        base.OnDeath();
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
        Debug.Log("Jump");
        ChangedAnim("Jump");
        isJumping = false;
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);

    }
    private void Attack()
    {
        ChangedAnim("Attack");
        isAttacking = true;
        Invoke("ResetAttack", 0.5f);
    }
    private void ResetAttack()
    {
        isAttacking = false;
    }
   

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Coin"))
        {
            coin++;
            Destroy(collision.gameObject);
            Debug.Log("Coin");
        }
        if(collision.CompareTag("DeadZone"))
        {
            //health = 0;
            ChangedAnim("Dead");
            isDead = true;
            Debug.Log("Dead");
            Invoke("OnInit", 2f);
        }
    }
}
