# Network Diagnostics Desktop — Photino migration preview

This archive contains the in-progress Photino.NET rebuild of the Network Diagnostics desktop application. The native shell now uses a React + TypeScript interface while retaining the existing .NET diagnostics engine and schema 2.0 report contract.

The application is self-contained; the .NET runtime does not need to be installed.

## Available in this preview

- **Connection Check** — lightweight first-party reachability, latency, request-loss, download, and upload measurement.
- **Compare / Single / Aggregate** transfer modes backed by the existing native measurement engine.
- Live phase, latency, throughput, payload, progress, cancellation, completion, and failure feedback.
- Native **System / Light / Dark** appearance preference persisted in the desktop data directory.
- Completed schema 2.0 reports are saved locally through the existing atomic report store.
- A compact **History** panel lists saved runs with timestamp, profile, latency, download, upload, loss, transfer size, labels, and tags already present in the report.
- Responsive Photino/WebView interface validated in dark and light themes and compact window sizes.

The saved-runs panel is deliberately read-only in this preview. Full report detail, comparison, import/export, and annotation editing are the next report-browser pieces to migrate.

## Not yet exposed in the replacement UI

The .NET engine still contains the broader desktop diagnostics, but the following controls and views are intentionally not part of this migration preview yet:

- Quick, Full, and Stress profile selection;
- saved-report detail, comparison, import/export, and label/tag editing;
- deeper DNS/ICMP/HTTP/QUIC and content/peak diagnostic launchers;
- LAN test controls;
- continuous monitoring, history, and alerts;
- command palette and tray integration.

Those features are being moved selectively from the old Avalonia application instead of carrying its XAML UI architecture into the Photino rebuild.

## Reports and privacy

Completed reports are saved under the operating system's local application-data directory. Report writes use a temporary file followed by an atomic replacement. Labels and tags are stored inside the schema 2.0 report itself when they are added.

Connection Check does not include local interface identifiers. The schema reader ignores unknown optional fields, and native sections that were not measured are represented as **Not measured** rather than failures.

## Run

Windows:

```powershell
.\NetworkDiagnosticsDesktop.exe
```

Linux:

```bash
chmod +x NetworkDiagnosticsDesktop
./NetworkDiagnosticsDesktop
```

macOS packages contain **Network Diagnostics.app**. Move it to Applications or open it from the extracted folder.

## macOS signing status

Pull-request and ordinary CI artifacts are bundled as proper macOS applications but are not signed or notarized. macOS may therefore require explicit approval through **System Settings → Privacy & Security**. Do not disable Gatekeeper globally.

Release signing and notarization require an Apple Developer ID certificate and notarization credentials configured as repository secrets. Once those credentials are available, the release workflow can produce a notarized distribution without the warning.
