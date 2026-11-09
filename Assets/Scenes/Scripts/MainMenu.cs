using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Главное меню: Play / Options / Quit. В Options — вкладки Audio, Video, Controls.
/// Интерфейс собирает меню Yellow Ball / Build Game Menu (Editor/MenuBuilder.cs),
/// оно же проставляет все ссылки ниже. Вид можно править прямо в сцене.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Сцена, которую запускает Play")]
    public string gameScene = "SampleScene";

    [Header("Панели")]
    public GameObject mainPanel;
    public GameObject optionsPanel;

    [Header("Вкладки Options")]
    public Button[] tabButtons = new Button[3];     // Audio, Video, Controls
    public GameObject[] tabPages = new GameObject[3];
    public Color tabActive = new Color(1f, 0.83f, 0.3f);
    public Color tabIdle = new Color(1f, 1f, 1f, 0.15f);

    [Header("Audio")]
    public Slider volumeSlider;
    public Text volumeValue;

    [Header("Video")]
    public Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Slider zoomSlider;
    public Text zoomValue;

    [Header("Controls")]
    public Button[] keyButtons = new Button[3];     // Left, Right, Jump
    public Text controlsHint;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int rebinding = -1;                     // какое действие ждёт новую клавишу
    private bool ignoreEvents;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Audio
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.onValueChanged.AddListener(v => { if (!ignoreEvents) GameSettings.SetVolume(v); RefreshLabels(); });

        // Video
        var list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                                     .OrderByDescending(r => r.x * r.y).ToList();
        var cur = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
        if (cur.x > 0 && !list.Contains(cur)) list.Insert(0, cur);
        if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
        resolutions.AddRange(list);
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(resolutions.Select(r => $"{r.x} x {r.y}").ToList());
        resolutionDropdown.onValueChanged.AddListener(i => { if (!ignoreEvents) ApplyResolution(); });
        fullscreenToggle.onValueChanged.AddListener(v => { if (!ignoreEvents) ApplyResolution(); });

        zoomSlider.minValue = GameSettings.MinZoom;
        zoomSlider.maxValue = GameSettings.MaxZoom;
        zoomSlider.onValueChanged.AddListener(v =>
        {
            v = Mathf.Round(v * 20f) / 20f;           // шаг 0.05
            if (!ignoreEvents) GameSettings.SetCameraZoom(v);
            RefreshLabels();
        });

        // Controls
        for (int i = 0; i < keyButtons.Length; i++)
        {
            int a = i;
            keyButtons[i].onClick.AddListener(() => StartRebind(a));
        }

        LoadValuesIntoUI();
        ShowMain();
    }

    private void LoadValuesIntoUI()
    {
        ignoreEvents = true;
        volumeSlider.value = GameSettings.Volume;
        zoomSlider.value = GameSettings.CameraZoom;
        int idx = resolutions.IndexOf(new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight));
        resolutionDropdown.value = Mathf.Max(0, idx);
        resolutionDropdown.RefreshShownValue();
        fullscreenToggle.isOn = GameSettings.Fullscreen;
        ignoreEvents = false;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
        if (zoomValue != null) zoomValue.text = "x" + zoomSlider.value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 0; i < keyButtons.Length; i++)
        {
            var t = keyButtons[i].GetComponentInChildren<Text>();
            t.text = rebinding == i ? "Press a key..." : GameSettings.KeyName(GameSettings.GetKey((GameSettings.Action)i));
        }
        if (controlsHint != null)
            controlsHint.text = rebinding >= 0
                ? "Press the new key for \"" + GameSettings.ActionNames[rebinding] + "\". Esc — cancel."
                : "Click a key to change it. Space always jumps too.";
    }

    private void ApplyResolution()
    {
        var r = resolutions[Mathf.Clamp(resolutionDropdown.value, 0, resolutions.Count - 1)];
        GameSettings.SetResolution(r.x, r.y, fullscreenToggle.isOn);
    }

    // ---------- кнопки ----------

    public void Play() => SceneManager.LoadScene(gameScene);

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowMain()
    {
        CancelRebind();
        mainPanel.SetActive(true);
        optionsPanel.SetActive(false);
    }

    public void ShowOptions()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
        LoadValuesIntoUI();
        ShowTab(0);
    }

    public void ShowTab(int index)
    {
        CancelRebind();
        for (int i = 0; i < tabPages.Length; i++)
        {
            tabPages[i].SetActive(i == index);
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null) img.color = i == index ? tabActive : tabIdle;
            var t = tabButtons[i].GetComponentInChildren<Text>();
            if (t != null) t.color = i == index ? new Color(0.15f, 0.12f, 0.05f) : Color.white;
        }
    }

    public void PresetWASD() { CancelRebind(); GameSettings.SetPreset(GameSettings.PresetWASD); RefreshLabels(); }
    public void PresetArrows() { CancelRebind(); GameSettings.SetPreset(GameSettings.PresetArrows); RefreshLabels(); }

    // ---------- смена клавиши ----------

    private void StartRebind(int action)
    {
        // снимаем выделение с кнопки: иначе Space/Enter «нажмут» её снова вместо назначения
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        rebinding = action;
        RefreshLabels();
    }

    private void CancelRebind()
    {
        if (rebinding < 0) return;
        rebinding = -1;
        RefreshLabels();
    }

    private void Update()
    {
        if (rebinding >= 0)
        {
            if (!Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelRebind(); return; }
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;    // мышь не назначаем
                if (k >= KeyCode.JoystickButton0) continue;
                if (Input.GetKeyDown(k))
                {
                    GameSettings.SetKey((GameSettings.Action)rebinding, k);
                    rebinding = -1;
                    RefreshLabels();
                    return;
                }
            }
            return;
        }

        // Esc в настройках — назад
        if (optionsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) ShowMain();
    }
}
