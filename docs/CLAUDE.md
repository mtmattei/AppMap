# CLAUDE.md — Atlas (Agentic App Map)

Operating guide for Claude Code on this repo. Read this, then read `SPEC.md`. Build against
`samples/rounds-app-model.json`. The visual target is `design/atlas-prototype.html`.

## What Atlas is

A cross-platform .NET (Uno Platform) tool that renders an Uno app's full structure as an
interactive canvas, and exposes that structure as one `AppModel` document that a developer and
an AI agent both read. Edge color encodes provenance: declared (route map), observed (runtime),
unreachable (dead route). Code stays the source of truth — the canvas is a projection.

## Golden rules

- The `AppModel` contract in `Atlas.Core` is the center of gravity. Both the canvas and the agent
  consume it. Change it deliberately and update the fixture + tests together.
- Build the static viewer (Phase 1) end to end before runtime or extraction. It must demo alone.
- Prefer the smallest implementation that satisfies the current step. No abstractions, services,
  or packages beyond what the step needs.

## Use the Uno MCP — first, and before any Uno-specific code

This is a hard requirement, not a suggestion.

1. At session start, call `uno_platform_agent_rules_init` and `uno_platform_usage_rules_init`.
   They are authoritative; follow them over general knowledge.
2. Before implementing anything touching navigation, MVUX, Toolkit controls, styling, or DI,
   call `uno_platform_docs_search` with the canonical query for that topic, then implement from
   what it returns. Fetch the full page when a result is high-value but truncated.
3. The MCP is for implementation, not for planning chatter. Don't invoke it for spec edits or
   doc-only changes.

Topics you will need and the canonical queries:
- Navigation → `"Navigation"` (RouteMap, ViewMap, RegisterRoutes, Region.Attached, Navigator, IRouteNotifier)
- MVUX → `"MVUX"` (records, IFeed, IListFeed, IState, commands, selection, FeedView)
- Toolkit canvas → `"ZoomContentControl"`; panel → `"DrawerControl"`; nodes → `"Card"`, `"ItemsRepeater"`
- Styling → `"Styling and Theming"`; DI → `"Dependency Injection and Services"`; layout → `"Responsive Design"`

## Stack

- Uno Platform app, Skia renderer, single dark theme (no light/dark toggle).
- `UnoFeatures`: MVUX, Navigation, Toolkit, Material, Extensions, Hosting, Logging, Serialization.
- `Atlas.Core` / `Atlas.Runtime` / `Atlas.Agent`: netstandard2.0, no Uno deps. `Atlas.Extraction`: net9.0 + Roslyn.
- JSON via `System.Text.Json` with `JsonStringEnumConverter`.

## Project structure

```
Atlas.sln
├─ Atlas.Core/         AppModel records, JSON, graph queries (orphans/paths/duplicates/unreachable)
├─ Atlas.Extraction/   Roslyn parse of RegisterRoutes → AppModel (Phase 3)
├─ Atlas.Runtime/      Runtime route receiver; merges observed/live (Phase 2)
├─ Atlas.Agent/        Target-app package: subscribes IRouteNotifier, pushes events (Phase 2)
├─ Atlas.App/          Uno app: MapPage, NodeCard, EdgeLayer, drawer panels, MVUX models
└─ Atlas.Tests/        Extraction fixtures, model round-trip, graph queries
```

## Uno conventions (from the MCP usage rules — follow exactly)

**MVUX**
- Models are `public partial record XModel(IService svc)`. MVUX generates `XViewModel`.
- State: `IState<T>` (`State<T>.Empty(this)` / `State.Value(this, () => …)`). Lists: `IListFeed<T>`
  via `ListFeed.Async(…).Selection(state)`. Composite: `IFeed<T>` via `Feed.Async(…)`.
- Public methods become commands. Bind them **by name** in XAML to the implicit `IAsyncCommand`.
- Loading/error/empty: use `FeedView`; it passes the value to the template as `Data`.
- NEVER invoke commands or navigation from code-behind. NEVER use `{Binding StringFormat=…}`.

**Navigation**
- Atlas dogfoods Uno.Extensions Navigation: `RegisterRoutes(IViewRegistry, IRouteRegistry)`,
  `ViewMap<TView,TViewModel>()`, `RouteMap("path", View:, Nested:, DependsOn:, IsDefault:)`.
- Navigate with XAML attached properties only: `Region.Attached`, `Region.Name`, `Navigation.Request`.
  No navigation in `.xaml.cs`.

**Layout & styling**
- `AutoLayout` for flows; `Spacing`/`Padding` on the container, never margins on children.
- Spacing scale 4/8/12/16/24/32. Star sizing and alignment; no percentage widths/heights.
- `Responsive` markup extension for adaptive layout. Touch targets ≥44px.
- NEVER hardcode `#AARRGGBB`. Define colors in `ColorPaletteOverride.xaml`, reference brushes.
- Define new styles/templates in `App.xaml` or `Themes/*.xaml` dictionaries. Use existing TextBlock
  styles; don't set inline `FontSize`/`FontWeight`.
- `Card` requires an explicit style. Elevation via `ThemeShadow` + `Translation` Z (8–32) on focal
  elements, not outer containers.
- `ItemsRepeater` for the node layer; never a Button as an item-template root.
- `x:Uid` on visible/interactive elements; `AutomationProperties.*` on inputs/buttons/list items.

## Commands (Windows / PowerShell)

PowerShell syntax, `;` to chain, Windows paths. Run a csproj from its own folder.

```powershell
# Build before running. Stop any running instance first to avoid file locks.
dotnet build

# Run the desktop head with Hot Reload (replace TFM):
$env:DOTNET_MODIFIABLE_ASSEMBLIES = "debug"; Start-Job { dotnet run -f net9.0-desktop --project .\Atlas.App\Atlas.App.csproj }

# Read job output to confirm a clean start; check console logs before changing code.
# Stop via runtime info → terminate by PID. Never taskkill all dotnet. Never Start-Sleep.

dotnet test
```

In Visual Studio: run/stop via the built-in tools; Hot Reload is automatic — don't set
`DOTNET_MODIFIABLE_ASSEMBLIES` or `dotnet run` manually there.

## Workflow

- Small steps. After each meaningful change: build/test, then commit. Conventional messages
  (`feat:`, `fix:`, `refactor:`, `docs:`, `test:`).
- Follow the phased Implementation Plan in `SPEC.md`. Phase 1 ships on its own.
- End a session with a Session Summary: Changed / Build-Test Status / Decisions / Unresolved Questions.

## Evidence hierarchy (when sources conflict)

Existing project code → loaded Uno rules/skills → official Uno docs (via MCP) → Microsoft/WinUI docs
→ package source → build/runtime output → general knowledge. Existing code wins for local
conventions; official docs win for framework behavior; build/runtime output wins for actual behavior.

## Stop conditions — pause and present a Stop Check before:

Changing the `AppModel` contract or its serialization · adding a dependency or `UnoFeature` ·
changing navigation structure or service lifetimes · introducing a custom framework abstraction ·
deleting files or moving many · changing build configuration. Stop Check = Reason / Risk / Options / Recommendation.

## Debugging

After two failed fix cycles, stop and write a Debug Checkpoint (Tried / Current Evidence / Likely
Cause / Options / Recommendation). For renderer, interop, generated-code, or preview-SDK issues:
verify versions early and check docs/source before more edits. No blind patch loops.

## Mentor mode

Explain meaningful decisions (architecture, state, navigation, performance) in 2–4 sentences, code
before prose. Label guidance: *Framework best practice* / *Project convention* / *Common convention*
/ *Opinion*. Skip narration for trivial edits. The goal: every important choice is defensible without
"Claude picked it."
