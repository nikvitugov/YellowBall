using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Yellow Ball / Apply Grass: трава на все платформы.
/// Настраивает текстуры (Tools/make_grass.py -> grass_top.png, grass_soil.png), создаёт материал
/// с шейдером YellowBall/GrassPlatform и ставит его префабам Platform, FallingPlatform, Ground, Square.
/// </summary>
public static class GrassSetup
{
    const string TopTex = "Assets/Scenes/Textures/grass_top.png";
    const string SoilTex = "Assets/Scenes/Textures/grass_soil.png";
    const string MatPath = "Assets/Scenes/Materials/GrassPlatform.mat";
    static readonly string[] Prefabs =
    {
        "Assets/Scenes/Prefab/Platform.prefab",
        "Assets/Scenes/Prefab/FallingPlatform.prefab",
        "Assets/Scenes/Prefab/Ground.prefab",
        "Assets/Scenes/Prefab/Square.prefab",
    };

    [MenuItem("Yellow Ball/Apply Grass")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        SetupTexture(TopTex, TextureWrapMode.Repeat, TextureWrapMode.Clamp, alpha: true);
        SetupTexture(SoilTex, TextureWrapMode.Repeat, TextureWrapMode.Repeat, alpha: false);

        var top = AssetDatabase.LoadAssetAtPath<Texture2D>(TopTex);
        var soil = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilTex);
        var shader = Shader.Find("YellowBall/GrassPlatform");
        if (top == null || soil == null || shader == null)
        {
            Debug.LogError("GrassSetup: нет текстур (запусти Tools/make_grass.py) или шейдера YellowBall/GrassPlatform");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MatPath));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.shader = shader;
        mat.SetTexture("_GrassTex", top);
        mat.SetTexture("_SoilTex", soil);
        float stripH = mat.GetFloat("_StripHeight");
        mat.SetFloat("_TileWidth", stripH * top.width / top.height);          // трава без искажений
        mat.SetFloat("_SoilAspect", (float)soil.width / soil.height);
        mat.SetFloat("_SoilTileWidth", mat.GetFloat("_TileWidth") * 3f);   // земля крупнее, чтобы не было видно повторов
        EditorUtility.SetDirty(mat);

        int changed = 0;
        foreach (var path in Prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            var sr = root.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sharedMaterial = mat;
                sr.color = Color.white;      // цвет квадрата больше не нужен (шейдер его не использует)
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"GrassSetup: готово, префабов с травой: {changed}. Ширина повтора {mat.GetFloat("_TileWidth"):F2} юн.");
    }

    static void SetupTexture(string path, TextureWrapMode u, TextureWrapMode v, bool alpha)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = alpha;
        ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.mipmapEnabled = true;
        ti.wrapModeU = u;
        ti.wrapModeV = v;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
    }

    /// <summary>Проверка: снимки с камеры игры, обычный и отдалённый, в %TEMP%/grass_*.png</summary>
    public static void Screenshots()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var cam = Camera.main;
        Shot(cam, "grass_near.png");
        var p = cam.transform.position;
        cam.transform.position = p + new Vector3(8f, 2f, -14f);
        Shot(cam, "grass_far.png");
        cam.transform.position = p + new Vector3(40f, 6f, -30f);
        Shot(cam, "grass_far2.png");
    }

    static void Shot(Camera cam, string file)
    {
        var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), file), shot.EncodeToPNG());
    }
}
