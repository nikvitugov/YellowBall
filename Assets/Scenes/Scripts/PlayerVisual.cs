using UnityEngine;

/// <summary>
/// Анимация модели героя (только визуал, физика и камера не трогаются):
///  - покачивание при беге: подпрыгивание на каждом шаге + наклон из стороны в сторону;
///  - объёмный разворот в сторону бега;
///  - сжатие/растяжение при прыжке и приземлении, «выпрыгивание» после респауна;
///  - несколько моделей героя, переключение клавишей (по умолчанию Tab).
/// Модели должны стоять «ногами» в начале координат и без поворота (как hero3d и hero_ai).
/// </summary>
[DisallowMultipleComponent]
public class PlayerVisual : MonoBehaviour
{
    [Tooltip("Текущая модель героя (дочерний объект Player)")]
    public Transform visual;
    public GroundCheck groundCheck;

    [Header("Модели")]
    [Tooltip("Все модели героя. Видна только одна")]
    public Transform[] models;
    public int activeModel;
    [Tooltip("Клавиша смены модели. None — выключить")]
    public KeyCode switchKey = KeyCode.Tab;

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

    [Header("Сжатие и растяжение")]
    [Tooltip("Растяжение при прыжке")]
    public float jumpStretch = 7f;
    [Tooltip("Сжатие при приземлении на единицу скорости падения")]
    public float landSquashPerSpeed = 0.9f;
    [Tooltip("Жёсткость пружины (больше — быстрее возвращается)")]
    public float springStiffness = 180f;
    [Tooltip("Затухание пружины")]
    public float springDamping = 12f;

    /// <summary>Фаза шага (полпериода синуса = один шаг), для звука шагов.</summary>
    public float StepPhase => phase;
    public float RunAmount => runAmount;

    private Rigidbody2D rb;
    private PlayerMove move;
    private PlayerMotionBlur blur;
    private Vector3[] basePos;
    private Quaternion[] baseRot;
    private Vector3[] baseScale;
    private float face = 1f;       // текущее (сглаженное) направление: 1 вправо, -1 влево
    private float targetFace = 1f;
    private float phase;
    private float runAmount;       // 0..1, плавно включает/выключает тряску
    private float squash, squashVel;    // >0 — вытянут вверх, <0 — сплющен
    private float appear = 1f, appearVel; // общий масштаб (для «выпрыгивания» после респауна)
    private bool hidden;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        move = GetComponent<PlayerMove>();
        blur = GetComponent<PlayerMotionBlur>();
        if (groundCheck == null) groundCheck = GetComponentInChildren<GroundCheck>();

        if (models == null || models.Length == 0)
        {
            if (visual == null)
            {
                var r = GetComponentInChildren<MeshRenderer>();
                if (r != null) visual = r.transform;
            }
            models = visual != null ? new[] { visual } : new Transform[0];
        }
        if (models.Length == 0) { enabled = false; return; }

        basePos = new Vector3[models.Length];
        baseRot = new Quaternion[models.Length];
        baseScale = new Vector3[models.Length];
        for (int i = 0; i < models.Length; i++)
        {
            if (models[i] == null) continue;
            basePos[i] = models[i].localPosition;
            baseRot[i] = models[i].localRotation;
            baseScale[i] = models[i].localScale;
        }
        SetModel(Mathf.Clamp(activeModel, 0, models.Length - 1));
    }

    private void OnEnable()
    {
        if (move == null) move = GetComponent<PlayerMove>();
        if (move == null) return;
        move.Jumped += OnJumped;
        move.Landed += OnLanded;
        move.Died += OnDied;
        move.Respawned += OnRespawned;
    }

    private void OnDisable()
    {
        if (move == null) return;
        move.Jumped -= OnJumped;
        move.Landed -= OnLanded;
        move.Died -= OnDied;
        move.Respawned -= OnRespawned;
    }

    private void OnJumped() => squashVel += jumpStretch;
    private void OnLanded(float impact) => squashVel -= Mathf.Min(impact * landSquashPerSpeed, 12f);
    private void OnDied() => SetHidden(true);

    private void OnRespawned()
    {
        SetHidden(false);
        appear = 0.15f; appearVel = 0f;     // вырастает с небольшим перехлёстом
        squash = 0f; squashVel = 0f;
    }

    /// <summary>Показать модель с номером i, остальные спрятать.</summary>
    public void SetModel(int i)
    {
        for (int k = 0; k < models.Length; k++)
        {
            if (models[k] == null) continue;
            models[k].localPosition = basePos[k];
            models[k].localRotation = baseRot[k];
            models[k].localScale = baseScale[k];
            models[k].gameObject.SetActive(k == i);
        }
        activeModel = i;
        visual = models[i];
        SetHidden(hidden);
        if (blur != null) blur.RefreshSources();
    }

    /// <summary>Спрятать/показать текущую модель (рендереры, объект остаётся).</summary>
    public void SetHidden(bool value)
    {
        hidden = value;
        if (visual == null) return;
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            r.enabled = !value;
    }

    private void Update()
    {
        if (switchKey != KeyCode.None && Input.GetKeyDown(switchKey) && models.Length > 1)
            SetModel((activeModel + 1) % models.Length);

        float dt = Time.deltaTime;
        float vx = rb != null && rb.simulated ? rb.velocity.x : 0f;
        float speed = Mathf.Abs(vx);
        bool grounded = groundCheck == null || groundCheck.isGrounded;

        // Разворот
        if (speed > minRunSpeed) targetFace = Mathf.Sign(vx);
        face = Mathf.MoveTowards(face, targetFace, turnSpeed * dt);

        // Тряска только когда бежим по земле
        float want = (grounded && speed > minRunSpeed) ? 1f : 0f;
        runAmount = Mathf.MoveTowards(runAmount, want, dt * 6f);
        phase += speed * stepsPerUnit * Mathf.PI * dt;
        if (runAmount <= 0f) phase = 0f;

        // Пружины сжатия и появления (полу-неявный Эйлер, шаг дробим для устойчивости)
        int sub = Mathf.Max(1, Mathf.CeilToInt(dt / 0.005f));
        float h = dt / sub;
        for (int i = 0; i < sub; i++)
        {
            squashVel += (-springStiffness * squash - springDamping * squashVel) * h;
            squash += squashVel * h;
            appearVel += (springStiffness * 0.6f * (1f - appear) - springDamping * 0.7f * appearVel) * h;
            appear += appearVel * h;
        }
        squash = Mathf.Clamp(squash, -0.45f, 0.45f);

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

        int m = activeModel;
        visual.localPosition = basePos[m] + Vector3.up * (bob + hop);
        visual.localRotation = Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(0f, yaw, 0f) * baseRot[m];

        float mirror = modelFacesRight ? side : -side;
        float wide = 1f - squash * 0.6f;              // объём примерно сохраняется
        Vector3 sc = baseScale[m] * Mathf.Max(0f, appear);
        sc.x *= mirror * wide;
        sc.y *= 1f + squash;
        sc.z *= wide;
        visual.localScale = sc;
    }
}
