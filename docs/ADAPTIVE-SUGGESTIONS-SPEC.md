# Atlas — Adaptive Agent Suggestions Spec

**Status:** Draft, pre-implementation. Build in a fresh session reading this cold.
**Targets:** `Atlas.Core` (new pure engine), `Atlas.App` (`MapModel` + `MapPage.xaml`), `Atlas.Tests`.
**Stack pin:** Uno.Sdk 6.5.36, net10.0, MVUX (`Uno.Extensions.Reactive`), Uno.Extensions.Navigation.WinUI 7.1.4, System.Text.Json 10.0.9. See `global.json` / `Directory.Packages.props` — do not rediscover versions.

## Problem

The agent panel's four chips are hardcoded in `MapPage.xaml:334-365`. Three call app-agnostic queries; two are nailed to RoundsApp:
- `MapModel.cs:112` — `FindPaths` calls `GraphQueries.FindPathsTo(m, "meds")`. NursingScheduleApp has no `meds` node, so the chip returns *"No node with id 'meds' in the model."*
- `MapPage.xaml:329` — intro prose hardcodes *"…structure of RoundsApp."*

The suggestion set must become a **projection of the loaded `AppModel`**, computed on load, so the panel describes the app in front of it. Target tier: **Tier 2 — triage list** (chips conditioned on what the model contains, carrying a count, smells sorted first).

---

## Architecture Brief

**Architecture choice: MVUX** (project default; the page is already an MVUX `MapModel` record with `IFeed`/`IState`). Suggestions are derived async state off the existing model feed — exactly the MVUX "feed projects from a service/document" shape. No MVVM here.

**Module structure**
- New file `Atlas.Core/SuggestionEngine.cs` — pure function, sits beside `GraphQueries` (same "same model in, same answer out" principle). No UI, no MVUX types; netstandard2.0-safe like the rest of Atlas.Core.
- New record `Atlas.Core/Suggestion.cs`.
- `Atlas.App/Presentation/MapModel.cs` — gains a `Suggestions` feed and a single `RunSuggestion` command; the four bespoke `Find*` commands collapse into one dispatch.
- `Atlas.App/Presentation/MapPage.xaml` — the four literal buttons become one `ItemsRepeater` over `Suggestions`.

**Contracts**
```csharp
// Atlas.Core
public enum SuggestionKind { Smell, Explorer }      // ordering + emission policy
public enum QueryId { Orphans, Duplicates, Unreachable, PathsTo, TraceLive }

public sealed record Suggestion(
    string Verb,          // "FIND" | "SHOW" | "DETECT" | "TRACE"
    string Label,         // generated, e.g. "Every path to Workload"
    QueryId Query,
    string? Arg,          // target node id for PathsTo; else null
    int? Count,           // pre-counted result size; null = not pre-run
    SuggestionKind Kind);
```

**`SuggestionEngine.For(AppModel) -> IReadOnlyList<Suggestion>`** — emission policy:
- **Smell chips** (emitted only when `Count > 0`, so a clean app shows no noise):
  - `Orphans` — count = `GraphQueries.FindOrphans(m).NodeIds.Count`.
  - `Duplicates` — count from `FindDuplicates`.
  - `Unreachable` — count of `Edges` with `EdgeKind.Unreachable`.
- **Explorer chips** (always emitted when applicable, no count gating):
  - `PathsTo {target}` — target = the node with the highest inbound-depth (deepest reachable leaf); tie-break by most inbound edges. Compute over the graph, never a literal id. Emit the top 1 (optionally top 2 if the second target is in a disjoint subtree).
  - `TraceLive` — emitted only when a node has `NodeStatus.Live` (runtime is connected and on a screen). Label: *"Trace route to {liveNodeName}"*, dispatches `PathsTo` with the live node id.
- **Ordering:** Smells first, by `Count` descending; Explorers after, stable. A model with real problems surfaces them at the top; a clean declared model (like the nursing fixture) shows just the explorer chip(s).

**Dispatch** — `MapModel.RunSuggestion(Suggestion s)` maps `QueryId` → existing `GraphQueries` call and writes `AgentResult` (reuse the current `RunQuery` private). `PathsTo` uses `s.Arg`; the rest ignore it. This removes the hardcoded `"meds"`.

**State model (MapModel)**
```csharp
public IFeed<IReadOnlyList<Suggestion>> Suggestions =>
    Graph.Select(SuggestionEngine.For);          // recomputes on every model snapshot
```
`Graph` already re-emits on Open-model and on every observed runtime route (`RuntimeBridge`), so suggestions track live edges flipping declared→observed for free.

**Data flow:** `RuntimeBridge.Models` → `Graph` feed → `Suggestions` projection → `ItemsRepeater`. Click → `RunSuggestion` → `GraphQueries` → `AgentResult` state → existing canvas highlight + answer prose (`EdgeLayer`/`NodeCard` `Highlighted*` bindings, unchanged).

**Platform constraints:** Pure Core engine runs everywhere including WASM. `TraceLive` simply never emits on WASM (no socket listener → no Live node), which is correct.

**Testing/validation**
- `Atlas.Tests/SuggestionEngineTests.cs` (pure, no UI):
  - `rounds-app-model.json` → expect Orphans(1, HandoffReport) + Unreachable(1) as top smells, PathsTo target resolves to a deep leaf (e.g. `meds`/`notes`), Duplicates present (VitalsEntry/MedAdministration share `NumberBox HR`).
  - `nursing-app-model.json` → expect **no smell chips** (clean), one Explorer `PathsTo` whose target is one of the five leaves. Assert the old `"meds"` literal never appears.
  - Determinism: same model in → identical ordered list out.
- Runtime: load each model in the viewer, confirm chip labels differ per app and counts match the query answers.

---

## Design Brief

**Visual direction:** unchanged Atlas IDE-minimal dark aesthetic. Chips keep `AtlasQueryButtonStyle`, the colored mono verb (`AtlasLiveBrush`), and `AtlasUiBody` label. The only new affordance is a **count badge**.

**Layout:** the static `AutoLayout` of four buttons (`MapPage.xaml:332-366`) becomes:
```
ItemsRepeater  ItemsSource={Binding Suggestions}  (FeedView-wrapped)
  StackLayout Vertical, Spacing 6
  ItemTemplate: AtlasQueryButtonStyle button
    AutoLayout Horizontal, Justify=SpaceBetween
      [ Verb (mono caption, AtlasLiveBrush) · Label (AtlasUiBody) ]
      [ Count badge — pill, AtlasMonoCaption, shown only when Count != null ]
```
**Typography/spacing:** reuse existing styles only (no new font sizes/hex per uno-scaffolding rules). Count badge = `Border` CornerRadius 999, `AtlasBorder2Brush` 1px, Padding 7,2, `AtlasMonoCaption` foreground `AtlasTextMutedBrush`.

**Component hierarchy:** the chips ItemsRepeater is wrapped in a `FeedView` (loading/none/data) so an empty suggestion set renders a tidy empty line rather than a gap.

**Theme usage:** all brushes from existing `Atlas*` resource keys. Per the runtime gotcha, custom-DP-in-template brushes use `StaticResource`, not `ThemeResource`.

**Responsive:** chips already live in a fixed 372px docked panel; no change. Narrow-width drawer layout (separate spec) inherits this list as-is.

**Intro prose:** bind the app name — replace the literal in `MapPage.xaml:329` with composed text. Use a converter or a `MapModel` projected string `AgentIntro => Graph.Select(m => $"The agent reads the same graph you see. Ask about the structure of {m.App}, or point it at a screen to scope an edit.")`.

---

## Interaction Brief

**User flow:** open/connect an app → chips regenerate within the same frame the canvas redraws → user clicks a chip → canvas highlights the result nodes/edges, answer prose appears below (existing `AgentResult` `FeedView`), "Clear highlights" resets.

**Input behavior:** single command `RunSuggestion` receives the `Suggestion` item. **Use the validated pattern** from `SelectNode` (`MapPage.xaml:167`): `utu:CommandExtensions.Command="{Binding Parent.RunSuggestion}"` on the ItemsRepeater so the bound item is the parameter — the uno-scaffolding gotcha says raw `CommandParameter` inside FeedView templates silently no-ops, and this attached-command form is the project's working workaround.

**Empty state:** clean app (no smells, e.g. nursing) → only explorer chip(s) show. If somehow zero suggestions, FeedView `NoneTemplate` shows muted *"No structural suggestions for this model."*

**Loading state:** while `Graph` resolves, FeedView `ProgressTemplate` shows nothing/skeleton — model load is sub-second from local JSON, so a bare placeholder is fine.

**Error state:** engine is total (never throws on a valid `AppModel`); a malformed model is already rejected upstream by `OpenModel` (`MapModel.cs:100`).

**Feedback/animation:** reuse the existing `Reveal` fade already applied to the `AgentResult` block; chips themselves don't animate on regenerate (avoid flespecially per-frame churn when live edges flip).

**Accessibility:** each chip keeps `AutomationProperties.Name` — generate it from verb+label so screen readers announce the live target (e.g. "Show every path to Workload"). Count badge gets `AutomationProperties.Name="{Count} found"`.

**Runtime verification steps:**
1. Launch viewer (`tools/run-atlas.ps1` or `uno_app_start`).
2. Bundled Rounds → confirm Orphans + Unreachable + Duplicates chips carry counts, PathsTo names a real deep screen, no `"meds"` literal.
3. Open `samples/nursing-app-model.json` → confirm smell chips disappear, one PathsTo chip names a nursing leaf, intro prose reads "…structure of NursingScheduleApp."
4. (If runtime wired) navigate the live app → a `TraceLive` chip appears naming the current screen.

---

## Implementation Plan

1. `Atlas.Core/Suggestion.cs` + `SuggestionKind`/`QueryId` enums.
2. `Atlas.Core/SuggestionEngine.cs` — `For(AppModel)`; inbound-depth target selection; smell-gating; ordering. Reuse `GraphQueries` for counts.
3. `Atlas.Tests/SuggestionEngineTests.cs` — rounds + nursing assertions, determinism. (Write before wiring UI.)
4. `MapModel.cs` — add `Suggestions` feed, `AgentIntro` string, `RunSuggestion(Suggestion)` dispatch; delete `FindPaths`/`FindOrphans`/`FindDuplicates`/`FindUnreachable` commands (or keep as private helpers `RunQuery` targets).
5. `MapPage.xaml` — replace the four buttons with the FeedView+ItemsRepeater chip list; bind intro prose; add count badge.
6. Build, run, walk the four verification steps. Commit per chip (`feat: model-derived agent suggestions`).

## Unresolved Questions

- **Explorer count:** emit top-1 PathsTo target only, or top-2 when subtrees are disjoint? (Default: top-1; revisit if it feels thin on wide graphs.)
- **Show clean smells as zero, or hide?** Spec hides smell chips at count 0. Alternative: always show all four with `· 0` badges for teach-ability. (Default: hide — less noise; accept as taste call.)
- **TraceLive vs PathsTo overlap:** when live, do we show both a generic deep-leaf PathsTo *and* a TraceLive, or collapse to TraceLive only? (Default: show both; TraceLive sorts first among explorers.)
- **Risk (5-min spike):** confirm `utu:CommandExtensions.Command` passes a non-`AppNode` item type (`Suggestion`) as the parameter cleanly — `SelectNode` proves it for `AppNode`; verify the generic case before building the template.
