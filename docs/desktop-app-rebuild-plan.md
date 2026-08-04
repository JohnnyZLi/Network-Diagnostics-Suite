# Desktop App Rebuild Plan

## Decision

Build the new desktop application branch directly from `main`.

- Working branch: `agent/desktop-app-rebuild`
- Starting commit: `31ca07efd69adffe0597c4cb1808c71da4e0eb86`
- Preserve `rescue/codex-diagnostic-overhaul` as a reference snapshot.
- Preserve PR #103 until all useful contract and native-planning work has been transferred and validated.
- Do not merge or continue the UI from PR #101.
- Do not merge PR #102 or PR #103 yet.

The clean branch avoids carrying the horizontal desktop redesign into the new app. PR #103 contains useful additive backend work, but its UI inheritance is not a suitable foundation.

## Product boundaries

### Website

The website remains at its approved pre-overhaul state.

Profiles:

- Quick
- Full
- Stress

Website changes are allowed only when required for schema 2.0 compatibility or to preserve measurements in exported reports. Advanced native fields do not need to appear in the website UI.

### Desktop and CLI

Profiles:

- Connection Check — Is the connection working normally?
- Quick — What performance am I getting now?
- Full — Where is the likely problem?
- Stress — How does the connection behave under sustained load?

The desktop application and CLI may collect richer native evidence. Every measurement actually collected must be serialized, even when it is not displayed in the default results view.

### Report compatibility

All combined website and native reports remain schema 2.0.

Readers must:

- ignore unknown optional fields;
- preserve measurements they do understand;
- render unsupported or absent native sections as `Not measured`;
- never turn an unsupported section into a failed diagnostic;
- distinguish a failed measurement from a measurement that was not attempted.

## Migration boundary from PR #103

Port the twelve non-UI files from PR #103 onto the clean branch:

1. `contracts/desktop-test-profiles.v1.json`
2. `contracts/report-v2.schema.json`
3. `tests/report-contract.test.mjs`
4. `tools/DeepProbe.Tests/NativeTransferPlanTests.cs`
5. `tools/DeepProbe.Tests/ProbeOptionsTransferTests.cs`
6. `tools/DeepProbe.Tests/ProfileContractTests.cs`
7. `tools/DeepProbe.Tests/ReportSerializationCompatibilityTests.cs`
8. `tools/DeepProbe/Diagnostics/ProbeOptions.cs`
9. `tools/DeepProbe/Planning/TransferPlan.cs`
10. `tools/DeepProbe/Program.cs`
11. `tools/NetworkDiagnostics.Core/Contracts/TestProfileContract.cs`
12. `tools/NetworkDiagnostics.Core/NetworkDiagnostics.Core.csproj`

Do not port `tools/NetworkDiagnostics.Desktop/MainWindow.axaml`.

Do not cherry-pick `tools/NetworkDiagnostics.Desktop/MainWindow.axaml.cs` wholesale. Reimplement its small profile-selection changes inside the new state-driven desktop architecture:

- add `Connection Check` as the default desktop profile;
- use eight idle pings for Connection Check;
- require data-use confirmation only for Full and Stress;
- keep the stable JSON identifier `connection-check`.

After the clean implementation is validated, compare the result against PR #103 and the rescue branch to confirm that no intended contract, serialization, profile-planning, or diagnostic-engine work was omitted.

## Desktop information architecture

The app has three top-level destinations:

1. **Test**
2. **History**
3. **Settings**

Setup, Running, Results overview, and Detailed results are lifecycle states inside **Test**, not equal navigation destinations. Report details are rendered through the same results components used by a newly completed test.

This structure avoids a dashboard that shows configuration, live progress, history, and dense results simultaneously.

## Application shell

Use a restrained native shell with:

- a compact app header;
- persistent navigation for Test, History, and Settings;
- one primary content region;
- a small contextual action area for actions such as export, open report folder, or start another test;
- native focus, keyboard navigation, accessibility, and high-contrast behavior.

Do not reproduce the website layout mechanically. Use the same terracotta and neutral design language, but let Avalonia own native interaction patterns.

At normal window sizes, the main content should have a readable maximum width rather than stretching controls edge to edge. Avoid a dense engineering-dashboard presentation.

## Test lifecycle

### 1. Setup

Purpose: choose what question the test should answer and start it.

Default content:

- page title and one-sentence explanation;
- four profile choices with purpose, approximate duration, and maximum transfer;
- Connection Check selected by default;
- one clear primary action;
- a compact privacy and data-use note.

Advanced setup is collapsed by default:

- transfer method;
- diagnostic target;
- optional LAN target;
- include local identifiers in the saved report;
- endpoint override when development or troubleshooting requires it.

Connection scaling must not be visually emphasized for Connection Check. Technical plan details may appear in an expandable `Test details` section.

Full and Stress open the existing data-use confirmation before execution. Connection Check and Quick do not.

### 2. Running

Purpose: show that useful work is occurring without competing with setup or completed results.

Show:

- selected profile and its purpose;
- current phase and plain-language status;
- overall progress when determinate;
- live throughput, latency, or transferred data only when meaningful;
- elapsed time;
- a secondary Stop action.

Hide or disable setup controls. Do not show stale previous results in the main content area.

Running-state fixtures must cover:

- normal determinate progress;
- an indeterminate native-diagnostics phase;
- cancellation;
- a recoverable stage error;
- a fatal run error.

### 3. Results overview

Purpose: answer the profile's main question before showing evidence.

Order:

1. verdict;
2. short explanation;
3. key metrics;
4. important findings;
5. recommended next action;
6. links to detailed results and export.

Verdict language must be profile-aware:

- Connection Check: working normally, possible issue, or unable to determine;
- Quick: current performance summary;
- Full: likely problem area or no clear bottleneck found;
- Stress: sustained-load behavior summary.

The overview must not claim certainty that the collected evidence does not support.

### 4. Detailed results

Use progressive disclosure with these sections:

- Overview
- Performance
- Responsiveness
- Local Network
- Path & Services
- Technical

Sections remain in a stable order across profiles. A section that was not measured is shown neutrally as `Not measured`; it is not removed in a way that makes reports difficult to compare.

Profile emphasis:

- Connection Check: Overview, Responsiveness, and limited Performance evidence;
- Quick: Performance and Responsiveness;
- Full: all diagnostic sections;
- Stress: Performance, Responsiveness, and connection-scaling evidence.

Technical contains raw or specialist evidence that should not dominate the default view, including endpoint selection, candidate probes, stage details, route data, detailed distributions, capability identifiers, engine metadata, and report JSON metadata.

## History and report details

### History

Use a master-detail layout when space permits.

Each row should show:

- date and time;
- profile;
- verdict or completion state;
- key metric summary;
- producer badge when useful;
- imported versus locally generated status.

Actions:

- open report;
- import JSON;
- export or copy report;
- reveal reports folder;
- delete a local report with confirmation.

### Report details

Opening a historical or imported report uses the same Results overview and Detailed results renderer as a fresh run.

Compatibility behavior:

- unknown optional fields are ignored;
- recognized fields are rendered;
- absent native fields show `Not measured`;
- producer and capability metadata may explain why a section is unavailable;
- malformed required schema fields produce an import error, not a failed network verdict.

## Settings

Keep Settings small initially.

First version:

- default profile;
- default transfer method inside advanced setup;
- report storage location and open-folder action;
- whether local identifiers are included by default;
- development endpoint override, clearly marked advanced;
- reset remembered Full and Stress data-use approvals.

Do not move frequently used test choices into Settings merely to simplify Setup.

## Result-state language

Every result component must support these states explicitly:

### Healthy

The measurement completed and available evidence is within the app's defined normal range.

### Problematic

The measurement completed and evidence supports one or more actionable findings.

### Inconclusive

The run completed, but evidence is insufficient or contradictory. Explain what prevented a stronger conclusion and identify the next useful test.

### Unavailable / Not measured

The capability was unsupported, the section was outside the selected profile, or the data was absent from an imported report. This is neutral, not a failure.

### Failed

The attempted measurement could not complete. Preserve partial evidence when safe and clearly identify which measurement failed.

### Running

The measurement is in progress and has no final verdict.

The view model should represent these states directly rather than deriving them from display strings.

## UI implementation structure

Move away from one large `MainWindow.axaml` and code-behind renderer.

Suggested structure:

```text
tools/NetworkDiagnostics.Desktop/
  MainWindow.axaml
  MainWindow.axaml.cs
  Models/
    AppRoute.cs
    TestSessionState.cs
    ResultState.cs
  ViewModels/
    MainWindowViewModel.cs
    SetupViewModel.cs
    RunningViewModel.cs
    ResultsViewModel.cs
    HistoryViewModel.cs
    SettingsViewModel.cs
  Views/
    SetupView.axaml
    RunningView.axaml
    ResultsOverviewView.axaml
    DetailedResultsView.axaml
    HistoryView.axaml
    ReportDetailsView.axaml
    SettingsView.axaml
  Components/
    ProfileChoice.axaml
    VerdictPanel.axaml
    MetricSummary.axaml
    FindingCard.axaml
    MeasurementState.axaml
  Services/
    DiagnosticRunService.cs
    ReportStore.cs
    ReportImportService.cs
    ReportPresentationService.cs
  DesignData/
    MockReports.cs
```

The exact MVVM package can be decided during implementation, but navigation, run state, report persistence, and presentation logic must not remain tightly coupled to individual controls in `MainWindow.axaml.cs`.

## Static design fixtures

Before connecting the engine, create deterministic mock fixtures for:

- Connection Check healthy;
- Connection Check problematic;
- Connection Check inconclusive;
- Connection Check with an unavailable native capability;
- Connection Check with a failed measurement and partial evidence;
- Connection Check running;
- imported website schema 2.0 report;
- imported desktop schema 2.0 report containing unknown optional fields.

The mock renderer and real-report renderer must use the same view models. Mock-only XAML should not become a parallel implementation.

## Vertical-slice sequence

### Slice 0 — Foundation and safe transfer

- port the twelve non-UI PR #103 files;
- validate website profile separation and schema 2.0 compatibility;
- add cross-client report fixtures;
- create the app shell, navigation model, result-state model, and mock-data path;
- keep the existing main desktop UI available until the replacement shell can launch reliably.

### Slice 1 — Connection Check

Implement one complete workflow:

- Setup;
- Full/Stress confirmation behavior remains testable even before those profiles are complete;
- Running;
- healthy, problematic, inconclusive, unavailable, failed, and cancelled states;
- Results overview;
- relevant detailed sections;
- report save;
- History entry;
- reopen through Report details;
- export.

Connect the real Connection Check engine only after the static state set is coherent.

### Slice 2 — Quick

- preserve the original Quick measurement plan;
- add profile-specific setup copy;
- emphasize current throughput and responsiveness;
- add single-versus-aggregate evidence without turning the overview into a scaling dashboard.

### Slice 3 — Full

- add diagnostic findings and likely-problem synthesis;
- add Local Network and Path & Services views;
- show unsupported OS capabilities as `Not measured`;
- validate Full data-use confirmation.

### Slice 4 — Stress

- add sustained-load verdicts;
- add progressive connection-scaling evidence;
- keep detailed scaling outside the default overview;
- validate Stress data-use confirmation and cancellation under load.

## Validation gates

Every slice must keep these checks green:

- browser profile contract remains Quick, Full, Stress;
- desktop profile contract remains Connection Check, Quick, Full, Stress;
- existing website schema 2.0 reports validate;
- desktop schema 2.0 reports validate;
- web reader accepts desktop reports with optional native fields;
- desktop reader accepts website reports and shows native sections as `Not measured`;
- unknown optional fields do not break either reader;
- native tests;
- desktop build and publish for supported targets;
- UI regression and visual audit;
- design-system conformance;
- secret scan.

For visual review, capture at least:

- Setup at minimum and normal window sizes;
- Running;
- each Connection Check result state;
- detailed results with measured and not-measured sections;
- History with no reports and multiple reports;
- imported website report;
- Settings;
- 200% scaling or equivalent high-DPI review.

## Immediate next implementation step

Port the twelve non-UI files from PR #103 onto `agent/desktop-app-rebuild` as one isolated commit. Run the existing browser, contract, native, and desktop build checks before creating the new Avalonia shell.

The first UI commit should contain only the shell, navigation, explicit state models, and static Connection Check fixtures. It should not connect the real diagnostic runner yet.