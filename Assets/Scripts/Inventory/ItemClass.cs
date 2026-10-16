using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemClass : MonoBehaviour, IPoolable
{
    public ItemItem itemData; // Bạn kéo file ScriptableObject vật phẩm vào ô này trong Inspector
    public int amount = 1;
    public Rigidbody2D rb;
    public LayerMask groundLayer;
    
    public void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
    public void OnSpawned()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.velocity = Vector2.zero;
    }
    public void OnDespawned() { }
    public void Update()
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic && CheckGrounded())
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.velocity = Vector2.zero;
        }
    }
    public void HaverstItem()
    {
        InventoryManager.Instance.AddItem(itemData,1);
        PoolManager.Despawn(gameObject);
    }
    
    private bool CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.5f, groundLayer);

        if (hit.collider != null)
        {
            return true;
        }
        return false;
    }

}
