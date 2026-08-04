# Advanced fault localization

This document defines the additional native evidence collected after the initial desktop release. The evidence remains additive in schema 2.0 and follows the same rule as the rest of the suite: unavailable measurements remain unavailable rather than being inferred.

## Local report comparison

Desktop History can select a saved report as a baseline and compare another report against it. Comparisons include available changes in:

- download and upload throughput;
- idle and loaded latency;
- request loss;
- default-gateway and DNS timing;
- IPv4 and IPv6 endpoint-response timing;
- Wi-Fi signal;
- diagnostic-process CPU usage; and
- operating-system TCP retransmission share.

A comparison is considered equivalent only when profile, transfer method, selected endpoint, selected interface, and transfer ceiling match. Non-equivalent reports can still be inspected together, but the application lists the mismatched conditions instead of presenting the result as a controlled experiment.

The trend summary uses up to the ten newest equivalent local reports. Reports can carry a local label and up to ten tags, such as `Wi-Fi`, `Ethernet`, `VPN off`, `before router restart`, or `evening`. Labels and tags are stored in the JSON report and remain local unless that report is exported.

## Loaded-latency localization

Full and Stress sample three path boundaries while the transfer stages are active:

1. the default gateway, when available;
2. the first responsive public IPv4 hop, when ICMP traceroute evidence is available; and
3. the selected measurement endpoint.

Samples are divided into idle, download, and upload phases. A loaded increase greater than 15 milliseconds at the earliest measured boundary produces one of these interpretations:

- **local network:** the default gateway slowed under load;
- **access link:** the gateway remained comparatively stable while the first public hop slowed; or
- **upstream path:** earlier boundaries remained comparatively stable while the endpoint slowed.

This is fault localization evidence, not proof of ownership. ICMP can be filtered or deprioritized, the visible route can differ from the return route, and the first responsive public hop is not guaranteed to be the subscriber-access device.

Private gateway addresses remain redacted unless local identifiers are enabled. Public hop and endpoint addresses are retained because they are already public path evidence.

## IPv4 and IPv6 comparison

Every native profile performs a low-data address-family probe against the selected endpoint. The probe records:

- DNS resolution duration and returned A/AAAA counts;
- ICMP latency when replies are available;
- direct TCP connection time;
- TLS handshake time and negotiated protocol for HTTPS endpoints;
- first-response HTTP timing and status; and
- the address family that completed the endpoint request sooner.

The probe does not run a second speed test for each address family, so it does not add unaccounted throughput payload to the profile data ceiling. A family can be reported as advertised but unusable when DNS returns an address and the other family completes while its TCP, TLS, or HTTP path does not.

NAT64 is reported only when the resolved IPv6 address uses the well-known `64:ff9b::/96` prefix and no IPv4 address is returned. Other provider-specific NAT64 prefixes are not inferred.

## Network-change and interception evidence

The native engine takes local and public network snapshots before and after a run. It records changes in:

- active interface identity;
- default gateway;
- active address families;
- effective system proxy;
- detected tunnel-style interfaces;
- public network name and ASN;
- observed public IP version; and
- selected endpoint edge location.

A changed run is marked as unsuitable for a stable baseline. The report does not claim that a detected tunnel interface is carrying the test traffic; it records the presence of tunnel-style interfaces and separately records the selected source interface and public-network evidence.

Captive-portal detection sends a no-redirect first-party ping request. A redirect or HTML response is labeled as suspected interception. Failure to detect a portal does not prove that no authentication gateway or transparent proxy exists.

Local interface names, gateway addresses, and proxy addresses are redacted by default. Public network, ASN, protocol, edge, and IP-version metadata remain visible because they describe the public measurement path.

## Host and interface evidence

The native engine records deltas while the diagnostic runs:

- diagnostic-process CPU usage normalized across logical processors;
- peak process working set;
- managed-memory usage;
- runtime-reported system memory load and high-load threshold;
- per-interface bytes, errors, and discards; and
- operating-system TCP segments sent and retransmitted.

Interface names and IDs are replaced with generic labels unless local identifiers are enabled.

TCP counters are system-wide. They can include browsers, synchronization clients, games, updates, and other background applications. A retransmission finding therefore requires at least 100 sent segments and a retransmission share of at least one percent, and it remains medium-confidence path or host evidence rather than proof that the diagnostic flow retransmitted.

High diagnostic-process CPU or increasing interface error/discard counters can indicate that the client or adapter limited the measurement. Runtime memory load near the high-load threshold is reported separately because memory pressure can disturb application scheduling without proving a network fault.

## Persistence and failure behavior

Desktop report and annotation writes use a temporary file followed by an atomic replacement. Abandoned temporary files older than one hour are removed when History is opened. Unreadable JSON reports are left untouched so they can be inspected or recovered manually.

All advanced fields are optional. Older schema 2.0 readers ignore them, and older reports continue to open with the relevant advanced sections shown as not measured.