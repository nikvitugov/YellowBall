using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Меню Yellow Light / Build Game Menu: собирает интерфейс главного меню в сцене GameMenu
/// (Play / Options / Quit, вкладки Audio / Video / Controls) и ставит GameMenu первой сценой сборки.
/// Повторный запуск пересобирает интерфейс с нуля (объект MenuCanvas заменяется).
/// После сборки всё — обычные UI-объекты, их можно двигать и красить в редакторе.
/// </summary>
public static class MenuBuilder
{
    const string MenuScenePath = "Assets/Scenes/GameMenu.unity";
    const string GameScenePath = "Assets/Scenes/SampleScene.unity";

    static readonly Color Yellow = new Color(1f, 0.83f, 0.3f);
    static readonly Color Dark = new Color(0.15f, 0.12f, 0.05f);
    static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.1f, 0.72f);
    static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.14f);

    static DefaultControls.Resources res;
    static Font font;

    [MenuItem("Yellow Light/Build Game Menu")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(MenuScenePath);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "MenuCanvas" || root.name == "EventSystem") Object.DestroyImmediate(root);

        res = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // EventSystem
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        SceneManager_Move(es, scene);

        // Canvas
        var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager_Move(canvasGo, scene);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var menu = canvasGo.AddComponent<MainMenu>();
        var ct = canvasGo.transform;

        // ---------- главное меню (справа, герой виден слева) ----------
        var main = Panel(ct, "MainPanel", new Vector2(1f, 0.5f), new Vector2(-380, 0), new Vector2(560, 680));
        var title = Label(main, "Title", "YELLOW LIGHT", 68, new Vector2(0, 220), new Vector2(540, 110), Yellow, FontStyle.Bold);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        var play = MenuButton(main, "Play", "Play", new Vector2(0, 60), new Vector2(420, 96), primary: true);
        var options = MenuButton(main, "Options", "Options", new Vector2(0, -60), new Vector2(420, 96));
        var quit = MenuButton(main, "Quit", "Quit", new Vector2(0, -180), new Vector2(420, 96));
        UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
        UnityEventTools.AddPersistentListener(options.onClick, menu.ShowOptions);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        // ---------- настройки ----------
        var opt = Panel(ct, "OptionsPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 780));
        Label(opt, "Title", "OPTIONS", 60, new Vector2(0, 320), new Vector2(600, 90), Yellow, FontStyle.Bold);

        string[] tabNames = { "Audio", "Video", "Controls" };
        var tabs = new Button[3];
        var pages = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            tabs[i] = MenuButton(opt, "Tab" + tabNames[i], tabNames[i], new Vector2(-340 + 340 * i, 220), new Vector2(310, 76));
            UnityEventTools.AddIntPersistentListener(tabs[i].onClick, menu.ShowTab, i);
            var page = new GameObject("Page" + tabNames[i], typeof(RectTransform));
            page.transform.SetParent(opt, false);
            var prt = (RectTransform)page.transform;
            prt.anchoredPosition = new Vector2(0, -40);
            prt.sizeDelta = new Vector2(1080, 420);
            pages[i] = page;
        }
        var back = MenuButton(opt, "Back", "Back", new Vector2(0, -320), new Vector2(320, 80));
        UnityEventTools.AddPersistentListener(back.onClick, menu.ShowMain);

        // Audio
        var audio = pages[0].transform;
        RowLabel(audio, "Volume", 120);
        menu.volumeSlider = MakeSlider(audio, "VolumeSlider", new Vector2(120, 120));
        menu.volumeValue = Label(audio, "VolumeValue", "100%", 36, new Vector2(440, 120), new Vector2(160, 60), Color.white, FontStyle.Bold);
        Label(audio, "AudioNote", "Music and sound effects volume will be split later.", 26,
              new Vector2(0, -20), new Vector2(900, 50), new Color(1, 1, 1, 0.55f), FontStyle.Italic);

        // Video
        var video = pages[1].transform;
        RowLabel(video, "Resolution", 140);
        menu.resolutionDropdown = MakeDropdown(video, "ResolutionDropdown", new Vector2(150, 140), new Vector2(460, 66));
        RowLabel(video, "Fullscreen", 40);
        menu.fullscreenToggle = MakeToggle(video, "FullscreenToggle", new Vector2(-50, 40));
        RowLabel(video, "Camera zoom", -60);
        menu.zoomSlider = MakeSlider(video, "ZoomSlider", new Vector2(120, -60));
        menu.zoomValue = Label(video, "ZoomValue", "x1.00", 36, new Vector2(440, -60), new Vector2(160, 60), Color.white, FontStyle.Bold);
        Label(video, "ZoomNote", "x1.5 — camera is closer to the hero, x0.5 — farther away.", 26,
              new Vector2(0, -150), new Vector2(900, 50), new Color(1, 1, 1, 0.55f), FontStyle.Italic);

        // Controls
        var controls = pages[2].transform;
        for (int i = 0; i < 3; i++)
        {
            float y = 150 - 95 * i;
            RowLabel(controls, GameSettings.ActionNames[i], y);
            menu.keyButtons[i] = MenuButton(controls, "Key" + (GameSettings.Action)i, "-", new Vector2(40, y), new Vector2(240, 74));
        }
        RowLabel(controls, "Presets", -140);
        var wasd = MenuButton(controls, "PresetWASD", "WASD", new Vector2(10, -140), new Vector2(180, 70));
        var arrows = MenuButton(controls, "PresetArrows", "Arrows", new Vector2(210, -140), new Vector2(180, 70));
        UnityEventTools.AddPersistentListener(wasd.onClick, menu.PresetWASD);
        UnityEventTools.AddPersistentListener(arrows.onClick, menu.PresetArrows);
        menu.controlsHint = Label(controls, "Hint", "", 26, new Vector2(0, -215), new Vector2(1000, 50),
                                  new Color(1, 1, 1, 0.6f), FontStyle.Italic);

        menu.mainPanel = main.gameObject;
        menu.optionsPanel = opt.gameObject;
        menu.tabButtons = tabs;
        menu.tabPages = pages;
        menu.gameScene = "SampleScene";
        opt.gameObject.SetActive(false);
        EditorUtility.SetDirty(menu);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SetBuildScenes();
        Debug.Log("MenuBuilder: меню собрано в " + MenuScenePath);
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    static void SetBuildScenes()
    {
        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != MenuScenePath && s.path != GameScenePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------- строительные блоки ----------

    static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = res.standard;
        img.type = Image.Type.Sliced;
        img.color = PanelColor;
        return rt;
    }

    static Text Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 box, Color color,
                      FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
        return t;
    }

    static void RowLabel(Transform parent, string text, float y) =>
        Label(parent, "Label " + text, text, 36, new Vector2(-330, y), new Vector2(340, 60), Color.white,
              FontStyle.Normal, TextAnchor.MiddleLeft);

    static Button MenuButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, bool primary = false)
    {
        var go = DefaultControls.CreateButton(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = primary ? Yellow : ButtonIdle;
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.colorMultiplier = 1.6f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        var t = go.GetComponentInChildren<Text>();
        t.font = font;
        t.text = label;
        t.fontSize = Mathf.RoundToInt(size.y * 0.42f);
        t.fontStyle = FontStyle.Bold;
        t.color = primary ? Dark : Color.white;
        return b;
    }

    static Slider MakeSlider(Transform parent, string name, Vector2 pos)
    {
        var go = DefaultControls.CreateSlider(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(460, 34);
        var fill = go.transform.Find("Fill Area/Fill").GetComponent<Image>();
        fill.color = Yellow;
        var handle = (RectTransform)go.transform.Find("Handle Slide Area/Handle");
        handle.sizeDelta = new Vector2(44, 0);
        return go.GetComponent<Slider>();
    }

    static Toggle MakeToggle(Transform parent, string name, Vector2 pos)
    {
        var go = DefaultControls.CreateToggle(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(60, 60);
        var bg = (RectTransform)go.transform.Find("Background");
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
        bg.anchoredPosition = Vector2.zero; bg.sizeDelta = Vector2.zero;
        go.transform.Find("Background/Checkmark").GetComponent<Image>().color = Dark;
        bg.GetComponent<Image>().color = Yellow;
        Object.DestroyImmediate(go.transform.Find("Label").gameObject);
        return go.GetComponent<Toggle>();
    }

    static Dropdown MakeDropdown(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = DefaultControls.CreateDropdown(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        foreach (var t in go.GetComponentsInChildren<Text>(true))
        {
            t.font = font;
            t.fontSize = 30;
        }
        var template = (RectTransform)go.transform.Find("Template");
        template.sizeDelta = new Vector2(0, 360);
        var item = (RectTransform)go.transform.Find("Template/Viewport/Content/Item");
        item.sizeDelta = new Vector2(0, 54);
        var content = (RectTransform)go.transform.Find("Template/Viewport/Content");
        content.sizeDelta = new Vector2(0, 56);
        var arrow = (RectTransform)go.transform.Find("Arrow");
        arrow.sizeDelta = new Vector2(40, 40);
        return go.GetComponent<Dropdown>();
    }
}
