using UnityEngine;

/// <summary>
/// Звуки героя: прыжок — разово при отталкивании, шаги — зацикленный звук,
/// пока бежим по земле; темп подстраивается под скорость бега.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAudio : MonoBehaviour
{
    public AudioClip jumpClip;
    public AudioClip stepsClip;

    [Range(0f, 1f)] public float jumpVolume = 0.7f;
    [Range(0f, 1f)] public float stepsVolume = 0.5f;
    [Tooltip("Сколько шагов в звуке stepsClip (в walking.wav их два)")]
    public int stepsInClip = 2;
    [Tooltip("Высота звука прыжка: меньше 1 — ниже и тяжелее")]
    [Range(0.5f, 1.5f)] public float jumpPitch = 0.78f;
    [Tooltip("Небольшой случайный разброс высоты прыжка, чтобы не надоедал")]
    public float jumpPitchJitter = 0.05f;
    [Tooltip("Глухость прыжка: срез высоких частот, Гц (22000 = без фильтра)")]
    [Range(500f, 22000f)] public float jumpMuffle = 1600f;

    private PlayerMove move;
    private PlayerVisual visual;
    private Rigidbody2D rb;
    private AudioSource oneShots;
    private AudioLowPassFilter jumpFilter;
    private AudioSource steps;
    private float stepsFade;

    private void Awake()
    {
        move = GetComponent<PlayerMove>();
        visual = GetComponent<PlayerVisual>();
        rb = GetComponent<Rigidbody2D>();

        // у прыжка свой объект: фильтр глухости действует только на него, а не на шаги
        var jumpGo = new GameObject("JumpAudio");
        jumpGo.transform.SetParent(transform, false);
        oneShots = jumpGo.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
        jumpFilter = jumpGo.AddComponent<AudioLowPassFilter>();
        jumpFilter.lowpassResonanceQ = 1f;

        steps = gameObject.AddComponent<AudioSource>();
        steps.playOnAwake = false;
        steps.loop = true;
        steps.clip = stepsClip;
        steps.volume = 0f;
        steps.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        if (move == null) move = GetComponent<PlayerMove>();
        if (move != null) move.Jumped += PlayJump;
    }

    private void OnDisable()
    {
        if (move != null) move.Jumped -= PlayJump;
    }

    private void PlayJump()
    {
        if (jumpClip == null) return;
        oneShots.pitch = jumpPitch + Random.Range(-jumpPitchJitter, jumpPitchJitter);
        jumpFilter.cutoffFrequency = jumpMuffle;
        oneShots.PlayOneShot(jumpClip, jumpVolume);
    }

    private void Update()
    {
        if (stepsClip == null) return;

        float speed = rb != null && rb.simulated ? Mathf.Abs(rb.velocity.x) : 0f;
        bool running = move != null && !move.IsDead && move.IsGrounded && speed > 0.2f;

        // плавное включение/выключение, чтобы не щёлкало
        stepsFade = Mathf.MoveTowards(stepsFade, running ? 1f : 0f, Time.deltaTime * (running ? 12f : 8f));
        steps.volume = stepsVolume * stepsFade;

        if (stepsFade > 0f)
        {
            // темп: столько же шагов в секунду, сколько «шагает» анимация тряски
            float stepsPerUnit = visual != null ? visual.stepsPerUnit : 0.9f;
            float stepsPerSecond = Mathf.Max(speed, 2f) * stepsPerUnit;
            float clipStepsPerSecond = stepsInClip / stepsClip.length;
            steps.pitch = Mathf.Clamp(stepsPerSecond / clipStepsPerSecond, 0.6f, 2f);
            if (!steps.isPlaying) { steps.time = 0f; steps.Play(); }
        }
        else if (steps.isPlaying)
        {
            steps.Stop();
        }
    }
}
