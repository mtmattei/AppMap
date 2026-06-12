# Atlas Narrow-Width Layout — Spec

The side panel (Agent/Inspector, fixed 372px column) collapses below a width
breakpoint and becomes a transient right-edge drawer, keeping the canvas usable
on narrow windows (snapped half-screen, small laptops, future tablet targets).

## Architecture Brief

- **Pattern**: MVUX, unchanged — this is a view-layer adaptation of `MapPage`.
  No new models; `PanelTab` (existing `IState<int>`) keeps owning *which* pane
  shows. A view-only `IsPanelOpen` lives in the page (drawer visibility is
  chrome state, not domain state — it should not survive model reloads or be
  visible to the agent).
- **Structure**: `MapPage.xaml` gains a `VisualStateGroup` with two states:
  - `Wide` (`AdaptiveTrigger MinWindowWidth="1000"`): current layout — panel
    is grid column 1 at 372px; drawer host collapsed.
  - `Narrow` (`MinWindowWidth="0"`): panel column width 0; the panel content
    moves into a right-edge transient surface over the canvas.
- **Panel content is single-sourced.** The TabBar + both panes must not be
  duplicated per state. Extract them into a named element that both states
  position (column vs overlay) via setters on `Grid.Column`/alignment/width —
  or, if reparenting via setters proves unreliable on Skia, extract to a
  `PanelHost` UserControl instantiated once and re-slotted in code on state
  change. Decide in the first spike (see Risks).
- **Drawer mechanism — decision**: hand-rolled overlay, not Toolkit
  `DrawerControl`.
  - Reason: both validated Atlas gotchas hit DrawerControl directly — the
    light-dismiss overlay blocks the main content while open, and
    `DrawerDepth`/`OpenDirection` setters crash at XAML parse (must be set in
    `Loaded`). A transient overlay is ~40 lines we fully control: a scrim
    `Border` + a 372px panel translated on/off-screen.
  - Tradeoff: we own the swipe-to-dismiss gesture (skip it — scrim tap +
    Escape are enough for v1) and the open/close animation (we already have
    the storyboard patterns from the zoom/Reveal work).
- **Platform constraints**: desktop Skia first; same XAML runs on WASM. No
  platform-specific code expected. `AdaptiveTrigger` is supported on Uno.
- **Testing/validation**: unit tests don't apply (view-only). Validate by
  resizing the desktop window across the breakpoint both directions, via the
  App MCP visual-tree snapshots at two window sizes, and the runtime checks in
  the Interaction Brief.

## Design Brief

- **Visual direction**: unchanged Super Normal/IDE-minimal register. The
  drawer is the existing panel surface (`AtlasPanelBrush`, hairline
  `AtlasBorderBrush` left edge) sliding over the canvas; no new chrome styles.
- **Breakpoint**: 1000px window width. Rationale: 372 (panel) + ~600 (minimum
  useful canvas at fit zoom) + margins. Single breakpoint only — no
  intermediate compact-panel state (do not overbuild).
- **Narrow layout**:
  - Canvas takes the full width.
  - Top bar unchanged; add a trailing icon button (`Segoe Fluent Icons`
    sidebar glyph, 44×44 target) that toggles the drawer. Visible only in
    `Narrow` state.
  - Drawer: right-anchored, 372px wide (clamped to `min(372, 85vw)`), full
    content height, over a scrim (`#000` at 35% opacity).
- **Spacing/typography**: reuse existing styles; no new resources except the
  scrim brush (goes in `AtlasBrushes.xaml`).
- **Component hierarchy**: `CanvasHost` (unchanged) → `Scrim` → `DrawerHost`
  (contains the single-sourced panel content).

## Interaction Brief

- **Open**: tap the panel toggle button; or select a node (`SelectNode`
  already flips `PanelTab` to Inspector — in `Narrow` it must also open the
  drawer, so selection always shows its consequence).
- **Close**: scrim tap, Escape, or the toggle button again. Closing does not
  clear selection or highlights.
- **Crossing the breakpoint**: Narrow→Wide with drawer open → drawer state
  discards, panel docks (no animation). Wide→Narrow → drawer starts closed.
- **Animation**: drawer translates X 372→0 over 200ms ease-out; scrim fades
  0→0.35 alongside. Exit mirrors entrance (slide right + fade out), per the
  exit-mirrors-entrance rule. Reuse the Reveal/storyboard patterns.
- **Input**: while the drawer is open, the scrim swallows canvas pointer
  input (that's its job — unlike DrawerControl's overlay, it's visibly a
  scrim). Canvas keyboard shortcuts (Ctrl+9 etc.) keep working; Escape closes
  the drawer before anything else (handle in drawer's KeyDown, mark Handled).
- **Empty/loading/error states**: unchanged — they live inside the panes.
- **Feedback**: notice pill stays bottom-center of the *canvas*, so it remains
  visible when the drawer is closed; with the drawer open it may sit under the
  scrim — acceptable (notices triggered from the drawer confirm actions whose
  result is in the drawer).
- **Accessibility**: toggle button gets `AutomationProperties.Name="Toggle
  panel"`; drawer traps Tab while open is *not* required for v1 (accepted
  gap); Escape-to-close covers keyboard users.
- **Runtime verification steps**:
  1. Resize below 1000px → panel column disappears, toggle button appears.
  2. Click a node while narrow → drawer slides in showing Inspector.
  3. Scrim tap and Escape both close; selection ring still on the node.
  4. Resize above 1000px with drawer open → docked panel, no dead scrim.
  5. Agent prompt from the drawer → highlights visible after close.

## Implementation Plan

1. Spike (time-boxed 15 min): confirm `AdaptiveTrigger` fires on Skia desktop
   window resize, and whether `Grid.Column`/width setters can re-slot the
   existing panel without duplication. Fallback: `PanelHost` UserControl +
   code-behind re-slotting on state change.
2. Extract panel content to the single-sourced element; verify Wide layout is
   pixel-identical (screenshot diff vs current).
3. Add `VisualStateGroup` + breakpoint, narrow column collapse, toggle button.
4. Add scrim + drawer host + open/close storyboards; wire toggle, scrim tap,
   Escape.
5. Hook `SelectNode` → drawer-open in Narrow (view listens to `PanelTab`
   changes or the existing selection-driven tab flip).
6. Verify the five runtime steps via App MCP + human pass; commit per step.

Pinned versions: whatever `Directory.Packages.props` already pins (validated:
Uno.Sdk 6.5.x line, Toolkit 8.4.2). No new packages.

## Unresolved Questions

- Does `AdaptiveTrigger` re-evaluate reliably on Skia desktop during live
  window resize? (Spike step 1; if not, fall back to a `SizeChanged` handler
  calling `GoToState` — keep the states declarative either way.)
- Can the panel be re-slotted between column and overlay purely with
  VisualState setters, or does Skia require code-behind reparenting? (Spike
  step 1 decides; both paths are speced above.)
- Should the legend also compact below ~800px (it overlaps more canvas when
  narrow)? Deferred — not part of this feature; note for a later pass.
