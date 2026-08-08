# Network Diagnostics Desktop — Photino preview

This archive contains the Photino.NET rebuild of the Network Diagnostics desktop application. The desktop shell uses a React + TypeScript + Vite interface inside Photino while retaining the existing .NET diagnostics engine, schema 2.0 report contract, native monitoring services, report store, and export logic.

The application is self-contained; the .NET runtime does not need to be installed.

## Available in this build

- A single **Network Diagnostics** page organized around the current network: observe live health first, run controlled diagnostics second, and open specialized Advanced tools only when needed.
- **Live Network Health** is the primary opening state. Its health-score orb always represents passive network health; it does not change meaning when a diagnostic starts.
- Passive monitoring includes seven-day local history, responsiveness/reliability scoring, multiple time windows, connection timeline, pause/resume, local alerts, and a clearly labeled last-measured capacity state.
- Copyable network summary, self-contained HTML health snapshot export, and privacy-aware CSV monitoring-history export are available from the secondary Share & export control.
- **Run Diagnostics** owns the active-test lifecycle with distinct Ready, Running, and Latest Result states while passive monitoring continues independently.
- **Connection Check, Quick, Full, and Stress** profiles are backed by the native `NetworkDiagnosticsRunner` with **Single, Aggregate, and Compare** transfer methods.
- Live phase, latency, throughput, payload, progress, cancellation, completion, and failure feedback stay inside the active diagnostic region rather than replacing the live-health score.
- Lower-data **Content speed** (`Connection Check + Aggregate`) and high-data **Peak capacity** (`Stress + Aggregate`) are explicit diagnostic presets rather than additional monitoring modes.
- The latest successful diagnostic remains available until another run completes; selecting a different profile only prepares the next run.
- Native Full/Stress evidence includes interface/network context, gateway and ICMP latency, traceroute, DNS resolvers, path MTU, service reachability, Wi-Fi/routing details, loaded-path localization, dual-stack/HTTP3 evidence, network-change detection, and host-resource evidence when supported by the platform.
- Persisted nondefault Advanced configuration is surfaced beside the normal Run controls so endpoint/interface/privacy/LAN overrides never silently change a diagnostic.
- **Advanced diagnostics** uses progressive disclosure for Targeting, LAN diagnostics, and Preflight. Only the selected specialized tool expands into detailed controls.
- Advanced Targeting supports endpoint candidates, interface binding, and explicit local-identifier opt-in. LAN supports peer target/port/duration/connections and the native throughput server. Preflight verifies the saved native route/target configuration without starting the full throughput run.
- Unsaved Advanced edits are explicitly labeled, and a running LAN server keeps its actual listening port visible as persistent application state.
- Schema 2.0 reports are persisted through the existing atomic report store.
- **History** remains a focused side workspace for saved-run detail, findings/evidence, comparison, JSON import/export, and persisted labels/tags.
- **Settings** is limited to application preferences such as appearance; diagnostic capability is not hidden behind the gear menu.
- **Cmd/Ctrl-F command palette** provides keyboard jumps to Live Network Health, Run Diagnostics, Advanced diagnostics, History, diagnostic profiles, transfer methods, and run/cancel actions.
- Persisted **System / Light / Dark** appearance preference.
- Responsive Photino/WebView interface validated in dark and light themes and compact window sizes.

## System tray

The previous Avalonia prototype included an optional system-tray/menu-bar convenience surface for showing the window, displaying the current score, pausing/resuming monitoring, and quitting. This Photino build intentionally does **not** include that tray surface.

Photino does not currently provide a built-in cross-platform tray/menu abstraction, and the available third-party options either omit macOS support or add substantial platform dependencies. No diagnostic, monitoring, alert, report, or export capability depends on the tray, so the migration keeps the five-platform desktop stack lean rather than reintroducing a UI framework solely for tray behavior.

## Reports and privacy

Completed reports and monitoring history are stored under the operating system's local application-data directory. Report writes use a temporary file followed by an atomic replacement. Labels and tags are stored inside the schema 2.0 report itself.

Local interface and network identifiers are excluded by default. They are included only when explicitly enabled under **Advanced diagnostics**. Privacy-aware CSV export follows the same setting. The schema reader ignores unknown optional fields, and native sections that were not measured are represented as **Not measured** rather than failures.

## Run

### Windows

```powershell
.\NetworkDiagnosticsDesktop.exe
```

### Linux

```bash
chmod +x NetworkDiagnosticsDesktop
./NetworkDiagnosticsDesktop
```

### macOS

macOS packages contain **Network Diagnostics.app**. You can move it to Applications or open it from the extracted folder.

Pull-request and ordinary CI artifacts are bundled as proper macOS applications but are not signed or notarized. The safest first-launch flow is to try opening the app normally, then use **System Settings → Privacy & Security → Open Anyway** if macOS blocks it.

For a local CI artifact that macOS has quarantined, you can also clear quarantine for this extracted app only and launch it:

```bash
xattr -dr com.apple.quarantine "Network Diagnostics.app"
open "Network Diagnostics.app"
```

Do not disable Gatekeeper globally.

## Signing status

Release signing and notarization require an Apple Developer ID certificate and notarization credentials configured as repository secrets. Once those credentials are available, the release workflow can produce a notarized distribution without the unsigned-build warning.