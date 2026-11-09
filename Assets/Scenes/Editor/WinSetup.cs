using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Меню Yellow Light / Setup Win Zone: ставит финиш (WinZone) на WallClimbing тестовой карты.</summary>
public static class WinSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Yellow Light/Setup Win Zone")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.CompareTag("WallClimbing")) continue;
            if (root.GetComponent<WinZone>() == null) Undo.AddComponent<WinZone>(root);
            Debug.Log("WinSetup: финиш на " + root.name + " " + root.transform.position);
            n++;
        }
        if (n == 0) { Debug.LogError("WinSetup: в сцене нет объекта с тегом WallClimbing"); return; }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
