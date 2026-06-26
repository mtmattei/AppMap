# Atlas — Agentic App Map

Atlas renders an Uno Platform app's full navigation structure as an interactive canvas, and exposes
that same structure as **one document** (`AppModel`) that a developer *and* an AI agent both read.

The thesis: an agent shouldn't have to understand an app only through scattered files. Atlas gives it —
and you — the same view: screens, routes, and how you move between them, with **every edge marked by
where the evidence came from**:

- **Declared** — written in the route map / on a button (static, from the code).
- **Observed** — actually fired while the app ran (runtime).
- **Unreachable** — declared but its guard never fires (a dead door).

Code stays the source of truth. The canvas is a projection of `AppModel`, never the other way around.

> New here? Read this file, then `docs/SPEC.md` (the build spec), then `docs/CLAUDE.md` (conventions).
> The visual target is `design/atlas-prototype.html`.

---

## How it works behind the scenes

Everything is a projection of one immutable record, `AppModel` (in `Atlas.Core/AppModel.cs`):

```csharp
public sealed record AppModel(
    string App, DateTimeOffset GeneratedAt,
    ModelSource Source,                 // Static | Runtime | Merged
    string SchemaVersion,
    IReadOnlyList<AppNode> Nodes,       // a screen: id, name, route, view, view-model, status, position…
    IReadOnlyList<AppEdge> Edges);      // a hop: from, to, Kind (provenance), Trigger, IsDefault, DependsOn

public enum EdgeKind { Declared, Observed, Unreachable }
```

Two independent pipelines fill that document, and the viewer renders the merge:

```
 ┌── STATIC (no app run) ─────────────────────────────────────────────────┐
 │  App.xaml.cs + *.xaml + *.cs                                            │
 │      │  Roslyn                                                          │
 │      ├─ RouteExtractor   → route tree (shell→child, IsDefault, DependsOn)│
 │      ├─ TriggerExtractor → lateral flow edges (Navigation.Request +     │
 │      │                     NavigateRouteAsync/ViewModelAsync), labelled  │
 │      └─ TreeLayout       → deterministic node positions                  │
 │              │                                                           │
 │         atlas CLI ───────────────► AppModel JSON ──────────┐            │
 └────────────────────────────────────────────────────────────┼───────────┘
                                                               ▼
                                                      Atlas.App  (the viewer)
                                                               ▲
 ┌── RUNTIME (app running) ──────────────────────────────────┼───────────┐
 │  Your app + Atlas.Agent ── IRouteNotifier ── NDJSON :9743 ─┘            │
 │      → RuntimeBridge merges observed/live deltas into the model         │
 └────────────────────────────────────────────────────────────────────────┘
```

- **Static extraction** turns source into an `AppModel` without running anything. `RouteExtractor`
  parses `RegisterRoutes`; `TriggerExtractor` recovers the real page→page flow from XAML
  `Navigation.Request` and `Navigate*Async` call sites; `TreeLayout` places the nodes. The `atlas`
  CLI is the host that emits JSON.
- **Runtime feed** is optional. A target app references the tiny `Atlas.Agent` package; it subscribes
  to Uno's `IRouteNotifier` and pushes each navigation over a local NDJSON socket. `Atlas.Runtime`'s
  `ModelMerger` flips declared edges to **observed** as they fire and marks the **live** screen.
- **The viewer** (`Atlas.App`) is an Uno MVUX app. The canvas (`ZoomContentControl` + `EdgeLayer` +
  `NodeCard`) renders the model; the agent panel runs structural queries (`GraphQueries`) and a
  free-text question router (`QuestionInterpreter`) over the *same* document.

---

## Repository layout

| Project | TFM | What it holds |
|---|---|---|
| `Atlas.Core` | `netstandard2.0` | The `AppModel` contract + JSON, and all **pure** logic: `GraphQueries`, `SuggestionEngine`, `QuestionInterpreter`, `TreeLayout`, `EditScope`. No Uno deps. |
| `Atlas.Extraction` | `net10.0` + Roslyn | `RouteExtractor` and `TriggerExtractor` — source → `AppModel`. |
| `Atlas.Runtime` | `netstandard2.0` | Runtime route receiver + `ModelMerger` (observed/live merge). |
| `Atlas.Agent` | `net10.0` (NuGet) | Drop-in package for a target app: subscribes `IRouteNotifier`, pushes events. |
| `Atlas.Cli` | `net10.0` (exe `atlas`) | `atlas extract` — runs extraction + layout, emits JSON. |
| `Atlas.App` | `net10.0-desktop;net10.0-browserwasm` | The Uno viewer: `MapPage`, `NodeCard`, `EdgeLayer`, drawer panels, MVUX models. |
| `Atlas.Tests` | `net10.0` (xunit) | Extraction, model round-trip, graph-query, interpreter coverage. |

---

## Using it

### Prerequisites

- .NET 10 SDK (see `global.json` for the pinned `Uno.Sdk`).
- Windows is the primary dev target; the viewer also runs on macOS/Linux/WASM via the Skia renderer.
- First-time Uno setup: `dotnet tool install -g uno.check && uno-check`.

### Build & test

```powershell
dotnet build Atlas.sln
dotnet test Atlas.Tests/Atlas.Tests.csproj   # 64 tests
```

### Run the viewer

```powershell
dotnet run -f net10.0-desktop --project .\Atlas.App\Atlas.App.csproj
```

It boots into a bundled sample. Use **Open model…** in the top bar (or drag a model `.json` onto the
canvas, or pass a path as a launch arg) to load any exported model. Hand-arranged node positions
persist per app under `%LocalAppData%\Atlas\layouts`.

### Extract a model from source (the `atlas` CLI)

```powershell
dotnet run --project .\Atlas.Cli\Atlas.Cli.csproj -- `
  extract .\samples\RoundsApp\RoundsApp\App.xaml.cs `
  --app "RoundsApp" `
  --source .\samples\RoundsApp\RoundsApp `
  --out rounds.json
```

```
atlas extract <App.xaml.cs> [--app <name>] [--source <dir>] [--out <file>] [--no-layout] [--compact]

  --app <name>    name stamped into the model (default: the source's project folder)
  --source <dir>  also scan this project dir for navigation triggers → lateral flow edges
  --out <file>    write JSON to a file (default: stdout)
  --no-layout     skip the deterministic tree layout (leave positions null)
  --compact       emit single-line JSON instead of indented
```

Without `--source`, you get just the route tree. With it, the lateral flow edges (with trigger
labels like *"Start rounding"*) are layered on top. Open the resulting JSON in the viewer.

### See your own running app on the map

Add the agent to your Uno app in **Debug only** and start it once after the host is built:

```csharp
Host = await builder.NavigateAsync<Shell>();
#if DEBUG
_ = Atlas.Agent.AtlasAgent.Start(Host.Services, "MyApp");
#endif
```

Run the viewer and your app together; declared edges flip to **observed** as you navigate. Requires
Uno.Extensions Navigation (`IRouteNotifier`). Full reference integration: `samples/RoundsApp`.
See `TOOLING.md` for packaging and publishing the viewer as a standalone tool.

---

## Working on it

### Conventions (non-negotiable — see `docs/CLAUDE.md`)

- **`AppModel` is the center of gravity.** The canvas, the agent, and the runtime feed all read it.
  Change the contract deliberately and update the fixture (`samples/rounds-app-model.json`) + tests together.
- **Keep logic pure and in `Atlas.Core`.** `GraphQueries`/`SuggestionEngine`/`QuestionInterpreter`/
  `TreeLayout` are pure functions with xunit coverage. New analysis goes there with a test, not in the viewer.
- **The viewer is MVUX.** Models are `partial record`s exposing `IFeed`/`IState`/`IListFeed`; public
  async methods become commands. Bind with `{Binding}` (the MVUX proxy needs it — `x:Bind` bypasses it).
- **Styling:** no hardcoded hex colors, no inline `FontSize`, no `{Binding StringFormat}`. Use the
  Material brush/typography resources and the `Atlas*` keys in `Themes/`.
- **Uno-specific work:** initialize the Uno MCP rules and consult the matching skill **before**
  writing navigation/MVUX/Toolkit/styling code (per `docs/CLAUDE.md`).

### Dev workflow

Small steps; after each meaningful change run `dotnet build` / `dotnet test`, then commit with a
conventional message (`feat:`, `fix:`, `refactor:`, `perf:`, `docs:`, `test:`). Stop the running
viewer before a rebuild to avoid file locks.

### Where to add things

- **A new extraction shape** (another navigation pattern) → `Atlas.Extraction`, with a synthetic test
  and, where possible, a real-source integration test using a `samples/` fixture.
- **A new structural query / agent answer** → `Atlas.Core/GraphQueries.cs` (pure + tested), then surface
  it via `SuggestionEngine` (a chip) and/or `QuestionInterpreter` (a typed-question route).
- **Canvas / panel UI** → `Atlas.App/Presentation` (MVUX models + XAML; custom controls under `Controls/`).

### Status

Phases 0–4 + tooling are in. **Phase 3 (static extraction) is complete**: route tree + `DependsOn` +
XAML/code trigger inference, hosted by the `atlas` CLI. Known best-effort gaps (tracked in `docs/SPEC.md`):
qualifier+route combos (`-/Home`), region-by-selection nav with no `Navigation.Request`, and
non-Extensions/`Frame` navigation. The agent question box uses a local router today; wiring the Claude
API for true natural language is the open Phase-4 upgrade.

### Map of the docs

| File | Read it for |
|---|---|
| `docs/SPEC.md` | The build spec — architecture, phased plan, open questions. |
| `docs/CLAUDE.md` | Conventions, commands, stop conditions, the Uno-MCP rule. |
| `TOOLING.md` | Running/publishing the viewer and wiring the agent into a target app. |
| `design/atlas-prototype.html` | The interactive visual reference. |
| `samples/` | The canonical `AppModel` fixtures and the `RoundsApp` reference app. |
