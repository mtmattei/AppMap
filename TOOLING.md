# Running Atlas as a tool

Atlas has two halves: the **viewer** (the desktop app you look at) and the **agent**
(a tiny package your app references so the viewer can see it navigate).

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

## 3. Use it

Run the viewer, run your app, click around. The map fills in as you navigate;
declared routes flip to **observed** the first time they fire, and the LIVE
badge follows your current screen. Drag nodes to arrange the map — positions
persist per app under `%LocalAppData%\Atlas\layouts`.
