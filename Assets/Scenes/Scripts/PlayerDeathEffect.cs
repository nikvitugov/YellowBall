using UnityEngine;

/// <summary>
/// Смерть героя: модель рассыпается на частицы — каждая вылетает из точки модели
/// и окрашена в цвет текстуры в этой точке. После респауна — маленькая вспышка искр.
/// Время до респауна задаётся в PlayerMove.respawnDelay.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathEffect : MonoBehaviour
{
    [Tooltip("Материал частиц (создаётся меню Yellow Ball / Setup Player)")]
    public Material particleMaterial;

    [Header("Распад")]
    public int pieces = 450;
    public Vector2 pieceSize = new Vector2(0.035f, 0.075f);
    public Vector2 pieceLifetime = new Vector2(0.6f, 0.95f);
    [Tooltip("Скорость разлёта от центра")]
    public Vector2 burstSpeed = new Vector2(1.2f, 3.8f);
    [Tooltip("Дополнительный подброс вверх")]
    public Vector2 upSpeed = new Vector2(0.5f, 2.5f);
    public float gravity = 1.1f;
    [Tooltip("Цвет, если текстура модели не читается")]
    public Color fallbackColor = new Color(1f, 0.85f, 0.45f);
    [Tooltip("Яркость кусочков (модель светится, поэтому цвет из текстуры немного усиливаем)")]
    public float brightness = 1.3f;

    [Header("Появление")]
    public int respawnSparks = 40;
    public Color sparkColor = new Color(1f, 0.9f, 0.5f);

    private PlayerMove move;
    private PlayerVisual visual;
    private ParticleSystem ps;

    private void Awake()
    {
        move = GetComponent<PlayerMove>();
        visual = GetComponent<PlayerVisual>();
        if (ps == null) ps = CreateSystem();
    }

    private void OnEnable()
    {
        if (move == null) move = GetComponent<PlayerMove>();
        if (move == null) return;
        move.Died += Burst;
        move.Respawned += Sparks;
    }

    private void OnDisable()
    {
        if (move == null) return;
        move.Died -= Burst;
        move.Respawned -= Sparks;
    }

    private void OnDestroy()
    {
        if (ps != null) Destroy(ps.gameObject);
    }

    private ParticleSystem CreateSystem()
    {
        // Отдельный объект в мире, чтобы частицы не ездили за игроком
        var go = new GameObject("PlayerDeathParticles");
        var p = go.AddComponent<ParticleSystem>();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = p.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(pieces, 64) + respawnSparks + 16;

        var emission = p.emission;
        emission.enabled = false;               // всё выпускаем вручную через Emit
        var shape = p.shape;
        shape.enabled = false;

        // к концу жизни кусочки уменьшаются и тают
        var sol = p.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f)));
        var col = p.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var rot = p.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var drag = p.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 1.2f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.sortingOrder = 20;                    // в игровом слое поверх платформ и шипов
        r.material = particleMaterial != null ? particleMaterial : new Material(Shader.Find("Sprites/Default"));
        return p;
    }

    /// <summary>Система частиц эффекта (создаётся при первом использовании).</summary>
    public ParticleSystem Particles => ps != null ? ps : (ps = CreateSystem());

    public void Burst()
    {
        if (visual == null) visual = GetComponent<PlayerVisual>();
        var ps = Particles;
        Transform model = visual != null ? visual.visual : null;
        var mf = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
        Mesh mesh = mf != null ? mf.sharedMesh : null;

        Vector3[] verts = null;
        Vector2[] uvs = null;
        Texture2D tex = null;
        Matrix4x4 m = Matrix4x4.identity;
        if (mesh != null && mesh.isReadable)
        {
            verts = mesh.vertices;
            uvs = mesh.uv;
            m = mf.transform.localToWorldMatrix;
            var mr = mf.GetComponent<MeshRenderer>();
            tex = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.mainTexture as Texture2D : null;
            if (tex != null && !tex.isReadable) tex = null;
        }

        Vector3 center = transform.position + Vector3.up * 0.5f;
        if (verts != null && verts.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < verts.Length; i += 7) sum += m.MultiplyPoint3x4(verts[i]);
            center = sum / Mathf.Ceil(verts.Length / 7f);
        }

        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < pieces; i++)
        {
            Vector3 pos;
            Color c = fallbackColor;
            if (verts != null && verts.Length > 0)
            {
                int k = Random.Range(0, verts.Length);
                pos = m.MultiplyPoint3x4(verts[k]);
                if (tex != null && uvs != null && uvs.Length == verts.Length)
                    c = tex.GetPixelBilinear(uvs[k].x, uvs[k].y);
            }
            else
            {
                pos = center + Random.insideUnitSphere * 0.4f;
            }

            Vector3 dir = pos - center;
            dir.z *= 0.3f;                       // в основном разлетаемся в плоскости экрана
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Random.onUnitSphere;
            ep.position = pos;
            ep.velocity = dir * Random.Range(burstSpeed.x, burstSpeed.y) + Vector3.up * Random.Range(upSpeed.x, upSpeed.y);
            ep.startSize = Random.Range(pieceSize.x, pieceSize.y);
            ep.startLifetime = Random.Range(pieceLifetime.x, pieceLifetime.y);
            ep.rotation = Random.Range(0f, 360f);
            c = new Color(Mathf.Clamp01(c.r * brightness), Mathf.Clamp01(c.g * brightness), Mathf.Clamp01(c.b * brightness), 1f);
            ep.startColor = c;
            ps.Emit(ep, 1);
        }
    }

    private void Sparks()
    {
        if (respawnSparks <= 0) return;
        var ps = Particles;
        var ep = new ParticleSystem.EmitParams();
        Vector3 center = transform.position + Vector3.up * 0.45f;
        for (int i = 0; i < respawnSparks; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            ep.position = center + (Vector3)(d * 0.15f);
            ep.velocity = (Vector3)(d * Random.Range(1.5f, 3f)) + Vector3.up * 0.5f;
            ep.startSize = Random.Range(0.025f, 0.05f);
            ep.startLifetime = Random.Range(0.25f, 0.45f);
            ep.startColor = sparkColor;
            ps.Emit(ep, 1);
        }
    }
}
