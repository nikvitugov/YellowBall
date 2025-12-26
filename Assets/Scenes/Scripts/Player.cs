using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public Vector3 respawnPosition; 

    public GroundCheck groundCheck;

    [SerializeField] private Rigidbody2D rb;


    public int maxHealth = 100;
    [SerializeField] private int currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Проверяем тег "Spike" у объекта, чтобы игрок получал урон только от шипов
        if (collision.CompareTag("Spike"))
        {
            TakeDamage(100); // наносим 100 единиц урона (можно менять)
        }

        if (collision.CompareTag("Checkpoint"))
        {
            respawnPosition = transform.position;
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Player Health: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        transform.position = respawnPosition;
        currentHealth = maxHealth;
        // Здесь можно добавить логику смерти, например перезапуск сцены
    }

    void FixedUpdate()
    {
        float moveInput = 0f;

        if (Input.GetKey(KeyCode.A))
            moveInput = -1f;
        else if (Input.GetKey(KeyCode.D))
        {
            moveInput = 1f;
        }

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        if (Input.GetKey(KeyCode.W) && groundCheck.isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
    }
}