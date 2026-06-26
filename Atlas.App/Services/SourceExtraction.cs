#if ATLAS_EXTRACT
using Atlas.Core;
using Atlas.Extraction;

namespace Atlas.App.Services;

/// <summary>
/// Desktop-only Roslyn pipeline: an app's <c>App.xaml.cs</c> → a laid-out <see cref="AppModel"/>
/// (route tree + flow triggers from the surrounding project + tree layout). Shared by the
/// "Extract from source…" command and the launch-arg boot path.
/// </summary>
internal static class SourceExtraction
{
    public static AppModel FromAppSource(string appXamlCsPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(appXamlCsPath))!;
        var app = new DirectoryInfo(dir).Name;

        var model = RouteExtractor.ExtractFromFile(appXamlCsPath, app, DateTimeOffset.Now);
        model = TriggerExtractor.AddTriggersFromFiles(model, ProjectSources(dir));
        return TreeLayout.Apply(model);
    }

    // Every .xaml / .cs under the project, minus build output that would inject phantom edges.
    private static IEnumerable<string> ProjectSources(string dir) =>
        Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                      || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
#endif
