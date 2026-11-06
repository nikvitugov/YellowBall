using UnityEngine;

/// <summary>
/// Анимация модели героя (только визуал, физика и камера не трогаются):
///  - покачивание при беге: подпрыгивание на каждом шаге + наклон из стороны в сторону;
///  - разворот в сторону бега (зеркалим модель по X с быстрым «переворотом»).
/// Камера — дочерний объект Player, а не модели, поэтому она не трясётся.
/// </summary>
[DisallowMultipleComponent]
public class PlayerVisual : MonoBehaviour
{
    [Tooltip("Модель героя (дочерний объект Player)")]
    public Transform visual;
    public GroundCheck groundCheck;

    [Header("Разворот")]
    [Tooltip("Смотрит ли модель вправо в исходном положении. Если перс бегает спиной вперёд — сними галочку")]
    public bool modelFacesRight = true;
    [Tooltip("Скорость разворота (больше — быстрее)")]
    public float turnSpeed = 7f;
    [Tooltip("Насколько герой подпрыгивает во время разворота")]
    public float turnHop = 0.06f;

    [Header("Тряска при беге")]
    [Tooltip("Высота подпрыгивания на каждом шаге")]
    public float bobHeight = 0.07f;
    [Tooltip("Наклон вправо-влево, градусы")]
    public float swayAngle = 5f;
    [Tooltip("Наклон вперёд по ходу бега, градусы")]
    public float leanAngle = 4f;
    [Tooltip("Шагов на одну единицу пройденного пути")]
    public float stepsPerUnit = 0.9f;
    [Tooltip("Ниже этой скорости тряски нет")]
    public float minRunSpeed = 0.2f;

    private Rigidbody2D rb;
    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector3 baseScale;
    private float face = 1f;       // текущее (сглаженное) направление: 1 вправо, -1 влево
    private float targetFace = 1f;
    private float phase;
    private float runAmount;       // 0..1, плавно включает/выключает тряску

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (groundCheck == null) groundCheck = GetComponentInChildren<GroundCheck>();
        if (visual == null)
        {
            var r = GetComponentInChildren<MeshRenderer>();
            if (r != null) visual = r.transform;
        }
        if (visual == null) { enabled = false; return; }

        basePos = visual.localPosition;
        baseRot = visual.localRotation;
        baseScale = visual.localScale;
    }

    private void Update()
    {
        float vx = rb != null ? rb.velocity.x : 0f;
        float speed = Mathf.Abs(vx);
        bool grounded = groundCheck == null || groundCheck.isGrounded;

        // Разворот
        if (speed > minRunSpeed) targetFace = Mathf.Sign(vx);
        face = Mathf.MoveTowards(face, targetFace, turnSpeed * Time.deltaTime);

        // Тряска только когда бежим по земле
        float want = (grounded && speed > minRunSpeed) ? 1f : 0f;
        runAmount = Mathf.MoveTowards(runAmount, want, Time.deltaTime * 6f);
        phase += speed * stepsPerUnit * Mathf.PI * Time.deltaTime; // полпериода синуса = один шаг
        if (runAmount <= 0f) phase = 0f;

        float s = Mathf.Sin(phase);
        float bob = Mathf.Abs(s) * bobHeight * runAmount;
        float tilt = s * swayAngle * runAmount - targetFace * leanAngle * runAmount;

        // Объёмный разворот: модель поворачивается вокруг вертикальной оси лицом к камере.
        // Первые 90° — поворот, на «ребре» модель зеркалится, следующие 90° — доворот.
        // Так спина никогда не показывается (важно, если модель плоская), а выглядит как полный поворот на 180°.
        float side = face >= 0f ? 1f : -1f;
        float t = Mathf.Abs(face);
        t = t * t * (3f - 2f * t);                    // плавный разгон/торможение
        float yaw = 90f * (1f - t) * side;
        float hop = Mathf.Sin((1f - Mathf.Abs(face)) * Mathf.PI * 0.5f) * turnHop;

        visual.localPosition = basePos + Vector3.up * (bob + hop);
        visual.localRotation = Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(0f, yaw, 0f) * baseRot;

        float mirror = modelFacesRight ? side : -side;
        Vector3 sc = baseScale;
        sc.x = baseScale.x * mirror;
        visual.localScale = sc;
    }
}
