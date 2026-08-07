# Network Diagnostics Desktop — Photino preview

This archive contains the Photino.NET rebuild of the Network Diagnostics desktop application. The desktop shell uses a React + TypeScript + Vite interface inside Photino while retaining the existing .NET diagnostics engine, schema 2.0 report contract, native monitoring services, report store, and export logic.

The application is self-contained; the .NET runtime does not need to be installed.

## Available in this build

- **Connection Check, Quick, Full, and Stress** diagnostic profiles backed by the native `NetworkDiagnosticsRunner`.
- **Single, Aggregate, and Compare** transfer modes.
- Live phase, latency, throughput, payload, progress, cancellation, completion, and failure feedback.
- Native Full/Stress evidence including interface/network context, gateway and ICMP latency, traceroute, DNS resolvers, path MTU, service reachability, Wi-Fi/routing details, loaded-path localization, dual-stack/HTTP3 evidence, network-change detection, and host-resource evidence when supported by the platform.
- Persisted **System / Light / Dark** appearance preference.
- Schema 2.0 report persistence through the existing atomic report store.
- **History** with saved-run detail, findings/evidence, comparison, JSON import/export, and persisted labels/tags.
- Continuous **Monitor** workspace with seven-day local history, responsiveness/reliability/speed scoring, multiple time windows, pause/resume, local alerts, and completed diagnostic throughput fed back into speed history.
- Monitor utilities for a lower-data **Content** speed check and higher-capacity **Peak** check.
- Copyable network summary, self-contained HTML health snapshot export, and privacy-aware CSV monitoring-history export.
- **Advanced** configuration for endpoint candidates, interface binding, explicit local-identifier opt-in, LAN target/port/duration/connections, native preflight, and LAN throughput server controls.
- **Cmd/Ctrl-F command palette** for keyboard navigation, profile actions, and transfer-mode changes.
- Responsive Photino/WebView interface validated in dark and light themes and compact window sizes.

## System tray

The previous Avalonia prototype included an optional system-tray/menu-bar convenience surface for showing the window, displaying the current score, pausing/resuming monitoring, and quitting. This Photino build intentionally does **not** include that tray surface.

Photino does not currently provide a built-in cross-platform tray/menu abstraction, and the available third-party options either omit macOS support or add substantial platform dependencies. No diagnostic, monitoring, alert, report, or export capability depends on the tray, so the migration keeps the five-platform desktop stack lean rather than reintroducing a UI framework solely for tray behavior.

## Reports and privacy

Completed reports and monitoring history are stored under the operating system's local application-data directory. Report writes use a temporary file followed by an atomic replacement. Labels and tags are stored inside the schema 2.0 report itself.

Local interface and network identifiers are excluded by default. They are included only when explicitly enabled in Advanced settings. Privacy-aware CSV export follows the same setting. The schema reader ignores unknown optional fields, and native sections that were not measured are represented as **Not measured** rather than failures.

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
