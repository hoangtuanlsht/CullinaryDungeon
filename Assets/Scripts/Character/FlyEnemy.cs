using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlyEnemy : Character
{
    public float speed, groundCheckRadius;
    public float dame;
    public Rigidbody2D rb;
    [SerializeField] Transform groundCheckUp;
    [SerializeField] Transform groundCheckDown;
    [SerializeField] Transform groundCheckWall;
    public LayerMask groundLayer;
    private bool isFacingLeft=true,isTouchingUp = false,isTouchingDown = false,isTouchingWall;
    public float dirX = -1f, dirY = -0.25f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        isTouchingUp = Physics2D.OverlapCircle(groundCheckUp.position, groundCheckRadius, groundLayer);
        isTouchingDown = Physics2D.OverlapCircle(groundCheckDown.position, groundCheckRadius, groundLayer);
        isTouchingWall = Physics2D.OverlapCircle(groundCheckWall.position, groundCheckRadius, groundLayer);
        rb.velocity = new Vector2 (dirX,dirY) * speed * Time.fixedDeltaTime;
        AttackUpAndDownState();
    }
    public void AttackUpAndDownState()
    {
        if (isTouchingUp)
        {
            dirY = -0.25f;
        }
        else if (isTouchingDown)
        {
            dirY = 0.25f;
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
    }
    
    // Tường chặn quái (EnemyWall): tường đứng → quay đầu, tường nằm (lỗ trần/sàn) → đổi hướng bay lên/xuống
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("EnemyWall")) return;
        var b = other.bounds;
        if (b.size.x > b.size.y)
        {
            dirY = transform.position.y < b.center.y ? -Mathf.Abs(dirY) : Mathf.Abs(dirY);
        }
        else if ((dirX > 0f && b.center.x > transform.position.x) || (dirX < 0f && b.center.x < transform.position.x))
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingLeft = !isFacingLeft;
        dirX *= -1f; // Đảo ngược giá trị di chuyển trục X
        transform.Rotate(0, 180, 0);
    }
    public override void OnDeath()
    {
        base.OnDeath();
    }
    public override void OnDespawn()
    {
        base.OnDespawn();
        Destroy(gameObject);
        Destroy(healthBar.gameObject);

    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(groundCheckUp.position, groundCheckRadius);
        Gizmos.DrawWireSphere(groundCheckDown.position, groundCheckRadius);
        Gizmos.DrawWireSphere(groundCheckWall.position, groundCheckRadius);
    }
}
