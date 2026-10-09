using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GruzMother : Character
{
    [Header("Idle")]
    [SerializeField] float moveSpeed;
    [SerializeField] Vector2 moveDirection;

    [Header("AttackUpDown")]
    [SerializeField] float attackSpeed;
    [SerializeField] Vector2 attackDirection;

    [Header("AttackPlayer")]
    [SerializeField] float attackPlayerSpeed;
    [SerializeField] public float dame;
    [SerializeField] GameObject player;
    [SerializeField] GameObject attackArea;
    private Vector2 playerPos;
    private bool hasPlayerPos;

    [Header("Other")]
    [SerializeField] Transform groundCheckUp;
    [SerializeField] Transform groundCheckDown;
    [SerializeField] Transform groundCheckWall;
    [SerializeField] float groundCheckRadius;
    [SerializeField] LayerMask groundLayer;

    private bool isTouchingUp = false;
    private bool isTouchingDown = false;
    private bool isTouchingWall = false;
    private bool isGoingUp = true;
    private bool isFacingLeft = true;
    private Rigidbody2D rb;
    private Animator animGruz;

    private IGruzState currentState;

    private void Awake()
    {
        moveDirection.Normalize();
        attackDirection.Normalize();
        rb = GetComponent<Rigidbody2D>();
        animGruz = GetComponent<Animator>();
        player = GameObject.FindWithTag("Player");

    }

    // Update is called once per frame
    private void Update()
    {
        isTouchingUp = Physics2D.OverlapCircle(groundCheckUp.position, groundCheckRadius, groundLayer);
        isTouchingDown = Physics2D.OverlapCircle(groundCheckDown.position, groundCheckRadius, groundLayer);
        isTouchingWall = Physics2D.OverlapCircle(groundCheckWall.position, groundCheckRadius, groundLayer);
        currentState?.OnExecute(this);
    }

    public void ChangeState(IGruzState newState)
    {
        currentState?.OnExit(this);
        currentState = newState;
        currentState?.OnEnter(this);
    }

    // animGruz đang private -> state bên ngoài muốn bắn trigger animation
    // phải qua hàm public này, không đổi animGruz thành public (giữ encapsulation).
    public void TriggerAnim(string triggerName)
    {
        animGruz.SetTrigger(triggerName);
    }

    // GruzDiveState cần biết cú lao đã kết thúc chưa
    // (AttackPlayer() tự đặt hasPlayerPos = false khi chạm tường/đất).
    public bool HasPlayerPos => hasPlayerPos;
    public override void OnInit()
    {
        base.OnInit();
        ChangeState(new GruzPatrolState());
    }

    public void IdleState()
    {
        if (isTouchingUp && isGoingUp)
        {
            ChangeDirection();
        }
        else if (isTouchingDown && !isGoingUp)
        {
            ChangeDirection();
        }

        if (isTouchingWall)
        {
            if (isFacingLeft)
            {
                Flip();
            }
            else if(!isFacingLeft) 
            {
                Flip();
            }
        }
        rb.velocity = moveSpeed * moveDirection;
    }
    public void AttackUpAndDownState()
    {
        if (isTouchingUp && isGoingUp)
        {
            ChangeDirection();
        }
        else if (isTouchingDown && !isGoingUp)
        {
            ChangeDirection();
        }

        if (isTouchingWall)
        {
            if (isFacingLeft)
            {
                Flip();
            }
            else if (!isFacingLeft)
            {
                Flip();
            }
        }
        rb.velocity = attackSpeed * attackDirection;
    }
    public void AttackPlayer()
    {
        if (!hasPlayerPos) 
        {
            playerPos = player.transform.position - transform.position;
            playerPos.Normalize();
            hasPlayerPos = true;
        }
        if (hasPlayerPos)
        {
            rb.velocity = playerPos * attackPlayerSpeed;
            attackArea.SetActive(true);
        }
        if(isTouchingWall || isTouchingDown)
        {
            rb.velocity = Vector2.zero;
            hasPlayerPos = false;
            animGruz.SetTrigger("Slamed");
            attackArea.SetActive(false);
        }
    }
    public override void OnDeath()
    {
        base.OnDeath();
        CancelInvoke(nameof(OnDespawn));
        rb.velocity = new Vector2(0, rb.velocity.y);
        Invoke("ReturnToMainMenu", 2f);
        Invoke(nameof(OnDespawn), 2.5f);
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
        Destroy(healthBar.gameObject);
        Destroy(gameObject);
    }
    public void FlipTowardsPlayer()
    {
        float playerDirection = player.transform.position.x - transform.position.x;
        if(playerDirection > 0 && isFacingLeft)
        {
            Flip();
        }
        else if (playerDirection <0 && !isFacingLeft)
        {
            Flip();
        }
    }
    private void ChangeDirection()
    {
        isGoingUp = !isGoingUp;
        moveDirection.y *= -1;
        attackDirection.y *= -1;

    }
    private void Flip()
    {
        isFacingLeft = !isFacingLeft;
        moveDirection.x *= -1;
        attackDirection.x *= -1;
        transform.Rotate(0, 180, 0);
    }
    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(groundCheckUp.position,groundCheckRadius);
        Gizmos.DrawWireSphere(groundCheckDown.position, groundCheckRadius);
        Gizmos.DrawWireSphere(groundCheckWall.position,groundCheckRadius);
    }
}
