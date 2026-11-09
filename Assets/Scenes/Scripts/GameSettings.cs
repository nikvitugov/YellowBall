
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Настройки игры: громкость, разрешение, приближение камеры, клавиши управления.
/// Хранятся в PlayerPrefs, применяются сами при запуске игры. Меню — MainMenu.cs.
/// </summary>
public static class GameSettings
{
    public enum Action { Left, Right, Jump }

    public static readonly string[] ActionNames = { "Move left", "Move right", "Jump" };

    public static readonly KeyCode[] PresetWASD = { KeyCode.A, KeyCode.D, KeyCode.W };
    public static readonly KeyCode[] PresetArrows = { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow };

    /// <summary>Прыжок всегда работает и на пробел — в дополнение к назначенной клавише.</summary>
    public const KeyCode ExtraJumpKey = KeyCode.Space;

    public const float MinZoom = 0.5f, MaxZoom = 1.5f;
    public const string MenuScene = "GameMenu";

    /// <summary>Громкость 0..1 (потом разделим на музыку и эффекты).</summary>
    public static float Volume { get; private set; } = 1f;
    /// <summary>Приближение камеры: 1 — как в сцене, 1.5 — в 1.5 раза ближе к герою, 0.5 — в 2 раза дальше.</summary>
    public static float CameraZoom { get; private set; } = 1f;
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }
    public static bool Fullscreen { get; private set; } = true;

    private static KeyCode[] keys = (KeyCode[])PresetWASD.Clone();

    /// <summary>Что-то поменялось (камера, громкость...). Подписываются игровые скрипты и меню.</summary>
    public static event System.Action<string> Changed;

    // ---------- клавиши ----------

    public static KeyCode GetKey(Action a) => keys[(int)a];

    public static bool Held(Action a) =>
        Input.GetKey(keys[(int)a]) || (a == Action.Jump && Input.GetKey(ExtraJumpKey));

    /// <summary>Назначить клавишу. Если она уже занята другим действием — меняем их местами.</summary>
    public static void SetKey(Action a, KeyCode key)
    {
        int i = (int)a;
        for (int k = 0; k < keys.Length; k++)
            if (k != i && keys[k] == key) keys[k] = keys[i];
        keys[i] = key;
        Save();
        Changed?.Invoke("keys");
    }

    public static void SetPreset(KeyCode[] preset)
    {
        keys = (KeyCode[])preset.Clone();
        Save();
        Changed?.Invoke("keys");
    }

    /// <summary>Короткое имя клавиши для кнопки: «A», «←», «Space».</summary>
    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.Space: return "Space";
            case KeyCode.LeftShift: return "L Shift";
            case KeyCode.RightShift: return "R Shift";
            case KeyCode.LeftControl: return "L Ctrl";
            case KeyCode.RightControl: return "R Ctrl";
            case KeyCode.Return: return "Enter";
        }
        string s = k.ToString();
        return s.StartsWith("Alpha") ? s.Substring(5) : s;
    }

    // ---------- громкость / камера / экран ----------

    public static void SetVolume(float v)
    {
        Volume = Mathf.Clamp01(v);
        AudioListener.volume = Volume;
        Save();
        Changed?.Invoke("volume");
    }

    public static void SetCameraZoom(float z)
    {
        CameraZoom = Mathf.Clamp(z, MinZoom, MaxZoom);
        Save();
        Changed?.Invoke("zoom");
    }

    public static void SetResolution(int width, int height, bool fullscreen)
    {
        ResolutionWidth = width;
        ResolutionHeight = height;
        Fullscreen = fullscreen;
        ApplyResolution();
        Save();
        Changed?.Invoke("resolution");
    }

    private static void ApplyResolution()
    {
        if (ResolutionWidth <= 0 || ResolutionHeight <= 0) return;
        // в редакторе размер задаёт окно Game, это сработает только в собранной игре
        Screen.SetResolution(ResolutionWidth, ResolutionHeight,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    // ---------- сохранение ----------

    public static void Load()
    {
        Volume = PlayerPrefs.GetFloat("volume", 1f);
        CameraZoom = Mathf.Clamp(PlayerPrefs.GetFloat("cameraZoom", 1f), MinZoom, MaxZoom);
        ResolutionWidth = PlayerPrefs.GetInt("resW", Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt("resH", Screen.currentResolution.height);
        Fullscreen = PlayerPrefs.GetInt("fullscreen", 1) == 1;
        for (int i = 0; i < keys.Length; i++)
            keys[i] = (KeyCode)PlayerPrefs.GetInt("key" + i, (int)PresetWASD[i]);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("volume", Volume);
        PlayerPrefs.SetFloat("cameraZoom", CameraZoom);
        PlayerPrefs.SetInt("resW", ResolutionWidth);
        PlayerPrefs.SetInt("resH", ResolutionHeight);
        PlayerPrefs.SetInt("fullscreen", Fullscreen ? 1 : 0);
        for (int i = 0; i < keys.Length; i++) PlayerPrefs.SetInt("key" + i, (int)keys[i]);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Load();
        AudioListener.volume = Volume;
        if (!Application.isEditor) ApplyResolution();

        // Esc в игре — назад в меню
        var go = new GameObject("GameSettings") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<EscToMenu>();
    }

    private class EscToMenu : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && SceneManager.GetActiveScene().name != MenuScene
                && Application.CanStreamedLevelBeLoaded(MenuScene))
                SceneManager.LoadScene(MenuScene);
        }
    }
}
