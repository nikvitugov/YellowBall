using UnityEngine;

/// <summary>
/// Плавная камера: догоняет игрока с небольшой задержкой и смотрит чуть вперёд по ходу бега.
/// Расстояние до игрока умножается на настройку «приближение камеры» (GameSettings.CameraZoom).
/// Вешается на Main Camera. Если камера была дочерней у игрока — при старте отцепляется,
/// сохраняя текущее смещение.
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Плавность")]
    [Tooltip("Время догона по горизонтали, сек")]
    public float smoothTimeX = 0.18f;
    [Tooltip("Время догона по вертикали, сек (больше — меньше дёргается при прыжках)")]
    public float smoothTimeY = 0.3f;

    [Header("Взгляд вперёд")]
    [Tooltip("Насколько камера сдвигается вперёд по ходу бега")]
    public float lookAhead = 1.5f;
    [Tooltip("Как быстро меняется сдвиг при развороте")]
    public float lookAheadSpeed = 2.5f;

    [Header("Смещение")]
    [Tooltip("Смещение от игрока. Если включено «брать из сцены» — берётся текущее положение камеры")]
    public Vector3 offset = new Vector3(0f, 0.1f, -7.23f);
    public bool offsetFromScene = true;

    private Rigidbody2D targetRb;
    private float ahead;
    private float velX, velY, velZ;
    private float zoom = 1f;

    private void Start()
    {
        if (target == null && transform.parent != null) target = transform.parent;
        if (target == null) { enabled = false; return; }

        if (offsetFromScene) offset = transform.position - target.position;
        if (transform.parent == target) transform.SetParent(null, true);

        targetRb = target.GetComponent<Rigidbody2D>();
        zoom = GameSettings.CameraZoom;
        transform.position = target.position + offset / zoom;   // сразу с нужным приближением
    }

    private void LateUpdate()
    {
        float vx = targetRb != null && targetRb.simulated ? targetRb.velocity.x : 0f;
        float wantAhead = Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) * lookAhead : ahead;
        ahead = Mathf.MoveTowards(ahead, wantAhead, lookAheadSpeed * Time.deltaTime);

        // приближение из настроек: смещение камеры делим на него (1.5 — в 1.5 раза ближе к герою)
        zoom = GameSettings.CameraZoom;
        Vector3 goal = target.position + offset / zoom + Vector3.right * ahead / zoom;
        Vector3 p = transform.position;
        p.x = Mathf.SmoothDamp(p.x, goal.x, ref velX, smoothTimeX);
        p.y = Mathf.SmoothDamp(p.y, goal.y, ref velY, smoothTimeY);
        p.z = Mathf.SmoothDamp(p.z, goal.z, ref velZ, 0.15f);
        transform.position = p;
    }
}
