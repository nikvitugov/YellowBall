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

    private float moveInput;
    private bool jumpHeld;
    private Vector2 defaultBoxSize;
    private int wallZones; // сколько зон WallClimbing сейчас касаемся

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
    }

    private void Update()
    {
        // Ввод читаем в Update, физику применяем в FixedUpdate
        moveInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveInput += 1f;

        jumpHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space);
    }

    private void FixedUpdate()
    {
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);

        if (jumpHeld && groundCheck != null && groundCheck.isGrounded)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
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
        currentHealth -= damage;
        Debug.Log("Player Health: " + currentHealth);

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        rb.velocity = Vector2.zero; // иначе после респауна продолжаем падать с той же скоростью
        rb.position = respawnPosition;
        transform.position = respawnPosition;
        currentHealth = maxHealth;

        var blur = GetComponent<PlayerMotionBlur>();
        if (blur != null) blur.ResetTrail(); // чтобы шлейф не тянулся через весь уровень
    }
}
