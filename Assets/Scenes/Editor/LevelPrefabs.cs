using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Меню Yellow Light / Convert Level To Prefabs: превращает объекты уровня в экземпляры префабов.
/// Объекты группируются по имени без номера («Platform (12)» -> Platform). Для каждой группы
/// префаб создаётся из первого объекта (или берётся существующий, как Triangle), а остальные
/// подключаются к нему. Всё, чем объект отличался (позиция, размер, цвет, коллайдер, гравитация),
/// сохраняется как override, так что уровень выглядит и работает как раньше.
/// </summary>
public static class LevelPrefabs
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string PrefabDir = "Assets/Scenes/Prefab";

    // имя в сцене -> имя префаба; null = не трогаем
    static string PrefabNameFor(GameObject go)
    {
        string baseName = Regex.Replace(go.name, @" \(\d+\)$", "");
        switch (baseName)
        {
            case "Platform": return go.GetComponent<Rigidbody2D>() != null ? "FallingPlatform" : "Platform";
            case "Ground": return "Ground";
            case "Triangle": return "Triangle";
            case "checkpoint": return "Checkpoint";
            case "WallClimbing": return "WallClimbing";
            case "Square": return "Square";
            case "Player": return "Player";
            default: return null;   // фон, свет, камера и т.п.
        }
    }

    [MenuItem("Yellow Light/Convert Level To Prefabs")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        Directory.CreateDirectory(PrefabDir);

        var groups = new Dictionary<string, List<GameObject>>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root)) continue;   // уже префаб
            string name = PrefabNameFor(root);
            if (name == null) continue;
            if (!groups.TryGetValue(name, out var list)) groups[name] = list = new List<GameObject>();
            list.Add(root);
        }

        var settings = new ConvertToPrefabInstanceSettings
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            recordPropertyOverridesOfMatches = true,
            changeRootNameToAssetName = false,
            logInfo = false,
        };

        var report = new StringBuilder();
        foreach (var kv in groups.OrderBy(k => k.Key))
        {
            string path = $"{PrefabDir}/{kv.Key}.prefab";
            var members = kv.Value;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int created = 0;
            if (asset == null)
            {
                // префаб из первого объекта; сам объект сразу становится его экземпляром
                var first = members[0];
                asset = PrefabUtility.SaveAsPrefabAssetAndConnect(first, path, InteractionMode.AutomatedAction);
                if (asset == null) { Debug.LogError("LevelPrefabs: не удалось создать " + path); continue; }
                ResetRootTransform(path);
                members = members.Skip(1).ToList();
                created = 1;
            }
            if (members.Count > 0)
                PrefabUtility.ConvertToPrefabInstances(members.ToArray(), asset, settings, InteractionMode.AutomatedAction);
            report.AppendLine($"  {kv.Key}: {members.Count + created} шт.{(created == 1 ? " (префаб создан)" : "")}");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("LevelPrefabs: готово\n" + report);
    }

    // У самого префаба позиция корня (0,0,0) — так аккуратнее; у объектов в сцене позиции остаются свои
    static void ResetRootTransform(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        root.transform.localPosition = Vector3.zero;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------- проверка: снимок состояния уровня до и после ----------

    /// <summary>Пишет в %TEMP%/level_dump.txt всё важное про каждый объект (для сравнения до/после).</summary>
    public static void Dump()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var lines = new List<string>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                lines.Add(Describe(t.gameObject));
        lines.Sort();
        string file = Path.Combine(Path.GetTempPath(), "level_dump.txt");
        File.WriteAllLines(file, lines);
        Debug.Log($"LevelPrefabs: {lines.Count} объектов -> {file}");
    }

    static string Describe(GameObject go)
    {
        var t = go.transform;
        string path = go.name;
        for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        var sb = new StringBuilder();
        sb.Append($"{path} | active={go.activeSelf} tag={go.tag} layer={go.layer} pos={F(t.position)} rot={F(t.eulerAngles)} scale={F(t.lossyScale)}");
        foreach (var c in go.GetComponents<Component>())
        {
            switch (c)
            {
                case SpriteRenderer sr: sb.Append($" | sprite={(sr.sprite ? sr.sprite.name : "-")} color={sr.color} size={F(sr.size)} order={sr.sortingOrder} flip={sr.flipX},{sr.flipY}"); break;
                case BoxCollider2D b: sb.Append($" | box size={F(b.size)} off={F(b.offset)} trig={b.isTrigger} en={b.enabled}"); break;
                case PolygonCollider2D pc: sb.Append($" | poly n={pc.points.Length} p0={(pc.points.Length > 0 ? F(pc.points[0]) : "-")} trig={pc.isTrigger}"); break;
                case CapsuleCollider2D cc: sb.Append($" | capsule {F(cc.size)} trig={cc.isTrigger}"); break;
                case Rigidbody2D rb: sb.Append($" | rb type={rb.bodyType} grav={rb.gravityScale} mass={rb.mass} constr={rb.constraints} interp={rb.interpolation}"); break;
                case MeshFilter mf: sb.Append($" | mesh={(mf.sharedMesh ? mf.sharedMesh.name : "-")}"); break;
                case MeshRenderer mr: sb.Append($" | mat={(mr.sharedMaterial ? mr.sharedMaterial.name : "-")} en={mr.enabled}"); break;
                case Light l: sb.Append($" | light {l.type} {l.intensity} {l.color}"); break;
                case MonoBehaviour mb: sb.Append($" | {mb.GetType().Name} {Regex.Replace(EditorJsonUtility.ToJson(mb), "\"instanceID\":-?\\d+", "\"instanceID\":X")}"); break;
            }
        }
        return sb.ToString();
    }

    static string F(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
    static string F(Vector2 v) => $"({v.x:F3},{v.y:F3})";
}
