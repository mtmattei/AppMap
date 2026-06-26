# Running Atlas as a tool

Atlas has two halves: the **viewer** (the desktop app you look at) and the **agent**
(a tiny package your app references so the viewer can see it navigate).

## 0. The `atlas` CLI (global tool)

Pack and install the command-line tool once; then `atlas` is on your PATH everywhere:

```powershell
dotnet pack .\Atlas.Cli\Atlas.Cli.csproj -c Release        # → .\artifacts\Atlas.Tool.0.1.0.nupkg
dotnet tool install --global --add-source .\artifacts Atlas.Tool
```

```powershell
atlas extract <App.xaml.cs> --source <projectDir> --out app.json   # source → AppModel JSON
atlas view app.json --viewer <path\to\Atlas.App.exe>               # launch the viewer into a model
```

`atlas view` reads `ATLAS_VIEWER` when `--viewer` is omitted. Update later with
`dotnet tool update --global --add-source .\artifacts Atlas.Tool`.

## 1. Get the viewer

### Run from source (today)

```powershell
dotnet run -f net10.0-desktop --project .\Atlas.App\Atlas.App.csproj
```

### Publish a self-contained build (share with a teammate)

```powershell
dotnet publish .\Atlas.App\Atlas.App.csproj -f net10.0-desktop -c Release `
  /p:PublishProfile=win-x64
```

The output under `Atlas.App\bin\Release\net10.0-desktop\win-x64\publish\` is a
standalone folder — copy it anywhere and run `Atlas.App.exe`, no .NET install
required. Profiles also exist for `win-arm64` and `win-x86`; on macOS/Linux
publish with `-r osx-arm64` / `-r linux-x64`.

Once running, the viewer loads the bundled sample. Use **Open model…** in the top
bar to point it at any exported app-model JSON.

## 2. Add the agent to your app

Atlas.Agent is built as a NuGet (`dotnet pack Atlas.Agent -c Release -o artifacts`).
From a local feed or nuget.org, reference it in **Debug only**:

```xml
<ItemGroup Condition="'$(Configuration)'=='Debug'">
  <PackageReference Include="Atlas.Agent" Version="0.1.0" />
</ItemGroup>
```

Start it once, after your host is built:

```csharp
Host = await builder.NavigateAsync<Shell>();

#if DEBUG
_ = Atlas.Agent.AtlasAgent.Start(Host.Services, "MyApp");
#endif
```

Requires Uno.Extensions Navigation (`IRouteNotifier`). See
`samples/RoundsApp` for a complete reference integration.

### Run only your app (auto-launch the viewer)

So you don't launch the viewer by hand each time, point the agent at the Atlas
executable with two environment variables. When it starts and no viewer is
reachable, the agent launches one once:

```powershell
$env:ATLAS_VIEWER      = "C:\path\to\Atlas.App.exe"            # the viewer to launch
$env:ATLAS_VIEWER_ARGS = "C:\path\to\MyApp\App.xaml.cs"        # boot it into your app's extracted map
```

Now run **only your app**: the viewer appears, extracts your app's structure from
that `App.xaml.cs` on boot, and lights up edges as you navigate. The viewer also
accepts an `App.xaml.cs` (or a model `.json`) directly as a launch argument.
`ATLAS_VIEWER_ARGS` is optional — without it the viewer opens the bundled sample
and still fills in observed routes. Extraction-on-boot is a desktop feature.

## 3. Use it

Run the viewer, run your app, click around. The map fills in as you navigate;
declared routes flip to **observed** the first time they fire, and the LIVE
badge follows your current screen. Drag nodes to arrange the map — positions
persist per app under `%LocalAppData%\Atlas\layouts`.
