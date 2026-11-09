using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    [Header("Движение")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("Ссылки")]
    public GroundCheck groundCheck;
    [Tooltip("Коллайдер JumpDetector: расширяется у стены, чтобы можно было прыгать от неё")]
    [SerializeField] private BoxCollider2D box;
    [SerializeField] private Rigidbody2D rb;

    [Header("Лазание по стене")]
    [SerializeField] private Vector2 wallClimbBoxSize = new Vector2(0.67f, 0.05f);

    [Header("Здоровье / респаун")]
    public int maxHealth = 100;
    [SerializeField] private int currentHealth;
    public Vector3 respawnPosition;
    [Tooltip("Сколько секунд после смерти до появления на чекпоинте")]
    public float respawnDelay = 1f;

    /// <summary>Оттолкнулись от земли (не вызывается каждый кадр при лазании).</summary>
    public event Action Jumped;
    /// <summary>Приземлились; параметр — скорость падения в момент касания.</summary>
    public event Action<float> Landed;
    /// <summary>Умерли (до задержки респауна).</summary>
    public event Action Died;
    /// <summary>Появились на чекпоинте.</summary>
    public event Action Respawned;

    public bool IsDead { get; private set; }
    public bool IsGrounded => groundCheck != null && groundCheck.isGrounded;

    private float moveInput;
    private bool jumpHeld;
    private Vector2 defaultBoxSize;
    private int wallZones; // сколько зон WallClimbing сейчас касаемся
    private bool wasGrounded;
    private float lastAirVy;
    private float lastJumpTime = -10f;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (groundCheck == null) groundCheck = GetComponentInChildren<GroundCheck>();
        if (box == null && groundCheck != null) box = groundCheck.GetComponent<BoxCollider2D>();
        if (box != null) defaultBoxSize = box.size;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        respawnPosition = transform.position; // стартовая точка = первый чекпоинт
        wasGrounded = IsGrounded;
    }

    private void Update()
    {
        // Ввод читаем в Update, физику применяем в FixedUpdate
        moveInput = 0f;
        jumpHeld = false;
        if (IsDead) return;

        // клавиши берутся из настроек (меню Options -> Controls)
        if (GameSettings.Held(GameSettings.Action.Left)) moveInput -= 1f;
        if (GameSettings.Held(GameSettings.Action.Right)) moveInput += 1f;
        jumpHeld = GameSettings.Held(GameSettings.Action.Jump);
    }

    private void FixedUpdate()
    {
        if (IsDead) return;

        bool grounded = IsGrounded;
        if (grounded && !wasGrounded && lastAirVy < -0.5f)
            Landed?.Invoke(-lastAirVy);
        if (!grounded) lastAirVy = rb.velocity.y;
        wasGrounded = grounded;

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        if (jumpHeld && grounded)
        {
            // Новый прыжок — только если не летим уже вверх (при лазании скорость задаётся каждый кадр)
            bool takeoff = rb.velocity.y < jumpForce * 0.5f && Time.time - lastJumpTime > 0.15f;
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (takeoff)
            {
                lastJumpTime = Time.time;
                Jumped?.Invoke();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsDead) return;

        if (collision.CompareTag("Spike"))
        {
            TakeDamage(100);
        }
        else if (collision.CompareTag("Checkpoint"))
        {
            respawnPosition = transform.position;
        }
        else if (collision.CompareTag("WallClimbing"))
        {
            wallZones++;
            if (box != null) box.size = wallClimbBoxSize;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("WallClimbing"))
        {
            wallZones = Mathf.Max(0, wallZones - 1);
            if (wallZones == 0 && box != null) box.size = defaultBoxSize;
        }
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        currentHealth -= damage;
        Debug.Log("Player Health: " + currentHealth);

        if (currentHealth <= 0)
            StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        Debug.Log("Player Died!");
        IsDead = true;
        rb.velocity = Vector2.zero;
        if (groundCheck != null) groundCheck.ResetState();
        rb.simulated = false;           // замираем: никакой физики и триггеров, пока «рассыпаны»
        Died?.Invoke();

        yield return new WaitForSeconds(respawnDelay);

        wallZones = 0;
        if (box != null) box.size = defaultBoxSize;
        rb.position = respawnPosition;
        transform.position = respawnPosition;
        rb.velocity = Vector2.zero;
        rb.simulated = true;
        currentHealth = maxHealth;
        wasGrounded = false;
        lastAirVy = 0f;
        IsDead = false;
        Respawned?.Invoke();
    }
}
