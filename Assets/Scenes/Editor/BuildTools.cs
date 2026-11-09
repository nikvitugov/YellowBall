using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Сборка игры одной командой, каждая платформа в свою папку (Builds/ в .gitignore):
///  - Yellow Light / Build Windows -> Builds/Windows/Yellow light.exe и архив Builds/YellowLight-Windows.zip
///  - Yellow Light / Build WebGL   -> Builds/WebGL/index.html и архив Builds/YellowLight-WebGL.zip
/// Архивы готовы к загрузке на itch.io: в Windows-архиве exe со всеми нужными файлами,
/// в WebGL-архиве index.html лежит в корне, как itch требует.
/// После WebGL-сборки активной платформой остаётся WebGL: Build Windows переключит обратно.
/// </summary>
public static class BuildTools
{
    const string PlayerPrefab = "Assets/Scenes/Prefab/Player.prefab";

    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));

    [MenuItem("Yellow Light/Build Windows")]
    public static void BuildWindows()
    {
        string dir = Path.Combine(Root, "Windows");
        Clean(dir);
        bool ok = Build(BuildTarget.StandaloneWindows64, Path.Combine(dir, PlayerSettings.productName + ".exe"));
        if (!ok) return;
        foreach (var d in Directory.GetDirectories(dir, "*DoNotShip*")) Directory.Delete(d, true);   // только для отладки
        Zip(dir, Path.Combine(Root, "YellowLight-Windows.zip"));
    }

    [MenuItem("Yellow Light/Build WebGL")]
    public static void BuildWebGL()
    {
        // gzip + запасная распаковка в браузере: работает на itch.io без настройки сервера
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        string dir = Path.Combine(Root, "WebGL");
        Clean(dir);
        if (!Build(BuildTarget.WebGL, dir)) return;
        Zip(dir, Path.Combine(Root, "YellowLight-WebGL.zip"));
    }

    static bool Build(BuildTarget target, string path)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"BuildTools: {target} -> {s.result}, {s.totalSize / (1024f * 1024f):F1} МБ, {s.totalTime.TotalSeconds:F0} с, ошибок {s.totalErrors}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        return s.result == BuildResult.Succeeded;
    }

    static void Clean(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
    }

    static void Zip(string dir, string zip)
    {
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(dir, zip, System.IO.Compression.CompressionLevel.Optimal, false);
        Debug.Log("BuildTools: архив " + zip);
    }
}
