# Atlas — Agentic App Map

A cross-platform .NET (Uno Platform) tool that renders an Uno app's full structure as an
interactive canvas, and exposes that structure as one model a developer and an AI agent both read.

The thesis: an agent shouldn't have to understand an app only through files. Atlas gives it — and
you — the same view: screens, routes, and the relationships between them, with each edge marked by
where it came from (declared in the route map, observed at runtime, or unreachable/dead).

## What's here

| File | Purpose |
|---|---|
| `SPEC.md` | The build spec: Architecture / Design / Interaction briefs, phased plan, open questions. Start here after this README. |
| `CLAUDE.md` | Operating guide for Claude Code: Uno conventions, commands, stop conditions, and the rule to use the Uno MCP before any Uno-specific code. |
| `samples/rounds-app-model.json` | The canonical `AppModel` fixture (a nurse-rounding app). Build the viewer against it before extraction exists. |
| `design/atlas-prototype.html` | The interactive design reference. Open it to see the intended canvas, provenance edges, inspector, and agent panel. |

## Build order

1. Open this repo in Claude Code. It reads `CLAUDE.md`, then `SPEC.md`.
2. Claude Code initializes the Uno MCP rules and scaffolds the solution (Phase 0).
3. Build Phase 1 — the static viewer — entirely against `samples/rounds-app-model.json`. This is a
   complete, demoable product: canvas, provenance layers, inspector, structural agent queries.
4. Phases 2–4 add the runtime feed (`IRouteNotifier`), Roslyn extraction, and act-from-canvas.

## The one idea to hold onto

`AppModel` is the center. The canvas renders it, the agent reads it, the runtime feed mutates it,
and code stays the source of truth. Everything else is a projection.
