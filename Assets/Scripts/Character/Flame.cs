using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flame : MonoBehaviour
{
    public ParticleSystem hitVFX;
    public Rigidbody2D rb;
    [SerializeField]private float speed;
    [SerializeField]private float damage;
    // Start is called before the first frame update
    void Start()
    {
        OnInit();
    }

    // Update is called once per frame
    private void OnInit(){
        rb.velocity = transform.right * speed;
        Invoke(nameof(OnDespawn), 5f);
    }
    private void OnDespawn()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            collision.GetComponent<Character>().OnHit(damage, transform);
            Instantiate(hitVFX, transform.position, transform.rotation);
            OnDespawn();
        }
    }
}
