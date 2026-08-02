# Network Diagnostics Desktop

This archive contains the graphical native application for Network Diagnostics Suite. It uses the same .NET core as the command-line deep probe and adds profile-driven first-party Internet transfer testing, operating-system diagnostics, local report history, and optional two-machine LAN isolation.

The application is self-contained; the .NET runtime does not need to be installed.

## Included test controls

- Quick, Full, and Stress profiles
- Compare, Single, and Aggregate transfer methods
- separate download and upload connection plans
- transfer-cap and estimated-time summaries
- single-versus-aggregate result comparison
- Stress download scaling across 1, 2, 4, 8, and 10 connections
- optional LAN target and local-address disclosure

Full and Stress can transfer substantial data. The application shows the current transfer ceiling and requires confirmation before those profiles run.

## Native diagnostics

The application also reports raw ICMP packet loss, default-gateway latency, traceroute, DNS resolver timing, path MTU, DNS/TCP/TLS connection phases, interfaces, platform Wi-Fi details, route-table details, and optional two-machine LAN throughput. Unsupported or permission-restricted platform fields are shown as unavailable rather than inferred.

SSID, local addresses, gateways, and resolver addresses remain hidden from saved reports unless the user explicitly enables local identifiers.

## Run

Windows:

```powershell
.\NetworkDiagnosticsDesktop.exe
```

macOS or Linux:

```bash
chmod +x NetworkDiagnosticsDesktop
./NetworkDiagnosticsDesktop
```

Reports are saved under the operating system's local application-data directory. The application can open that folder or copy the latest report into the user's Documents folder.

## macOS notice

The initial CI packages are not Apple-signed or notarized. Review the source and checksum, build locally, or explicitly approve the application in **System Settings → Privacy & Security** if you trust it. Do not disable Gatekeeper globally.
