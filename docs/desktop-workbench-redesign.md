# Desktop network diagnostics workbench

> **Historical design record.** The shipped application now uses a React + TypeScript + Vite workbench in Photino.NET, not Avalonia. See the [current desktop package guide](../tools/NetworkDiagnostics.Desktop/DISTRIBUTION.md) and [native architecture](native-architecture.md) for implemented behavior.

This document defines the product and interaction architecture for the native desktop redesign. The diagnostic engine, report schema, report storage, packaging, and cross-platform support remain intact. The existing desktop presentation layer is replaced incrementally through focused pull requests.

## Product boundary

The website remains the quick-access diagnostic surface with three profiles, simplified results, and browser-safe evidence. The desktop application becomes a persistent diagnostic workstation with report management, explicit comparisons, deep evidence, LAN isolation, expert configuration, and keyboard-first workflows.

The products may share the name, logo, report schema, and one accent color. The desktop application does not inherit the website's editorial layout.

## Reference patterns

| Product | Pattern adopted | Problem solved |
| --- | --- | --- |
| Little Snitch | Live phase-linked timeline and evidence inspector | Makes a running diagnostic investigatory rather than decorative |
| TablePlus | Dense tables, split panes, preserved selection, contextual actions | Makes large report libraries efficient to scan and operate |
| Linear | Navigation history, selection behavior, keyboard workflow | Makes the application feel predictable across nested workspaces |
| Raycast | Shared command model and command palette | Prevents toolbar, menu, context-menu, and shortcut logic from diverging |
| Apple professional applications | Sidebar, toolbar, content area, inspector, status bar | Establishes a familiar desktop information hierarchy |

The application does not copy another product's branding, ornament, or exact layout. These references define interaction patterns only.

## Permanent workspaces

- Test
- Reports
- Comparisons
- Settings
- Monitoring, after the redesigned core workflows ship

A running diagnostic is application-level activity. It is not owned by the currently visible page and continues while the user inspects reports or settings.

## Shell regions

1. Toolbar: back, forward, breadcrumb/title, search where relevant, and contextual actions.
2. Sidebar: permanent workspaces and persistent active-run status.
3. Main workspace: setup, running test, result, report list, report detail, comparison, or settings.
4. Inspector: metadata, selected-item configuration, and contextual actions.
5. Status bar: interface, endpoint, active test, network state, and measured data use.

At medium widths the sidebar compacts and the inspector collapses. At narrow widths the sidebar becomes a drawer and the inspector becomes an overlay or secondary destination. Primary actions must never require horizontal scrolling.

## Typed destinations

The navigation layer uses typed destinations instead of manually treating controls as pages:

- `TestSetupDestination`
- `RunningTestDestination(runId)`
- `TestResultDestination(reportId, section)`
- `ReportListDestination`
- `ReportDetailDestination(reportId, section)`
- `ComparisonDestination(baselineId, candidateId)`
- `SettingsDestination(section)`

Each history entry can preserve search, filters, sort order, selection, scroll offset, expanded sections, result section, sidebar state, inspector state, and comparison selections.

## History behavior

- Back restores chronological context.
- Forward restores a destination after a back operation.
- Navigating to a new destination after going back clears the forward stack.
- Breadcrumbs move through structural hierarchy and do not replace chronological history.
- Back navigation never cancels an active diagnostic.
- Invalid or deleted report identifiers fall back safely to the report list.

Keyboard conventions:

- macOS: Command+Left Bracket and Command+Right Bracket
- Windows and Linux: Alt+Left and Alt+Right
- Mouse back and forward buttons where the platform exposes them

## Persistent run session

One application-level service owns the current run identifier, profile, method, progress, current phase, cancellation, live measurements, final report, and terminal state. Views observe that service and do not own the diagnostic lifetime.

The sidebar always exposes the active run and allows the user to return to it. Closing the application during a run requires an explicit cancel-and-quit or keep-open choice.

## Pull-request boundaries

### PR A — Application shell and navigation

- Typed destinations and navigation entries
- Back and forward stacks
- Breadcrumb model
- Sidebar, toolbar, inspector shell, and status bar
- Route application and state restoration hooks

### PR B — Reports and comparison

- Dense report table
- Report detail workspace
- Explicit baseline and candidate selection
- Comparison metrics, cautions, evidence differences, and actions

### PR C — Test setup and persistent run session

- Compact setup form
- Inspector configuration
- Application-level active-run service
- Navigation during a run

### PR D — Running and results

- Live phase timeline
- Progress and stop behavior
- Result hierarchy and evidence sections
- Partial-evidence states

### PR E — Settings, command palette, and persistence

- Settings categories
- Window and workspace persistence
- Shared command/action system

### PR F — Testing and platform polish

- Navigation and interaction automation
- Visual regression and scaling coverage
- Accessibility
- macOS, Windows, and Linux refinements

## Avalonia decision gate

Avalonia remains the frontend through the Reports and Comparison vertical slice. Reassessment occurs only after a properly architected implementation is evaluated for navigation responsiveness, table and split-pane polish, keyboard reliability, control-state consistency, macOS intentionality, and maintenance cost.
