# Atlas — Fluid App Connection Spec

**Status:** Draft, pre-implementation. Build in a fresh session reading this cold.
**Targets:** `Atlas.App` (`MapModel`, `MapPage`, new services, `App.xaml.cs`, Desktop `Program.cs`), `Atlas.Runtime` (`ModelMerger`, `RuntimeBridge`), `Atlas.Tests`.
**Stack pin:** Uno.Sdk 6.5.36, net10.0, MVUX, Uno.Extensions.Navigation.WinUI 7.1.4, System.Text.Json 10.0.9. See `global.json` / `Directory.Packages.props`.

## Problem

Connecting an app to Atlas has two friction points:

1. **Static load** (`MapModel.OpenModel` → `ModelFilePicker`, native `FileOpenPicker`): every load is browse-from-scratch, and the MCP can't drive the native dialog — so even automated testing needs a human click.
2. **Live connect:** `TOOLING.md` requires a Debug-only NuGet ref **plus** an `AtlasAgent.Start(...)` line in the target app, and `RuntimeBridge.OnMessage` (`RuntimeBridge.cs:157`) **drops any runtime route that doesn't already resolve to a node in the loaded model** (`ResolveNode` → null → `return`). So live connect only works when a matching static model is already loaded. You cannot "just run the app and watch it draw."

Goal: connection feels like the prototype's promise — *point once, or run the app, and Atlas draws it.* Delivered in three phases, cheap-first.

---

## Architecture Brief

**Architecture choice: MVUX**, unchanged. New work is services behind DI + pure additions to `Atlas.Runtime`. The `AppModel`-is-the-single-contract invariant holds: every new path produces or mutates an `AppModel` and pushes it through the existing `RuntimeBridge` model stream.

### Phase A — Static-load fluidity (cheap, additive)

- **Recent models store.** New `IRecentModels` service + `JsonRecentModels` impl, mirroring `JsonLayoutStore` (same `%LocalAppData%\Atlas\` root, `recent.json`, last N=8 absolute paths, MRU order). Registered in DI beside `ILayoutStore`.
- **Every load path records into it:** picker (`OpenModel`), drag-drop, launch-arg, recent-pick all funnel through one `MapModel.LoadModelFromPath(string path)` that reads text, deserializes, calls `Bridge.OpenModel`, and `IRecentModels.Add(path)`.
- **Drag-drop:** `CanvasHost` gets `AllowDrop=True` + `DragOver`/`Drop` handlers in `MapPage.xaml.cs`. Drop reads the first `.json` `StorageFile`, forwards to the VM. (MVUX note: invoking a model command from code-behind — resolve the bindable proxy command the same way existing code-behind reaches the VM; flagged as a risk below.)
- **Launch arg / file association:** Desktop `Platforms/Desktop/Program.cs` already owns `args`; thread a startup path option into `App.OnLaunched` → into `IAppModelSource`. Add `StartupModelPath` (nullable) consulted in `RuntimeBridge.EnsureStartedAsync` before falling back to the embedded `JsonAppModelSource`. Enables `Atlas.App.exe path\to\model.json` and Windows "Open with".

### Phase B — Live connect without a pre-loaded model (structural)

- **Synthesize-on-observe.** New `ModelMerger.SynthesizeNode(AppModel, routePath)` → `AppNode` when `ResolveNode` returns null:
  - `Id` = slug of last route segment (dedup against existing ids).
  - `Name` = last segment titled; `View`/`ViewModel` = `""` (unknown until extraction); `Route` = the observed path.
  - `Kind` = `Page`; `Status` = `Live`; `Files`/`Elements`/`Tokens` = empty; `Position` = `null`.
  - Provenance: the node and its inbound edge are `Observed` — structurally true, source-thin.
- **`RuntimeBridge.OnMessage`** (`:157`): on `ResolveNode == null`, synthesize, append to `model.Nodes`, then `ApplyRoute` as today. Result: connect a live app with the **embedded/empty model** and the graph builds itself screen-by-screen.
- **Null-position auto-placement.** Synthesized nodes have no `Position`. `CanvasLayout` must place position-less nodes deterministically — anchor near the `from` node with a fan offset (right + stagger), so the live map grows readably. This is the one non-trivial layout change.

### Phase C — Lower the live-connect bar (optional, bridges to Phase 3)

- **One-line attach.** Ship a `UseAtlas()` `IHostBuilder` extension in `Atlas.Agent` so target apps write one discoverable line instead of a manual `AtlasAgent.Start` after host build. (Does not remove the NuGet ref.)
- **Watch-a-folder reload.** When Phase 3 extraction writes `obj/atlas/app-model.json`, add an optional `FileSystemWatcher` (desktop only) that re-`OpenModel`s on change — "point once, redraws on rebuild." Out of scope until extraction lands; noted so Phase B's synthesized nodes and Phase 3's real nodes are designed to coexist (a later extracted model replaces synthesized stand-ins by id/route).

**Data flow (unchanged spine):** all phases end at `RuntimeBridge` pushing an `AppModel` to the `Graph` feed → canvas + suggestions + inspector re-render. No new rendering path.

**Platform constraints:**
- Drag-drop, launch-arg, recent store, FileSystemWatcher = **desktop** (the viewer's primary target). On WASM these no-op gracefully (no sockets already disables live connect per `RuntimeBridge.cs:103`).
- `StorageFile`/drag-drop must marshal to the UI thread; MVUX commands may run off it (same dispatcher bridge `ModelFilePicker` already uses).

**Testing/validation**
- `Atlas.Tests/ModelMergerTests.cs`: `SynthesizeNode` produces an Observed/Live node with empty source fields; `OnMessage`-equivalent path: feeding a route absent from a model grows the node + observed edge; a later route matching a real node does **not** duplicate.
- `JsonRecentModels` round-trip (add/dedupe/cap at 8, MRU order).
- Runtime: (1) drag `nursing-app-model.json` onto canvas → loads + appears in Recent. (2) Launch `Atlas.App.exe <path>` → opens that model. (3) With synthesize-on-observe, connect a live app against the embedded model → previously-unknown screens appear as Observed/Live.

---

## Design Brief

**Visual direction:** unchanged Atlas top-bar grammar. Two small additions:

- **Recent-models control** next to the app-name pill (`MapPage.xaml:94-102`): a caret `Button` (TextButtonStyle) opening a `MenuFlyout` of recent file names (full path as tooltip). Empty → disabled. Selecting one = `OpenRecent(path)`.
- **Drop affordance:** on `DragOver`, overlay a dashed `AtlasDeclaredBrush` border + centered `AtlasMonoBody` *"Drop an app-model .json"* on the canvas; remove on `Drop`/`DragLeave`. Reuse existing brushes/styles; no new tokens or hex.

**Synthesized node styling:** synthesized nodes render with the existing `NodeCard` but read visually as source-thin — empty inspector sections already collapse (`InspectorSection Count=0`), and the `MODEL · AS THE AGENT SEES IT` JSON shows blank `view`/`files`, which is honest. No new card variant; optionally a subtle "discovered" hairline if it reads ambiguously (defer).

**Spacing/typography:** flyout and overlay use existing `Atlas*` styles. Top-bar spacing already `Spacing=8`; the caret button slots in without reflow.

**Responsive:** top-bar is horizontal `AutoLayout` with `Justify=SpaceBetween`; the recent caret stays grouped with the pill cluster. Narrow-width spec inherits unchanged.

---

## Interaction Brief

**User flows**
- *Drag-drop:* drag `.json` over canvas → overlay appears → drop → model loads, notice toast *"Model loaded → {App}"* (existing `ShowNotice`), entry added to Recent.
- *Recent:* click caret → pick → loads, toast.
- *Launch arg:* open file with Atlas / pass path on CLI → viewer boots straight into that model, no picker.
- *Live build-up (Phase B):* run the instrumented app with only the embedded model loaded → each navigation adds an Observed/Live node; back-nav moves the live node without inventing forward edges (existing `ApplyRoute` rule at `ModelMerger.cs:84`).

**Input behavior:** drag-drop accepts a single `.json`; multiple → take first, toast the count. Non-JSON / malformed → reuse the existing *"Not a valid app model file"* path (`MapModel.cs:101`).

**Empty states:** Recent flyout empty → disabled caret. No model + no arg → embedded sample (today's behavior).

**Loading state:** local JSON load is sub-second; the drop overlay doubles as the transient affordance. Synthesized-node growth is incremental and live — no spinner.

**Error states:** bad drop file → toast, model unchanged. Missing launch-arg path → log warning, fall back to embedded. Synthesize never throws on a valid `AgentMessage`.

**Feedback/animation:** reuse `Reveal`/`ShowNotice` (2.5s toast). Drop overlay fades via the same `Reveal` helper on a bound `IsDragOver` state.

**Accessibility:** caret button `AutomationProperties.Name="Open a recent model"`; each flyout item names the app/file. Drop overlay text is real text (screen-reader visible). Drag-drop is mouse-centric → keep the picker + recent as keyboard-reachable equivalents.

**Runtime verification steps**
1. Drag `samples/nursing-app-model.json` onto the canvas → loads, pill reads NURSINGSCHEDULEAPP, appears in Recent.
2. Close, relaunch `Atlas.App.exe "<path>\nursing-app-model.json"` → boots into the nursing model.
3. Open Recent caret → reopen Rounds.
4. (Phase B) With only the embedded model, wire a live app and navigate to a screen absent from the model → confirm a new Observed/Live node appears and is positioned readably near its origin.

---

## Implementation Plan

**Phase A (afternoon):**
1. `IRecentModels` + `JsonRecentModels` (clone `JsonLayoutStore` shape), DI registration.
2. `MapModel.LoadModelFromPath` funnel; route `OpenModel`/recent through it.
3. `MapPage.xaml` recent caret + `MenuFlyout`; `MapModel.OpenRecent`.
4. `MapPage.xaml.cs` drag-drop handlers + `IsDragOver` state + overlay.
5. Launch-arg: `Program.cs` → `App.OnLaunched` → `StartupModelPath` consulted in `RuntimeBridge.EnsureStartedAsync`.
6. `JsonRecentModels` tests. Commit `feat: drag-drop, recent models, launch-arg open`.

**Phase B (structural):**
7. `ModelMerger.SynthesizeNode` + tests.
8. `RuntimeBridge.OnMessage` synthesize branch.
9. `CanvasLayout` null-position auto-placement (anchor-near-from).
10. Runtime walk of live build-up. Commit `feat: synthesize observed nodes on unknown routes`.

**Phase C (deferred, with/after Phase 3 extraction):**
11. `Atlas.Agent` `UseAtlas()` host extension.
12. Optional `FileSystemWatcher` reload of extraction output; reconcile synthesized vs extracted by id/route.

## Unresolved Questions

- **Synthesized-node identity vs extraction:** when a later real model (extracted, Phase 3) arrives, do synthesized nodes get replaced silently by route match, or merged (keep runtime position, adopt real source)? (Default: merge by route, keep position — but confirm when Phase 3 is specced.)
- **CanvasLayout placement:** anchor-near-from with fan offset vs a simple incremental grid for position-less nodes — which reads better as a live map grows? (Risk, 10-min spike against a scripted route sequence.)
- **Drag-drop from code-behind to MVUX command:** verify the cleanest way to invoke `LoadModelFromPath` from `MapPage.xaml.cs` — direct bindable-proxy command execute vs a bound `IState<string>` the VM observes. (Risk, 10-min spike; `ModelFilePicker`'s dispatcher bridge is the reference.)
- **Phase C scope:** is `UseAtlas()` worth shipping before Phase 3, given it still needs the NuGet ref? (Default: defer — pairs naturally with extraction.)
- **Security/trust:** launch-arg and drag-drop open arbitrary JSON from disk; deserialization is already guarded by `AppModelJson.Deserialize` (throws on non-AppModel). No code execution risk; accept as-is.
