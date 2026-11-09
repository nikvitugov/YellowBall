using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Yellow Ball / Setup Layers & Quality:
///  1) три Sorting Layer: Background -> Default (= Middle) -> Foreground;
///  2) LocationLayer на префабах уровня и фоне (порядок внутри Middle фиксирован, без мерцания);
///  3) качество картинки: сглаживание (MSAA) у камеры и в настройках качества, текстуры без сжатия.
/// Можно запускать повторно.
/// </summary>
public static class LayersSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // префаб -> (слой, порядок внутри слоя)
    static readonly (string prefab, LocationLayer.Layer layer, int order)[] Prefabs =
    {
        ("Assets/Scenes/Prefab/Ground.prefab",          LocationLayer.Layer.Middle, 0),
        ("Assets/Scenes/Prefab/Platform.prefab",        LocationLayer.Layer.Middle, 1),
        ("Assets/Scenes/Prefab/FallingPlatform.prefab", LocationLayer.Layer.Middle, 2),
        ("Assets/Scenes/Prefab/Square.prefab",          LocationLayer.Layer.Middle, 3),
        ("Assets/Scenes/Prefab/WallClimbing.prefab",    LocationLayer.Layer.Middle, 4),
        ("Assets/Scenes/Prefab/Checkpoint.prefab",      LocationLayer.Layer.Middle, 5),
        ("Assets/Scenes/Prefab/Triangle.prefab",        LocationLayer.Layer.Middle, 6),
        ("Assets/Scenes/Prefab/Player.prefab",          LocationLayer.Layer.Middle, 10),
    };

    // текстуры, которые видно крупно — без сжатия, чтобы не мылились
    static readonly string[] SharpTextures =
    {
        "Assets/Scenes/Textures/asset02.png",
        "Assets/Scenes/Textures/grass_top.png",
        "Assets/Scenes/Textures/grass_soil.png",
        "Assets/Scenes/Textures/old-wood-planks_prw.jpg",
        "Assets/Scenes/Textures/rough-stone-texture-photo_seamless.jpg",
        "Assets/Scenes/Textures/hero3d_tex.png",
        "Assets/Scenes/Models/HeroAI/hero_ai_tex.jpg",
    };

    [MenuItem("Yellow Ball/Setup Layers & Quality")]
    public static void Run()
    {
        CreateSortingLayers();

        // префабы
        foreach (var (path, layer, order) in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            var ll = root.GetComponent<LocationLayer>();
            if (ll == null) ll = root.AddComponent<LocationLayer>();
            ll.layer = layer;
            ll.order = order;
            ll.Apply();
            if (path.EndsWith("Player.prefab"))
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true)) cam.allowMSAA = true;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // сцена: фон в Background, у экземпляров убираем переопределения слоя (пусть берут из префаба)
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var so = new SerializedObject(r);
                    foreach (var prop in new[] { "m_SortingLayerID", "m_SortingLayer", "m_SortingOrder" })
                    {
                        var p = so.FindProperty(prop);
                        if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                    }
                }
                foreach (var ll in root.GetComponentsInChildren<LocationLayer>(true)) ll.Apply();
                continue;
            }
            var sr = root.GetComponent<SpriteRenderer>();
            if (sr != null && root.GetComponent<Collider2D>() == null)
            {
                // спрайт без коллайдера — декорация; самая большая (фон) идёт в Background
                var ll = root.GetComponent<LocationLayer>();
                if (ll == null) ll = root.AddComponent<LocationLayer>();
                ll.layer = LocationLayer.Layer.Background;
                ll.order = 0;
                ll.Apply();
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        SetupQuality();
        foreach (var path in SharpTextures)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            ti.anisoLevel = Mathf.Max(ti.anisoLevel, 4);
            ti.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("LayersSetup: готово. Слои: " + string.Join(" < ", SortingLayer.layers.Select(l => l.name)));
    }

    static void CreateSortingLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("m_SortingLayers");

        int IndexOf(string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
            return -1;
        }

        void Add(string name, int index)
        {
            if (IndexOf(name) >= 0) return;
            layers.InsertArrayElementAtIndex(index);
            var e = layers.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("name").stringValue = name;
            e.FindPropertyRelative("uniqueID").intValue = (name.GetHashCode() & 0x7ffffff0) | 1;   // уникальный, не 0
            e.FindPropertyRelative("locked").intValue = 0;
        }

        Add("Background", 0);                              // до Default -> рисуется раньше (позади)
        Add("Foreground", layers.arraySize);               // после всего -> поверх
        // порядок: Background, Default, Foreground
        int bg = IndexOf("Background"), def = IndexOf("Default"), fg = IndexOf("Foreground");
        if (bg > def) layers.MoveArrayElement(bg, def);
        def = IndexOf("Default"); fg = IndexOf("Foreground");
        if (fg < def) layers.MoveArrayElement(fg, layers.arraySize - 1);
        tagManager.ApplyModifiedProperties();
    }

    static void SetupQuality()
    {
        var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var list = qs.FindProperty("m_QualitySettings");
        for (int i = 0; i < list.arraySize; i++)
        {
            var q = list.GetArrayElementAtIndex(i);
            string name = q.FindPropertyRelative("name").stringValue;
            int aa = name == "Ultra" ? 8 : name == "Very High" || name == "High" ? 4 : name == "Medium" ? 2 : 0;
            q.FindPropertyRelative("antiAliasing").intValue = aa;
            q.FindPropertyRelative("anisotropicTextures").intValue = aa > 0 ? 2 : 1;   // 2 = Forced On
        }
        qs.ApplyModifiedProperties();
    }

    /// <summary>Проверка: снимок с камеры игры в %TEMP%/layers_shot.png</summary>
    public static void Screenshot()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var cam = Camera.main;
        var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "layers_shot.png"), shot.EncodeToPNG());
        Debug.Log("LayersSetup: AA=" + QualitySettings.antiAliasing + ", msaa камеры=" + cam.allowMSAA);
    }
}
