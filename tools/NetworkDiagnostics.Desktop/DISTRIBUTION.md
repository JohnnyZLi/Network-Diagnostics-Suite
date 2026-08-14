# Network Diagnostics Desktop

This archive contains the Photino.NET rebuild of the Network Diagnostics desktop application. The desktop shell uses a React + TypeScript + Vite interface inside Photino while retaining the existing .NET diagnostics engine, schema 2.0 report contract, native monitoring services, report store, and export logic.

The application is self-contained; the .NET runtime does not need to be installed.

Only one application instance runs for a user by default so monitoring, active diagnostics, and report storage are not duplicated. Developers and automated visual checks can opt out with `--allow-multiple-instances`.

## Available in this build

- Three primary workspaces keep the instrument compact: **Live Network Health**, **Run Diagnostics**, and **Advanced Diagnostics**. History, alerts, and settings remain secondary panels.
- **Live Network Health** opens first. Its score and timeline always represent passive monitoring, even while controlled diagnostic traffic is running.
- Passive monitoring includes seven-day local history, responsiveness and reliability scoring, multiple time windows, capacity history, pause/resume, local alerts, and privacy-aware summary/HTML/CSV exports.
- **Run Diagnostics** exposes native **Connection, Quick, Full, and Stress** profiles; **Both, Single, and Aggregate** transfer topologies; **Automatic, Direct R2, and Worker** download paths; and automatic or explicit network-interface routing.
- The setup workspace shows the actual native run plan: estimated runtime, transfer cap, baseline sampling, download runs, service checks, diagnostic depth, and every connection stage.
- Native preflight data shows the selected endpoint, provider/network, edge, latency, interface, requested download path, actual delivery path, and R2 availability or fallback behavior before launch.
- Each profile has an explicit measurement manifest. Full and Stress include the wider native system suite: interface/network context, gateway and ICMP latency, traceroute, DNS resolvers, path MTU, service reachability, Wi-Fi/routing data, loaded-path localization, dual-stack and HTTP/3 data, network-change detection, and host-resource data where supported.
- A running diagnostic replaces setup controls with native phase and stage progress, elapsed/remaining time, transferred payload, live latency/throughput, the active path, and cancellation controls.
- A completed diagnostic immediately prioritizes the native verdict, findings, measurements, actual delivery path, recommended next step, and expandable schema 2.0 technical data. A result remains usable and exportable in the current session if its automatic local save fails, with an explicit retry action.
- **Advanced Diagnostics** covers ordered endpoint candidates, explicit interface binding, local-identifier privacy, native preflight, a standalone LAN throughput client, and a LAN server with copyable pairing targets.
- **History** supports saved-run search/filtering, report detail, findings, measurements, full schema 2.0 technical data, comparison, deletion, JSON import/export, and persisted labels/tags.
- **Settings** owns System/Light/Dark appearance, expected capacity, passive-monitor cadence and alert thresholds, the reports directory, and report retention. Diagnostic configuration remains in the workbench.
- **Cmd/Ctrl-F** opens a keyboard command palette for workspaces, profiles, topologies, download paths, preflight, run/cancel actions, alerts, and History.
- Schema 2.0 reports continue to use the existing atomic native report store and remain compatible with existing readers.

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
