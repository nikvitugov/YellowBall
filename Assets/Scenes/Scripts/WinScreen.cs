using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Экран победы поверх игры: затемнение, «YOU WIN», подпись и кнопка Restart
/// (перезапускает текущий уровень). Собирается кодом, в сцене ничего настраивать не надо.
/// Restart также по R или Enter; Esc, как и везде в уровне, выходит в меню.
/// </summary>
public class WinScreen : MonoBehaviour
{
    static readonly Color Yellow = new Color(1f, 0.83f, 0.3f);
    static readonly Color Dark = new Color(0.15f, 0.12f, 0.05f);

    public static WinScreen Current { get; private set; }

    private CanvasGroup group;
    private RectTransform content;
    private float shownAt;

    public static WinScreen Show(string title, string subtitle)
    {
        if (Current != null) return Current;
        var go = new GameObject("WinScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Current = go.AddComponent<WinScreen>();
        Current.Build(title, subtitle);
        return Current;
    }

    private void Build(string title, string subtitle)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // затемнение на весь экран
        var dim = NewRect("Dim", transform);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.sizeDelta = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.6f);

        content = NewRect("Content", transform);
        content.sizeDelta = new Vector2(1000, 600);

        var t = MakeText("Title", content, font, title, 120, FontStyle.Bold, Yellow, new Vector2(0, 110), new Vector2(1200, 160));
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.5f);
        shadow.effectDistance = new Vector2(4, -4);
        MakeText("Subtitle", content, font, subtitle, 40, FontStyle.Italic, new Color(1, 1, 1, 0.7f), new Vector2(0, 10), new Vector2(1000, 60));

        // кнопка Restart в стиле главного меню
        var btnRt = NewRect("Restart", content);
        btnRt.anchoredPosition = new Vector2(0, -130);
        btnRt.sizeDelta = new Vector2(420, 96);
        var img = btnRt.gameObject.AddComponent<Image>();
        img.color = Yellow;
        var btn = btnRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.colorMultiplier = 1.6f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(Restart);
        var label = MakeText("Text", btnRt, font, "Restart", 40, FontStyle.Bold, Dark, Vector2.zero, Vector2.zero);
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        shownAt = Time.unscaledTime;
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float k = Mathf.Clamp01(t / 0.45f);
        group.alpha = k;
        // лёгкий «выпрыг» надписи
        float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
        content.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0, 1, k)) * pop;

        // клавиши — с небольшой задержкой, чтобы случайный прыжок не перезапустил уровень
        if (t > 0.6f && (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Restart();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Text MakeText(string name, Transform parent, Font font, string text, int size, FontStyle style,
                         Color color, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(name, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
