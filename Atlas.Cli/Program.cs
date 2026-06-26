using Atlas.Core;
using Atlas.Extraction;

// atlas — the desktop/CLI host for Roslyn extraction. It parses an Uno app's
// RegisterRoutes (an App.xaml.cs) into the cross-platform AppModel JSON the viewer
// and the agent both consume, keeping the viewer itself platform-clean.
//
//   atlas extract <App.xaml.cs> [--app <name>] [--out <file>] [--no-layout]
//
// --app      name stamped into the model (default: the source file's project folder)
// --out      write JSON to this file (default: stdout)
// --no-layout  skip the deterministic tree layout; leave Position null

try
{
    return Run(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Console.Error.WriteLine(Usage());
        return args.Length == 0 ? 1 : 0;
    }

    if (args[0] != "extract")
    {
        Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage()}");
        return 1;
    }

    string? path = null, app = null, outPath = null;
    var layout = true;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--app": app = Next(args, ref i, "--app"); break;
            case "--out": outPath = Next(args, ref i, "--out"); break;
            case "--no-layout": layout = false; break;
            default:
                if (args[i].StartsWith('-'))
                {
                    Console.Error.WriteLine($"Unknown option '{args[i]}'.\n\n{Usage()}");
                    return 1;
                }
                if (path is not null)
                {
                    Console.Error.WriteLine($"Unexpected argument '{args[i]}'.\n\n{Usage()}");
                    return 1;
                }
                path = args[i];
                break;
        }
    }

    if (path is null)
    {
        Console.Error.WriteLine($"Missing <App.xaml.cs> path.\n\n{Usage()}");
        return 1;
    }

    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    // Default the app name to the source's project folder (App.xaml.cs lives at the project root).
    app ??= new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!).Name;

    var model = RouteExtractor.ExtractFromFile(path, app, DateTimeOffset.Now);
    if (layout)
    {
        model = TreeLayout.Apply(model);
    }

    var json = AppModelJson.Serialize(model);
    if (outPath is null)
    {
        Console.WriteLine(json);
    }
    else
    {
        File.WriteAllText(outPath, json);
        Console.Error.WriteLine($"Wrote {model.Nodes.Count} nodes, {model.Edges.Count} edges → {outPath}");
    }

    return 0;
}

static string Next(string[] args, ref int i, string option)
{
    if (i + 1 >= args.Length)
    {
        throw new ArgumentException($"Option '{option}' needs a value.");
    }
    return args[++i];
}

static string Usage() =>
    """
    atlas extract <App.xaml.cs> [--app <name>] [--out <file>] [--no-layout]

      Parses an Uno app's RegisterRoutes into AppModel JSON.

      --app <name>   app name stamped into the model (default: project folder)
      --out <file>   write JSON to a file (default: stdout)
      --no-layout    skip deterministic tree layout (leave positions null)
    """;
