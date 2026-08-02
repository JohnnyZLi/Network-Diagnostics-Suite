# Privacy model

The project is designed around data minimization and explicit boundaries. It has no application database and no code path that sends a completed result back to the project owner.

## Browser application

### Transient processing

The browser processes timing samples and generated transfer payloads, Cloudflare edge/network/protocol context, and—on Full and Stress—reachability timing for named services. The Worker examines the connecting address only to identify IPv4 or IPv6 and does not return the address itself.

The browser application has no accounts, authentication, cookies, tracking local storage, analytics, advertising, telemetry, results database, third-party scripts, remote fonts, or result-submission endpoint. Imported native JSON is read with the File API and remains in the current tab.

Recent browser reports and remembered high-data confirmations are stored only in that browser's local storage. Clearing site data removes them.

### Infrastructure boundary

Cloudflare necessarily receives source addresses, traffic, and ordinary request metadata required to route and protect the application. It may process that data under its own terms and retention practices.

The main download path uses project-operated deterministic assets through Cloudflare Static Assets or the project R2 custom domain. Upload, ping, metadata, and fallback download requests use project Workers. The suite does not submit results to M-Lab or another public measurement dataset.

Full and Stress service checks contact Cloudflare, Google, Microsoft, GitHub, Apple, and Amazon. Each provider necessarily sees the originating connection. Quick does not run the service battery.

Worker observability is disabled in `wrangler.jsonc`. This prevents project-level Worker request logs from being enabled but does not make infrastructure traffic invisible.

## Native desktop application

The desktop application runs locally, calls project-operated first-party transfer endpoints, performs operating-system diagnostics, and writes completed schema 2.0 JSON reports to the user's local application-data directory. It has no telemetry or automatic result upload.

The latest 12 report files are listed from the local reports directory. The application does not copy or synchronize those files elsewhere. **Export latest** creates a user-requested copy in the user's Documents directory.

Remembered Full/Stress confirmations are stored in a small local settings file containing only the approved profile name and transfer ceiling. No measurement data is stored in that settings file.

### First-party transfer boundary

Every desktop run intentionally exchanges generated payloads with the project-operated ping, download, and upload endpoints. The UI displays the selected transfer ceiling and requires confirmation for Full and Stress. Cloudflare receives the native connection and traffic just as it receives browser traffic.

## Command-line deep probe

The default CLI run performs deep diagnostics locally and writes schema 1.2 JSON to the selected or timestamped path. It does not transfer speed-test payloads to project infrastructure unless `--internet-transfer` is explicitly supplied. That option emits the combined schema 2.0 report and contacts the same first-party transfer endpoints as the desktop application.

## Native report contents

Default native reports can include:

- test time, operating-system description, and CPU architecture;
- interface name, description, type, link speed, MTU, and protocol support;
- ICMP statistics and the selected public target;
- public traceroute-hop addresses and reverse-DNS names;
- DNS timing, path-MTU status, and service endpoint timing;
- Wi-Fi signal, RSSI, channel, band, protocol, and reported link rates when exposed;
- route destinations, interface names, metrics, and default-route status;
- first-party transfer measurements when that scope runs.

Default native reports omit or redact:

- public IP address as a dedicated field;
- MAC address and computer hostname;
- Wi-Fi SSID;
- interface addresses, gateway addresses, and local resolver addresses;
- private, carrier-grade NAT, loopback, and link-local traceroute-hop addresses;
- route gateways and other sensitive route addresses.

Enabling local identifiers in the desktop application or using `--include-addresses` adds available SSID, interface, gateway, resolver, private-hop, and route-address fields. Such reports should be treated as sensitive diagnostic material.

Platform Wi-Fi and route providers invoke only fixed read-only commands with fixed argument lists. User input is not passed to a shell. Missing tools, permission failures, localized output, and unsupported fields are recorded as unavailable.

## Optional LAN server/client

`--lan-server` opens a TCP listener on all local interfaces on port 8765 by default and remains active until stopped. It accepts only the probe's small command protocol and generated throughput payloads. It does not read files, enumerate clients, write reports, or contact project infrastructure.

A desktop or CLI LAN client connects only to the target explicitly entered by the user. The report records the target, resolved address, port, transfer byte counts, rates, and response timing. These fields can reveal private network addressing.

Run the LAN server only on a trusted network, permit the port only in the appropriate local firewall profile, and stop it when testing is complete.

## Exported and shared results

Browser exports can contain timestamps, measured rates, loaded latency, network organization/ASN, serving edge, protocol information, and service results. Native reports can additionally contain public path addresses, hardware/interface descriptions, Wi-Fi characteristics, and routing context even under default redaction.

Review JSON before posting it publicly. Exporting and sharing are user-controlled actions outside the application's no-retention boundary.

## Threat and abuse controls

- Test endpoints accept only required HTTP methods.
- Bandwidth endpoints reject ordinary cross-site browser requests.
- Per-request and per-profile transfer sizes are capped.
- Static payload responses use restrictive headers.
- Native platform commands use fixed executable names and arguments without shell interpolation.
- The LAN protocol accepts only bounded ping, download, and upload commands.
- Production should use Cloudflare rate limits for public bandwidth endpoints.

No client-side control can prevent a custom script from making direct requests to a public endpoint. Infrastructure limits remain part of the deployment model.
