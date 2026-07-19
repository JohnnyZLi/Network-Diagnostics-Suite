# Network Deep Probe

This archive contains the native companion for Network Diagnostics Suite. It runs locally, performs operating-system-level network tests, and writes a JSON report that can be opened at `https://network.johnnyli.dev`.

The binary is self-contained; the .NET runtime does not need to be installed.

## Run

Windows 11, from PowerShell or Windows Terminal:

```powershell
.\NetworkDeepProbe.exe
```

macOS or Linux, from Terminal:

```bash
chmod +x NetworkDeepProbe
./NetworkDeepProbe
```

Run `NetworkDeepProbe --help` (or `NetworkDeepProbe.exe --help`) for target, output, ping-count, hop-limit, and address-inclusion options.

## macOS security notice

The current macOS binaries are open-source CI builds but are not Apple-signed or notarized. macOS may block the first launch. Review the source and checksum, build from source, or explicitly approve the binary in **System Settings → Privacy & Security** if you trust it. Do not disable Gatekeeper globally.

## Privacy

The probe contains no telemetry or upload code. It omits the public IP, MAC address, hostname, SSID, local interface addresses, gateway addresses, local resolver addresses, and private traceroute hops by default. `--include-addresses` deliberately includes the local-address fields, so review that report before sharing it.

Public traceroute hops and the operating-system/interface descriptions are diagnostic output and can still reveal network or hardware context.

Source and full methodology: `https://github.com/JohnnyZLi/Network-Diagnostics-Suite`
