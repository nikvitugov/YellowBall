using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Настройка игрока в сцене одной командой: меню Yellow Light / Setup Player.
///  - две модели героя: HeroAI (сгенерированная) и Hero3D (надутая из картинки, Tools/inflate_hero.py);
///    обе стоят на одном месте одного роста, в игре переключаются клавишей Tab;
///  - материалы, читаемые меши/текстуры (нужно эффекту распада);
///  - компоненты PlayerAudio, PlayerDeathEffect и CameraFollow со всеми ссылками.
/// Запускать можно повторно, например после перегенерации hero3d.obj.
/// </summary>
public static class PlayerSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    const string Hero3DObj = "Assets/Scenes/Textures/hero3d.obj";
    const string Hero3DTex = "Assets/Scenes/Textures/hero3d_tex.png";
    const string Hero3DMat = "Assets/Scenes/Textures/hero3d_Material.mat";

    const string HeroAIObj = "Assets/Scenes/Models/HeroAI/hero_ai.obj";
    const string HeroAITex = "Assets/Scenes/Models/HeroAI/hero_ai_tex.jpg";
    const string HeroAIMat = "Assets/Scenes/Models/HeroAI/hero_ai_Material.mat";

    const string ParticleMat = "Assets/Scenes/Materials/DeathParticles.mat";

    const float DefaultHeight = 0.98f;   // рост героя в юнитах (как у старой модели hero.fbx)

    [MenuItem("Yellow Light/Setup Player")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        MakeReadable(Hero3DObj, Hero3DTex);
        MakeReadable(HeroAIObj, HeroAITex);

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("PlayerSetup: в сцене нет Player"); return; }
        var pt = player.transform;

        // Рост и точка «ног»: от уже стоящей Hero3D, иначе от старой модели, иначе по умолчанию
        float height = DefaultHeight;
        Vector3 feet = Vector3.zero;
        var existing = pt.Find("Hero3D");
        var oldFbx = pt.Cast<Transform>().FirstOrDefault(c => c.name != "Hero3D" && c.name != "HeroAI"
                                                             && c.GetComponentInChildren<MeshRenderer>(true) != null);
        var existingMf = existing != null ? existing.GetComponent<MeshFilter>() : null;
        if (existingMf != null && existingMf.sharedMesh != null)
        {
            var b = existing.GetComponent<MeshFilter>().sharedMesh.bounds;
            float s = Mathf.Abs(existing.localScale.y);
            height = b.size.y * s;
            feet = existing.localPosition + existing.localRotation * Vector3.Scale(existing.localScale, new Vector3(b.center.x, b.min.y, b.center.z));
        }
        else if (oldFbx != null && oldFbx.gameObject.activeSelf)
        {
            var b = oldFbx.GetComponentInChildren<MeshRenderer>().bounds;
            height = b.size.y;
            feet = pt.InverseTransformPoint(new Vector3(b.center.x, b.min.y, pt.position.z));
        }

        var models = new System.Collections.Generic.List<Transform>();

        // Сгенерированная модель: объёмная, смотрит в +Z -> разворот на 180°, чтобы смотрела в камеру
        var ai = BuildModel(pt, "HeroAI", HeroAIObj, HeroAITex, HeroAIMat, height, feet,
                            Quaternion.Euler(0f, 180f, 0f), mirror: false, emission: 0.45f, flat: false);
        if (ai != null) models.Add(ai);

        // Надутая модель из картинки
        var inflated = BuildModel(pt, "Hero3D", Hero3DObj, Hero3DTex, Hero3DMat, height, feet,
                                  Quaternion.identity, mirror: false, emission: 0.75f, flat: true);
        if (inflated != null) models.Add(inflated);

        if (models.Count == 0) { Debug.LogError("PlayerSetup: не нашёл ни одной модели героя"); return; }
        if (oldFbx != null) oldFbx.gameObject.SetActive(false);

        // Визуал: список моделей, первая — по умолчанию
        var visual = GetOrAdd<PlayerVisual>(player);
        visual.models = models.ToArray();
        visual.activeModel = 0;
        visual.visual = models[0];
        for (int i = 0; i < models.Count; i++) models[i].gameObject.SetActive(i == 0);
        EditorUtility.SetDirty(visual);

        // Звуки
        var audio = GetOrAdd<PlayerAudio>(player);
        audio.jumpClip = FindClip("jumping");
        audio.stepsClip = FindClip("walking");
        EditorUtility.SetDirty(audio);

        // Распад на частицы
        var death = GetOrAdd<PlayerDeathEffect>(player);
        death.particleMaterial = GetParticleMaterial();
        EditorUtility.SetDirty(death);

        var move = player.GetComponent<PlayerMove>();
        if (move != null) { move.respawnDelay = 1f; EditorUtility.SetDirty(move); }

        // Камера
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = GetOrAdd<CameraFollow>(cam.gameObject);
            follow.target = pt;
            EditorUtility.SetDirty(follow);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"PlayerSetup: готово. Модели: {string.Join(", ", models.Select(m => m.name))}; рост {height:F3}");
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Transform BuildModel(Transform player, string name, string objPath, string texPath, string matPath,
                                float height, Vector3 feet, Quaternion rot, bool mirror, float emission, bool flat)
    {
        var mesh = AssetDatabase.LoadAllAssetsAtPath(objPath).OfType<Mesh>().FirstOrDefault();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (mesh == null || tex == null)
        {
            Debug.LogWarning($"PlayerSetup: пропускаю {name}: нет {objPath} или {texPath}");
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        // Перс светящийся: текстура светится сама, не завися от освещения сцены
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", Color.white * emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);

        var t = player.Find(name);
        if (t == null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Setup Player");
            t = go.transform;
            t.SetParent(player, false);
        }
        t.GetComponent<MeshFilter>().sharedMesh = mesh;
        t.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var info = GetOrAdd<HeroModelInfo>(t.gameObject);
        info.flat = flat;
        info.turnAngleScale = flat ? 0.6f : 1f;   // плоскую сильно поворачивать нельзя — видно, что она «блин»
        EditorUtility.SetDirty(info);

        float s = height / mesh.bounds.size.y;
        var scale = new Vector3(mirror ? -s : s, s, s);
        var b = mesh.bounds;
        t.localRotation = rot;
        t.localScale = scale;
        t.localPosition = feet - rot * Vector3.Scale(scale, new Vector3(b.center.x, b.min.y, b.center.z));
        return t;
    }

    static void MakeReadable(string objPath, string texPath)
    {
        if (AssetImporter.GetAtPath(objPath) is ModelImporter mi && !mi.isReadable)
        {
            mi.isReadable = true;
            mi.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (!ti.isReadable || ti.maxTextureSize < 2048))
        {
            ti.isReadable = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
    }

    static AudioClip FindClip(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null && clip.name == name) return clip;
        }
        Debug.LogWarning("PlayerSetup: не нашёл звук " + name);
        return null;
    }

    static Material GetParticleMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMat);
        if (mat != null) return mat;
        Directory.CreateDirectory(Path.GetDirectoryName(ParticleMat));
        var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        mat = new Material(shader);
        // без текстуры — квадратные «осколки»
        AssetDatabase.CreateAsset(mat, ParticleMat);
        return mat;
    }

    // ---------- проверки из batch-режима ----------

    /// <summary>Снимок с камеры игры в %TEMP%/hero_shot.png (активная модель).</summary>
    public static void Screenshot() => Shot("hero_shot.png", null);

    /// <summary>Снимки каждой модели героя: %TEMP%/hero_shot_*.png</summary>
    public static void ScreenshotAll()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var player = GameObject.Find("Player");
        var visual = player.GetComponent<PlayerVisual>();
        for (int i = 0; i < visual.models.Length; i++)
        {
            for (int k = 0; k < visual.models.Length; k++) visual.models[k].gameObject.SetActive(k == i);
            Shot($"hero_shot_{visual.models[i].name}.png", player.transform);
        }

    }

    static void Shot(string file, Transform focus)
    {
        if (focus == null) EditorSceneManager.OpenScene(ScenePath);
        var cam = Camera.main;
        var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        string path = Path.Combine(Path.GetTempPath(), file);
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Debug.Log("PlayerSetup: снимок " + path);
    }
}
