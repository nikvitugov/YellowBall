using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Размытие в движении: за игроком рисуется плотный шлейф полупрозрачных копий его модели
/// по позициям за последние доли секунды. Чем быстрее движение — тем шлейф заметнее.
/// Копии рисуются через Graphics.DrawMesh, без создания лишних GameObject.
/// </summary>
[DisallowMultipleComponent]
public class PlayerMotionBlur : MonoBehaviour
{
    [Header("Шлейф")]
    [Tooltip("Сколько копий рисовать. Больше — глаже размытие")]
    [Range(2, 24)] public int ghostCount = 10;
    [Tooltip("Длина шлейфа во времени, сек")]
    [Range(0.02f, 0.4f)] public float trailDuration = 0.1f;
    [Tooltip("Прозрачность ближайшей копии")]
    [Range(0f, 1f)] public float maxAlpha = 0.45f;
    [Tooltip("Цвет шлейфа (умножается на текстуру модели)")]
    public Color tint = new Color(1f, 0.92f, 0.45f, 1f);

    [Header("Скорость")]
    [Tooltip("Ниже этой скорости шлейфа нет")]
    public float minSpeed = 1.5f;
    [Tooltip("На этой скорости шлейф максимальный")]
    public float maxSpeed = 9f;

    [Header("Ресурсы")]
    public Shader trailShader;
    [Tooltip("Если пусто — берутся все MeshRenderer в дочерних объектах")]
    public MeshRenderer[] sources;

    private struct Sample
    {
        public float time;
        public Vector3 position;
    }

    private readonly List<Sample> history = new List<Sample>(64);
    private Material material;
    private MaterialPropertyBlock block;
    private MeshFilter[] filters;
    private Rigidbody2D rb;
    private Vector3 lastPosition;
    private float smoothedIntensity;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (trailShader == null) trailShader = Shader.Find("YellowBall/MotionTrail");
        if (trailShader == null)
        {
            Debug.LogWarning("PlayerMotionBlur: шейдер YellowBall/MotionTrail не найден, эффект выключен");
            enabled = false;
            return;
        }

        material = new Material(trailShader) { hideFlags = HideFlags.HideAndDontSave };
        block = new MaterialPropertyBlock();

        if (sources == null || sources.Length == 0)
            sources = GetComponentsInChildren<MeshRenderer>();

        filters = new MeshFilter[sources.Length];
        for (int i = 0; i < sources.Length; i++)
            filters[i] = sources[i] != null ? sources[i].GetComponent<MeshFilter>() : null;

        ResetTrail();
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    /// <summary>Сбросить историю (например, после телепорта/респауна).</summary>
    public void ResetTrail()
    {
        history.Clear();
        lastPosition = transform.position;
        smoothedIntensity = 0f;
    }

    private void LateUpdate()
    {
        float now = Time.time;
        Vector3 pos = transform.position;

        // Скорость: из физики, если есть, иначе по смещению за кадр
        float speed;
        if (rb != null) speed = rb.velocity.magnitude;
        else speed = Time.deltaTime > 0f ? (pos - lastPosition).magnitude / Time.deltaTime : 0f;
        lastPosition = pos;

        float target = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        // Плавно, чтобы шлейф не мигал при резких остановках
        smoothedIntensity = Mathf.MoveTowards(smoothedIntensity, target, Time.deltaTime * 8f);

        history.Add(new Sample { time = now, position = pos });
        float oldest = now - trailDuration - 0.05f;
        int drop = 0;
        while (drop < history.Count - 2 && history[drop + 1].time < oldest) drop++;
        if (drop > 0) history.RemoveRange(0, drop);

        if (smoothedIntensity <= 0.001f || history.Count < 2) return;

        for (int g = 1; g <= ghostCount; g++)
        {
            float k = g / (float)ghostCount;          // 0..1, дальше от игрока
            Vector3 past = PositionAt(now - trailDuration * k);
            Vector3 offset = past - pos;
            if (offset.sqrMagnitude < 0.0004f) continue;

            float fade = (1f - k);
            float alpha = maxAlpha * fade * fade * smoothedIntensity;
            Color c = tint;
            c.a = alpha;

            Matrix4x4 shift = Matrix4x4.Translate(offset);
            for (int i = 0; i < sources.Length; i++)
            {
                var r = sources[i];
                var f = filters[i];
                if (r == null || f == null || f.sharedMesh == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

                block.Clear();
                block.SetColor(ColorId, c);
                Texture tex = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
                if (tex != null) block.SetTexture(MainTexId, tex);

                Matrix4x4 m = shift * r.localToWorldMatrix;
                for (int s = 0; s < f.sharedMesh.subMeshCount; s++)
                    Graphics.DrawMesh(f.sharedMesh, m, material, r.gameObject.layer, null, s, block, false, false);
            }
        }
    }

    // Позиция игрока в момент t (линейная интерполяция по истории)
    private Vector3 PositionAt(float t)
    {
        if (t <= history[0].time) return history[0].position;
        for (int i = history.Count - 1; i > 0; i--)
        {
            Sample a = history[i - 1];
            if (a.time <= t)
            {
                Sample b = history[i];
                float span = b.time - a.time;
                return span > 1e-5f ? Vector3.Lerp(a.position, b.position, (t - a.time) / span) : b.position;
            }
        }
        return history[0].position;
    }
}
