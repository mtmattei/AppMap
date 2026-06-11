# Atlas — Agentic App Map · Build Spec

> A cross-platform .NET (Uno Platform) tool that renders the full structure of an Uno
> application as an interactive canvas, and exposes that structure as a single model that
> both a developer and an AI agent can read, navigate, and act on.

**Session:** Atlas — Agentic App Map (build spec)

**Assumptions**

- Target apps use **Uno.Extensions Navigation** (`RegisterRoutes` with `ViewMap`/`RouteMap`). Frame-only and custom navigation are out of scope for v1 and handled as best-effort later.
- Target apps use **MVUX** (`*Model` records → generated `*ViewModel`). Atlas itself is built the same way (dogfood).
- Atlas viewer runs on the **Skia renderer** across Windows/macOS/Linux/WASM. Roslyn extraction is a desktop/CLI concern, not a viewer concern.
- Dev environment is **Windows 11 / PowerShell / Visual Studio** with Hot Reload, per the Uno agent rules.
- The canonical model contract is `samples/rounds-app-model.json`. Build the viewer against it first; wire extraction and runtime in later.

---

## The core artifact: the App Model

Everything in Atlas is a projection of one document. The canvas renders it; the agent reads it; the runtime feed mutates it. It is never authored on the canvas — code stays the source of truth.

**C# contract (lives in `Atlas.Core`, `System.Text.Json`, `JsonStringEnumConverter`):**

```csharp
public sealed record AppModel(
    string App,
    DateTimeOffset GeneratedAt,
    ModelSource Source,            // Static | Runtime | Merged
    string SchemaVersion,
    IReadOnlyList<AppNode> Nodes,
    IReadOnlyList<AppEdge> Edges);

public sealed record AppNode(
    string Id,
    string Name,
    NodeKind Kind,                 // Shell | Page | Dialog
    string Route,                  // "patients/{id}"  ('' for shell)
    string View,                   // "PatientDetailPage"
    string ViewModel,              // "PatientDetailModel"
    NodeStatus Status,             // Normal | Live | Orphan
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Elements,
    IReadOnlyList<string> Tokens,
    Point? Position);              // optional persisted layout

public sealed record AppEdge(
    string From,
    string To,
    EdgeKind Kind,                 // Declared | Observed | Unreachable
    string Trigger,                // "RecordVitals"
    bool IsDefault = false,        // RouteMap default region
    bool DependsOn = false);       // RouteMap DependsOn relationship
```

`EdgeKind` is the thesis of the whole tool: **Declared** comes from the route map, **Observed** comes from the runtime feed, **Unreachable** is a declared route whose guard never fires. The two sources merge into one model; provenance is preserved, never flattened.

---

## Architecture Brief

Define:

**App/module structure** — one solution, five projects:

| Project | Type | Responsibility |
|---|---|---|
| `Atlas.Core` | netstandard2.0 lib | `AppModel` records, JSON (de)serialization, graph queries (orphans, paths, duplicates). No UI, no Uno deps. |
| `Atlas.Extraction` | net9.0 lib (Roslyn) | Parse a target app's `RegisterRoutes` → `AppModel` (declared nodes + edges). Desktop only. |
| `Atlas.Runtime` | netstandard2.0 lib | Receiver for runtime route events; merges Observed/Live into the model. |
| `Atlas.Agent` | netstandard2.0 lib | Tiny package the *target* app references; subscribes to `IRouteNotifier`, pushes events to Atlas over a local channel. |
| `Atlas.App` | Uno app (MVUX + Navigation + Toolkit) | The canvas, inspector, and agent panel. |
| `Atlas.Tests` | test lib | Extraction fixtures, model round-trip, graph-query assertions. |

**State model** — MVUX. The map page is driven by one model record:

```csharp
public partial record AppMapModel(IAppModelSource ModelSource, IAgentService Agent)
{
    public IFeed<AppModel> Model => Feed.Async(ModelSource.LoadAsync);
    public IState<AppNode?> Selected => State<AppNode?>.Empty(this);
    public IState<QueryResult?> AgentResult => State<QueryResult?>.Empty(this);

    public IState<bool> ShowDeclared => State.Value(this, () => true);
    public IState<bool> ShowObserved => State.Value(this, () => true);
    public IState<bool> ShowUnreachable => State.Value(this, () => true);

    public async ValueTask SelectNode(AppNode node) => await Selected.SetAsync(node);
    public async ValueTask RunQuery(AgentQuery query) { /* Agent.RunAsync → AgentResult */ }
    public async ValueTask ClearHighlights() => await AgentResult.SetAsync(null);
    public async ValueTask JumpTo(AppNode node) { /* Runtime.NavigateAsync(node.Route) */ }
    public async ValueTask ScopeEdit(AppNode node) { /* Agent.ScopeAsync(node) */ }
}
```

Project convention (from Uno usage rules): public methods on the model surface as `IAsyncCommand`s bound **by name** in XAML. No navigation or command invocation in code-behind.

**Navigation model** — Atlas dogfoods Uno.Extensions Navigation. `RegisterRoutes` registers `ShellModel` → `MapModel` (default) and `SettingsModel`. XAML attached properties (`Region.Attached`, `Region.Name`, `Navigation.Request`) only — never code-behind navigation.

**Services / dependencies** — registered via `IHostBuilder` DI:
- `IAppModelSource` — loads the model (JSON file in v1; `Atlas.Extraction` in v2).
- `IRuntimeBridge` — connects to a running target app via `IRouteNotifier` events; raises observed/live updates.
- `IAgentService` — runs graph queries and edit-scoping. Backed by `Atlas.Core` graph functions for structural queries; Claude API for natural-language reasoning.

**Data flow**
1. `IAppModelSource` yields an `AppModel` (static JSON or Roslyn extraction).
2. `Atlas.Core` deserializes; the map `IFeed<AppModel>` publishes it; the canvas renders.
3. `IRuntimeBridge` merges `Observed`/`Live` deltas into the feed as route events arrive.
4. `IAgentService` reads the same `AppModel`; queries return a `QueryResult` (highlighted node/edge ids + prose) that drives canvas highlight state.

**Platform constraints**
- Roslyn extraction needs the full .NET runtime; it runs on desktop or as a CLI, and emits JSON. The viewer consumes JSON and stays fully cross-platform.
- `IRouteNotifier` is in-process to the *target* app, so the runtime feed requires the target to reference `Atlas.Agent`. Exact `IRouteNotifier` surface to be confirmed via `uno_platform_docs_search "IRouteNotifier"` at Phase 2 start.
- Static extraction yields no `Trigger` text (triggers live in XAML `Navigation.Request` and code nav calls). v1 leaves observed triggers to the runtime feed; declared edges carry the route path as the label.

**Testing / validation approach**
- `Atlas.Core`: model JSON round-trips; graph queries (orphans, paths, duplicates, unreachable) assert against the `RoundsApp` fixture.
- `Atlas.Extraction`: parse small `RegisterRoutes` fixtures, assert node/edge output.
- `Atlas.App`: build, then run via Hot Reload (PowerShell `Start-Job`), read job logs for errors, exercise each interaction (see Interaction Brief verification steps).

---

## Design Brief

Define:

**Visual direction** — a developer instrument in the Super Normal register: restraint, function first, syntax-derived color doing semantic work. Dark, single theme (no light/dark toggle, per Uno usage rules). The signature element is the canvas itself, where **edge color encodes provenance** (observed / declared / unreachable) — the one thing a static nav editor cannot show.

**Theme encoding (the important translation)** — the prototype uses raw hex and two brand fonts. In Uno that becomes named resources, defined once, never hardcoded:
- Define an Atlas palette in `ColorPaletteOverride.xaml` (Material): surface, accent (amber), and three semantic provenance brushes — `AtlasObservedBrush` (sage), `AtlasDeclaredBrush` (blue), `AtlasUnreachableBrush` (coral) — plus `AtlasShellBrush` (purple) and `AtlasOrphanBrush` (coral).
- Define Atlas text styles in `App.xaml` / `Themes/TextBlock.xaml`: a mono chrome ramp (Martian Mono — labels, routes, code) and a serif prose style (Newsreader — agent reasoning). Map to the Material type roles; do not set inline `FontSize`/`FontWeight` on elements.
- Rule (from usage rules): no `#AARRGGBB` literals anywhere; reference brushes only.

**Layout structure** — `MapPage`: a `Grid` with a top bar row and a content row. Content is the `ZoomContentControl` canvas filling the page, with a right-edge `DrawerControl` panel (Agent / Inspector). Legend and zoom controls are canvas overlays.

**Typography** — Atlas mono styles for all chrome (node titles, routes, view-model names, buttons, JSON peek); Atlas serif style for agent prose. Body ≥16px effective, line-height on the 4px grid.

**Spacing** — 4/8 scale (4, 8, 12, 16, 24, 32). `AutoLayout` with `Spacing`/`Padding` on the container; never margins on children.

**Component hierarchy**
```
MapPage
└─ Grid (rows: TopBar, Content)
   ├─ TopBar (AutoLayout: brand mark, app name, Runtime-connected pill)
   └─ Grid (Content)
      ├─ ZoomContentControl
      │  └─ Canvas
      │     ├─ EdgeLayer (path geometry, brush by EdgeKind)
      │     └─ ItemsRepeater (nodes → NodeCard, explicit Card style)
      ├─ Legend overlay (layer toggles)
      ├─ ViewControls overlay (zoom in/out/reset)
      └─ DrawerControl (right)
         └─ FeedView (Model)
            ├─ AgentPanel (query chips, response, scoped-context block)
            └─ InspectorPanel (node fields, actions, JSON peek)
```

**Theme usage** — Material resources preferred; `ColorPaletteOverride.xaml` for the Atlas palette; specialized dictionaries in `Themes/` for `NodeCard` and overlay styles. Node accent is a left border brush keyed to `NodeKind`. Elevation via `ThemeShadow` + `Translation` Z (8–32) on node cards and the drawer, not outer container shadows.

**Responsive / adaptive** — `Responsive` markup extension. Drawer is docked on wide form factors and overlays on narrow (<905px). Canvas always fills remaining space. Touch targets ≥44px.

---

## Interaction Brief

Define:

**User flows**
1. *Map a project:* open Atlas → load model (JSON in v1) → canvas lays out nodes and edges.
2. *Understand structure:* toggle provenance layers, pan/zoom, read the live node.
3. *Inspect:* tap a node → drawer shows route, view-model, files, tokens, and the JSON the agent receives.
4. *Reason:* run an agent query → canvas highlights the answer (orphans, paths, duplicates, unreachable).
5. *Act:* "Jump to (live)" navigates the running target; "Ask agent to modify" hands the agent a scoped context.

**Input behavior** — `ZoomContentControl` handles pan (drag) and zoom (Ctrl+wheel / pinch). Tap selects a node. Keyboard mirrors Hot Design's canvas: Ctrl+0 reset, Ctrl+9 auto-fit, Ctrl+ +/- zoom. Layer toggles are tap targets in the legend.

**Empty states** — no model loaded: "Point Atlas at a project to map it." No runtime connection: "Static view. Start the app to see live routes." (Interface voice, actionable.)

**Loading states** — `FeedView` shows a loading template while extraction or an agent query runs. The canvas stays interactive with the last model.

**Error states** — extraction can't find `RegisterRoutes`: name the file it looked in and what to check. Agent failure: state what failed and the retry path. Errors do not apologize and are never vague.

**Animations / transitions** — edge-highlight opacity transition on query; live-node pulse; drawer slide. Respect reduced-motion (disable pulse and transitions).

**Feedback states** — selection ring on the active node; toast on Jump and Scope edit, using the same verb the button used ("Navigated → PatientDetail", "Scoped edit context ready").

**Accessibility** — `AutomationProperties.Name` on nodes, toggles, and actions; `x:Uid` on visible/interactive elements (`MapPage.Label.*`); focus order across toolbar → canvas → drawer; provenance also encoded by line style (solid/dashed), not color alone.

**Runtime verification steps**
1. `dotnet build` the `Atlas.App` head, then start with Hot Reload via `Start-Job`; read job output for a clean start.
2. Load `samples/rounds-app-model.json`; confirm 12 nodes / 11 edges render with correct kinds and the live node on `PatientDetail`.
3. Click each node; confirm the drawer fields and JSON peek match the fixture.
4. Run all four structural queries; confirm highlights match the expected node/edge sets.
5. Toggle each provenance layer; confirm edges show/hide.
6. (Phase 2) Attach a sample target referencing `Atlas.Agent`; navigate it; confirm `IRouteNotifier` events flip declared edges to observed and move the live node.

---

## Implementation Plan

Small, verifiable steps. Build/test after each; commit with conventional messages. Phase 1 is a complete, demoable product on its own.

**Phase 0 — Scaffold**
- `dotnet new unoapp` (MVUX + Navigation + Toolkit + Material features); add `Atlas.Core`, `Atlas.Extraction`, `Atlas.Runtime`, `Atlas.Tests` to the solution. → `feat: scaffold solution`
- Add `AppModel` records + JSON serialization to `Atlas.Core`; round-trip the fixture in a test. → `feat: add app-model contract` / `test: app-model round-trips fixture`

**Phase 1 — Static viewer (MVP)**
- `IAppModelSource` loading the JSON fixture; register in DI. → `feat: load app-model from json`
- Atlas theme: `ColorPaletteOverride.xaml` palette + Atlas text styles. → `feat: add atlas theme resources`
- `MapPage` shell: top bar + `ZoomContentControl` + `DrawerControl`. → `feat: add map page shell`
- `NodeCard` style + `ItemsRepeater` binding to nodes; position from model. → `feat: render node cards`
- `EdgeLayer` geometry with brush/dash by `EdgeKind`; arrowheads. → `feat: render edges by provenance`
- Legend layer toggles bound to `ShowDeclared/Observed/Unreachable`. → `feat: add provenance layer toggles`
- Inspector panel via `FeedView`; selection wiring; JSON peek. → `feat: add node inspector`
- `Atlas.Core` graph queries (orphans, paths, duplicates, unreachable) + tests. → `feat: add graph queries` / `test: graph query coverage`
- Agent panel running structural queries through `Atlas.Core`; highlight state. → `feat: add agent panel structural queries`

**Phase 2 — Runtime feed**
- Confirm `IRouteNotifier` surface via docs; `Atlas.Agent` subscriber + local channel. → `feat: add runtime route notifier bridge`
- `IRuntimeBridge` receiver merges observed/live deltas into the feed. → `feat: merge runtime routes into model`

**Phase 3 — Static extraction**
- `Atlas.Extraction` Roslyn walk of `RegisterRoutes` (`ViewMap`/`RouteMap`/`DependsOn`/nesting) → `AppModel`. → `feat: extract app-model from registerroutes`
- Best-effort trigger labels from XAML `Navigation.Request` + code nav calls. → `feat: infer navigation triggers`

**Phase 4 — Act from canvas**
- `JumpTo` deep-links the running target via the bridge. → `feat: jump to route from canvas`
- `ScopeEdit` assembles bounded context (node files + immediate flow + tokens) for the agent; Claude API natural-language queries. → `feat: scope agent edits from canvas`

## Unresolved Questions

- **`IRouteNotifier` reach.** Does it surface enough to reconstruct observed edges *and* their triggers, or only the current route? This decides how much the runtime feed can fill that static extraction leaves blank.
- **Local channel for `Atlas.Agent`.** WebSocket, named pipe, or reuse of the Uno dev-server transport? Reusing the dev server is less code but couples us to its protocol.
- **Extraction host.** Roslyn library invoked by the viewer (desktop only) vs. a separate `atlas extract` CLI that emits JSON consumed everywhere. CLI keeps the viewer platform-clean.
- **Agent edit commit path.** Does `ScopeEdit` open a PR, stage a diff for review, or just prepare context for Claude Code in the repo? This is the governance seam for the enterprise pitch.
- **Layout source of truth.** Persist hand-arranged `Position` in the model, or auto-layout (layered graph) on every load? Auto-layout scales past ~12 nodes; persisted positions respect intent.
- **Frame / custom navigation.** v1 assumes Extensions Navigation. When do mixed-navigation apps get best-effort call-site analysis, and how do we show the lower confidence on those edges?
