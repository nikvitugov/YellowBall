using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ставит сгенерированную модель героя (Tools/inflate_hero.py -> hero3d.obj) на место старой.
/// Меню: Yellow Ball / Use 3D Hero. Можно запускать повторно после перегенерации модели.
/// </summary>
public static class HeroSwap
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ObjPath = "Assets/Scenes/Textures/hero3d.obj";
    const string TexPath = "Assets/Scenes/Textures/hero3d_tex.png";
    const string MatPath = "Assets/Scenes/Textures/hero3d_Material.mat";
    const string NewName = "Hero3D";

    [MenuItem("Yellow Ball/Use 3D Hero")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var mesh = AssetDatabase.LoadAllAssetsAtPath(ObjPath).OfType<Mesh>().FirstOrDefault();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (mesh == null || tex == null)
        {
            Debug.LogError("HeroSwap: нет " + ObjPath + " или " + TexPath + ". Сначала запусти Tools/inflate_hero.py");
            return;
        }

        // Материал: обычный Standard, непрозрачный, слегка матовый
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.25f);
        mat.SetFloat("_Metallic", 0f);
        // Перс светящийся: текстура светится сама, не завися от освещения сцены
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", tex);
        mat.SetColor("_EmissionColor", new Color(0.75f, 0.75f, 0.75f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath);

        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("HeroSwap: в сцене нет Player"); return; }
        var visualComp = player.GetComponent<PlayerVisual>();

        // Старая модель (FBX) — берём её размеры, потом выключаем
        Transform hero3d = player.transform.Find(NewName);
        Transform old = null;
        foreach (Transform child in player.transform)
            if (child.name != NewName && child.GetComponentInChildren<MeshRenderer>(true) != null) { old = child; break; }

        Bounds target;
        if (old != null && old.gameObject.activeSelf)
            target = old.GetComponentInChildren<MeshRenderer>().bounds;
        else if (hero3d != null)
            target = hero3d.GetComponent<MeshRenderer>().bounds;
        else
            target = new Bounds(player.transform.position, Vector3.one);

        if (hero3d == null)
        {
            var go = new GameObject(NewName, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, "Use 3D Hero");
            hero3d = go.transform;
            hero3d.SetParent(player.transform, false);
        }

        hero3d.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = hero3d.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // Масштаб: по высоте старой модели; низ и центр по X совпадают
        float s = target.size.y / mesh.bounds.size.y;
        hero3d.localRotation = Quaternion.identity;
        hero3d.localScale = Vector3.one * s;
        Vector3 meshBottom = new Vector3(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z) * s;
        Vector3 worldBottom = new Vector3(target.center.x, target.min.y, player.transform.position.z);
        hero3d.localPosition = player.transform.InverseTransformPoint(worldBottom) - meshBottom;

        if (old != null) old.gameObject.SetActive(false);
        if (visualComp != null)
        {
            visualComp.visual = hero3d;
            EditorUtility.SetDirty(visualComp);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"HeroSwap: готово. Масштаб {s:F3}, старая модель '{(old != null ? old.name : "-")}' выключена");
    }

    /// <summary>Для проверки из batch-режима: снимок с камеры игры в %TEMP%/hero_shot.png</summary>
    public static void Screenshot()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var cam = Camera.main;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        shot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        string path = Path.Combine(Path.GetTempPath(), "hero_shot.png");
        File.WriteAllBytes(path, shot.EncodeToPNG());
        Debug.Log("HeroSwap: снимок сохранён в " + path);
    }
}
