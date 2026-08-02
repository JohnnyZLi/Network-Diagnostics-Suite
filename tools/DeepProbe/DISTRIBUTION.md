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

The default command retains the original deep-only schema 1.1 report. Run `NetworkDeepProbe --help` (or `NetworkDeepProbe.exe --help`) for all controls.

## First-party Internet transfer

Add the same Quick, Full, Stress and Compare, Single, Aggregate concepts used by the browser application:

```bash
./NetworkDeepProbe --internet-transfer --profile quick --transfer-method compare
```

This mode contacts the project-operated `network.johnnyli.dev` ping, download, and upload endpoints and emits a combined schema 2.0 report. The report includes separate single and aggregate measurements where selected, loaded latency, transfer caps, estimated transfer time, and the Stress 1, 2, 4, 8, and 10 connection sequence.

Full and Stress can transfer substantial data. Review the transfer plan printed before the run and avoid metered or cellular connections unless the data use is acceptable.

## Local-link throughput isolation

Use two machines on the same trusted local network. On a preferably wired machine:

```bash
./NetworkDeepProbe --lan-server
```

On the device being tested, replace the address with one printed by the server:

```bash
./NetworkDeepProbe --lan-target 192.168.1.10
```

The client writes a normal JSON report containing the LAN result and the regular Internet diagnostics. TCP port 8765 is used by default. Permit it through the server firewall only on trusted networks, and stop the server with Ctrl+C after the test.

## macOS security notice

The current macOS binaries are open-source CI builds but are not Apple-signed or notarized. macOS may block the first launch. Review the source and checksum, build from source, or explicitly approve the binary in **System Settings → Privacy & Security** if you trust it. Do not disable Gatekeeper globally.

## Privacy

The deep-only probe contains no telemetry or project-operated upload code. `--internet-transfer` intentionally exchanges generated payloads with the project-operated first-party test endpoints. LAN client mode intentionally sends generated test bytes to the user-selected LAN server, and LAN server mode receives or transmits generated test bytes only while it is running.

Reports omit the public IP, MAC address, hostname, SSID, local interface addresses, gateway addresses, local resolver addresses, and private traceroute hops by default. `--include-addresses` deliberately includes the local-address fields, so review that report before sharing it.

Public traceroute hops and the operating-system/interface descriptions are diagnostic output and can still reveal network or hardware context.

Source and full methodology: `https://github.com/JohnnyZLi/Network-Diagnostics-Suite`
