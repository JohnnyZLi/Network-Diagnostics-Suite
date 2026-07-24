# Privacy model

The project is designed around data minimization and honest boundaries. It has no application database and no code path that sends a completed result back to the project owner.

## Browser app

### What the application processes transiently

- Timing samples and generated upload/download payloads needed to run the selected test.
- Cloudflare edge, network organization, ASN, protocol, TLS, and IP-version context.
- Reachability timing for named common services in the opt-in Full and Stress profiles.

The Worker examines the connecting address only to classify the session as IPv4 or IPv6. It does not return that address to the browser.

### What the application does not have

- User accounts or authentication.
- Cookies, local-storage tracking, analytics, advertising, or telemetry.
- A results database or server-side result submission endpoint.
- Third-party scripts, fonts, or tracking pixels.
- Access to imported deep-probe report contents; the browser reads those files locally.

Worker observability is disabled in `wrangler.jsonc`. This prevents project-level Worker request logs from being enabled, but it does not mean traffic is invisible to infrastructure providers.

### Infrastructure boundary

Cloudflare necessarily receives the network traffic, source address, and ordinary request metadata required to route and protect the Worker. Cloudflare may process that data under its own terms, security controls, and retention practices.

When the user selects Full or Stress, the browser sends one reachability request to each named provider. Each provider necessarily sees the originating connection and may process it under its own privacy policy. Quick mode does not run this third-party battery.

## Native deep probe

The probe runs locally and writes JSON to a user-selected or timestamped local path. It has no telemetry or project-operated upload code. The optional LAN mode intentionally exchanges generated test bytes with a user-selected machine on the local network.

The default report includes:

- Test time, operating-system description, and CPU architecture.
- Interface name, description, type, link speed, MTU, and protocol support.
- ICMP statistics, public traceroute-hop addresses and reverse-DNS names, DNS timings, MTU estimate, and endpoint timing.

The default report omits or redacts:

- Public IP address as a dedicated field.
- MAC address, computer hostname, and Wi-Fi SSID.
- Interface IP addresses, gateway addresses, and local DNS addresses.
- Private, carrier-grade NAT, loopback, and link-local traceroute-hop addresses.

`--include-addresses` explicitly adds interface IP, gateway, DNS, and private-hop addresses. The report should then be treated as sensitive diagnostic material.


### Optional LAN server/client

`--lan-server` opens a TCP listener on all local interfaces on port 8765 by default and remains active until it is stopped. It accepts only the probe's small command protocol and generated throughput payloads; it does not read files or enumerate the connecting client. The server does not write results or contact the project infrastructure.

`--lan-target` connects to the host explicitly supplied by the user and records its target name, resolved address, port, transfer byte counts, rates, and response timings in the local JSON report. Those fields can reveal a private LAN address, so review the report before sharing it.

Run the LAN server only on a trusted network, permit the port only in the appropriate local firewall profile, and stop it when the test is complete.

## Exported results

Browser result exports contain timestamps, measured rates and timings, the network organization/ASN, serving edge, protocol information, and common-service results. Deep-probe reports can contain public network-path addresses and hardware descriptions even with default redaction.

Review a JSON file before posting it publicly. Export and sharing are user-controlled actions outside the application's no-retention boundary.

## Threat and abuse controls

- Test endpoints accept only their required HTTP methods.
- Bandwidth endpoints reject ordinary cross-site browser requests.
- Per-request download and upload sizes are capped.
- Static responses use a restrictive Content Security Policy and security headers.
- Production deployment should add a Cloudflare rate-limiting rule for the bandwidth endpoints to discourage automated abuse.

No client-side control can fully prevent a custom script from making direct requests to a public endpoint. Production limits are therefore part of the deployment model.
