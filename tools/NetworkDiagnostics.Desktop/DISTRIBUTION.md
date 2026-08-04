# Network Diagnostics Desktop

This archive contains the native graphical application for Network Diagnostics Suite. It shares its diagnostic engine and schema 2.0 report contract with the command-line deep probe.

The application is self-contained; the .NET runtime does not need to be installed.

## Diagnostic profiles

- **Connection Check** — lightweight first-party reachability, latency, request-loss, download, and upload measurement.
- **Quick** — broader single-versus-aggregate throughput and loaded-responsiveness evidence.
- **Full** — Internet measurements plus operating-system local-network, route, resolver, service, Wi-Fi, and path evidence.
- **Stress** — sustained load and progressive connection scaling plus the Full diagnostic evidence set.

Full and Stress can transfer substantial data. The application shows the current transfer ceiling and requires confirmation before those profiles run. Remembered approval applies only to the current ceiling; a future increase asks again.

## Advanced native evidence

Native runs also collect low-data technical evidence that does not create a second speed-test payload:

- independent IPv4 and IPv6 DNS, ICMP, TCP, TLS, and HTTP checks;
- loaded-latency sampling at the default gateway, first responsive public hop, and measurement endpoint for Full and Stress;
- captive-portal, system-proxy, tunnel-interface, local-route, public-network, ASN, edge, and IP-version change detection;
- diagnostic-process CPU, memory pressure, interface error/discard counters, and system TCP retransmission deltas.

Unavailable fields remain **Not measured**. ICMP path localization and system-wide counters are supporting evidence rather than proof of one device, provider, or flow.

## Reports, comparison, and privacy

Completed reports are saved under the operating system's local application-data directory. History can reopen saved reports through the same renderer used for live results. Schema 2.0 JSON reports can also be imported or exported.

History can select a comparison baseline and compare another saved report. Equivalent trends require the same profile, transfer method, endpoint, interface, and transfer ceiling. Saved reports can be labeled and tagged for controlled comparisons such as Wi-Fi versus Ethernet or before versus after a router restart.

Report and annotation writes use a temporary file followed by an atomic replacement. Labels and tags stay inside the local JSON report and are included only when that report is exported.

The reader ignores unknown optional fields. Native sections absent from a website report are treated as **Not measured**, not failures.

SSID, local addresses, gateways, resolver addresses, interface names used by host counters, and proxy addresses remain hidden from saved reports unless the user explicitly enables local identifiers in Settings. Public ASN, network, edge, protocol, and IP-version evidence remains visible because it describes the public measurement path. A custom measurement endpoint can be configured under Advanced settings.

See `docs/advanced-diagnostics.md` in the source repository for measurement boundaries and limitations.

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
