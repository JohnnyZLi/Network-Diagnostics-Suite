# Measurement methodology

This document defines what each result means, how it is calculated, and where it can be misleading. The guiding rule is simple: do not give a browser-level approximation an operating-system-level label.

## Browser measurements

### Idle latency

The app repeatedly performs an uncached request to the same-origin `/api/ping` endpoint and records elapsed high-resolution browser time. Each request has a 1.5-second timeout.

The successful round-trip times produce:

- **Minimum and maximum:** lowest and highest successful sample.
- **Mean:** arithmetic average of successful samples.
- **Median:** linearly interpolated 50th percentile.
- **P95:** linearly interpolated 95th percentile.
- **Jitter:** mean absolute difference between consecutive successful samples.
- **Request loss:** timed-out or failed requests divided by attempted requests.

Request loss is not raw packet loss. TCP can retransmit packets beneath the browser and a failed request can be caused by DNS, TLS, HTTP, the browser, an extension, or the server.

### Throughput

The download phase uses four deterministic, incompressible 24 MiB files deployed with the application as Cloudflare Workers Static Assets. Before measurement begins, the client calls a same-origin warm-up endpoint. The Worker retrieves any missing segment through the static-assets binding and stores a full cacheable copy in `caches.default`, which is local to the Cloudflare data center handling the request. This server-side warm-up does not transfer the 96 MiB payload across the user's connection.

Each measured download request receives one 96 MiB logical response assembled as a streaming concatenation of the four cached segments. The browser response is marked `Cache-Control: no-store`, so the browser's local HTTP cache cannot satisfy a measured transfer. Quick, Full, and Stress start six, eight, and ten requests respectively. A request normally remains open until the timed phase ends. If a very fast connection finishes all 96 MiB early, that worker may open a replacement response; the report records started, completed, interrupted, and replacement requests plus bytes by request generation.

The report prioritizes the Worker's deterministic `X-NDS-Cache-Status` and `X-NDS-Cache-Age` headers, while retaining `CF-Cache-Status` and `Age` as fallbacks. It also records Worker-stream fallbacks and the browser Resource Timing `nextHopProtocol` values associated with the speed path. A warm-up `MISS` followed by measured `HIT` responses means the selected data center stored the four segments before the timed phase. If the local Cache API is unavailable, the warm-up or stream is labeled `BYPASS`. If the logical stream cannot be used, the client falls back to the smaller dynamically generated Worker endpoint.

The download byte ceilings are 600 MB for Quick, 900 MB for Full, and 3 GB for Stress. They are safety ceilings rather than targets. Reaching a ceiling aborts the active phase and marks the sample cap-limited when it ends substantially before the configured duration.

The upload phase sends generated binary request bodies and the Worker reads and discards them. The whole-phase value uses successfully transferred payload bytes and elapsed wall time:

```text
Mbps = transferred bytes × 8 ÷ elapsed seconds ÷ 1,000,000
```

The graph samples recent transfer rate every 250 milliseconds. The headline steady-state value excludes up to the first second of the measured phase, while the report also retains the whole-phase value. The pre-measurement edge warm-up and the steady-state calculation make startup effects less dominant without hiding the complete measured-phase average.

The report marks a direction as **cap-limited** when the byte ceiling ends it substantially before the configured duration, **still ramping** when the latter half is more than 20% faster than the earlier measured half, **declining** when the latter half is below 80% of the earlier measured half, and **unstable** when the steady-state coefficient of variation is high. These labels describe sample quality; they cannot prove whether a limit came from the access line, route, browser, Cloudflare edge, or test implementation.

The displayed stability score is a bounded project metric:

```text
stability = clamp(100 - coefficient of variation × 100, 0, 100)
```

It is useful for comparing runs but is not an industry certification.

### Loaded latency and bufferbloat signal

Latency requests continue while download traffic saturates the connection and again while upload traffic saturates it. Added delay is:

```text
added delay = max(0, loaded median - idle median)
```

The grade uses project-specific thresholds:

| Added median delay | Grade |
| ---: | :---: |
| 0–5 ms | A+ |
| >5–15 ms | A |
| >15–30 ms | B |
| >30–60 ms | C |
| >60–100 ms | D |
| >100 ms | F |

The grade is a compact interpretation of queueing delay, not a standards-body rating.

### Common-service battery

Full and Stress profiles make one cache-bypassed, credential-free, no-referrer browser request to each of these targets: Cloudflare, Google, Microsoft, GitHub, Apple, and Amazon.

Because the requests use `no-cors`, the browser intentionally hides response status and content. The app can only report whether the fetch completed before the timeout and how long the browser waited. A failure does not prove that the service is down.

### Edge context

The Worker returns the servicing edge code, network organization and ASN, HTTP protocol, TLS version, and whether the connection reached Cloudflare over IPv4 or IPv6. It uses the connecting address only to infer the IP version and never returns the address itself.

## Native deep probe

Windows 11, macOS, and Linux packages run the same .NET measurement engine and emit the same versioned JSON schema. The packages differ only by operating system and CPU runtime. CI runs the unit suite and launches each binary on its target platform before publishing it.

### Isolated LAN throughput

The optional native LAN mode uses a user-controlled second machine instead of a public endpoint. The server listens on TCP port 8765 by default. The client opens parallel TCP streams and measures:

- Eight short TCP request/response samples to the LAN server.
- Download bytes received during the configured duration.
- Upload bytes written during the configured duration.

The default transfer duration is eight seconds per direction with four parallel streams. Throughput uses the same decimal megabit formula as the browser result.

This removes the Internet service provider, public transit/peering, and remote test platform from the path. It does not remove the client and server operating systems, their CPUs, network adapters, local firewall, switch, access point, cabling, or TCP implementation. A weak server machine can therefore still cap the LAN result. A wired server with a link rate above the expected client speed is preferred.

### ICMP latency and packet loss

The probe sends 20 ICMP Echo Requests to the selected target by default, with a 1.5-second timeout and 120 milliseconds between attempts. The same distribution statistics used by the browser are calculated from replies. This is real ICMP loss for this specific sample and target, though a device may deprioritize or block ICMP while forwarding other traffic normally.

If a default gateway is available, the probe also sends up to 12 pings to it. Comparing gateway loss/latency with Internet loss/latency helps separate a local-link problem from an upstream problem.

### Traceroute

The probe sends three ICMP probes for each time-to-live value from 1 through 30 by default. Each probe waits up to 1.2 seconds. Reverse DNS lookup is limited to 600 milliseconds per responding hop.

An unanswered hop is not automatically broken: routers often rate-limit or ignore expired-TTL responses while still forwarding traffic. Private, carrier-grade NAT, loopback, and link-local addresses are hidden unless `--include-addresses` is supplied.

### DNS resolver timing

The probe sends five direct UDP port 53 A-record queries for `example.com` to up to two active system resolvers and to Cloudflare (`1.1.1.1`), Google (`8.8.8.8`), and Quad9 (`9.9.9.9`). It validates the transaction ID, success response code, and nonzero answer count.

Some networks intentionally block third-party resolvers. A failed public-resolver test can therefore describe policy rather than an outage.

### Path MTU estimate

For IPv4 targets, the probe performs a binary search using ICMP Echo Requests with the Don't Fragment flag. It searches payload sizes 512 through 1472 bytes and adds 28 bytes for the IPv4 and ICMP headers. This estimate depends on ICMP behavior and is not available for IPv6 in the current version.

### DNS, TCP, and TLS phases

For six common HTTPS endpoints, the probe separately times:

1. Hostname resolution.
2. TCP connection to port 443.
3. TLS handshake and negotiated application protocol.

It does not issue an HTTP content request after the handshake. The values help distinguish resolver delay, transport connection delay, and TLS negotiation delay.

### Interface facts

For active non-loopback, non-tunnel interfaces, the report includes interface name and description, media type, reported link speed, IPv4 MTU, and IPv4/IPv6 support. Link speed is the adapter's negotiated or reported link rate, not measured Internet throughput.

Operating systems expose interface, resolver, and default-gateway metadata differently. The probe uses .NET's native network APIs first and supplements resolver discovery from `/etc/resolv.conf` on macOS and Linux when necessary. Unsupported or unavailable fields remain empty rather than being guessed.

## Important limitations

- Browser results describe one device, browser, route, Cloudflare edge, and moment in time. LAN results describe two user-controlled devices and the local path between them.
- VPNs, content blockers, endpoint security, power-saving modes, CPU load, Wi-Fi contention, and browser scheduling can affect results.
- A short sample can miss intermittent faults. Repeat runs at different times and compare wired versus wireless paths.
- Worker-local cache delivery removes repeated static-asset retrieval after a successful warm-up, but browser throughput can still be limited by the selected Cloudflare edge, its transport behavior, and its route to the ISP. No first-party Internet test can mathematically subtract its own network path.
- The Cache API is local to the data center handling the request and does not replicate stored segments globally. A test routed to a different Cloudflare data center needs its own warm-up.
- The Worker concatenates cached segments without assembling the 96 MiB response in memory, but it still participates in the response path and can affect delivery.
- Several same-origin HTTP requests may share one HTTP/2 or HTTP/3 connection and congestion controller. The browser test reports aggregate application throughput but does not claim a specific number of independent transport connections.
- Cache headers and Resource Timing expose useful evidence, but browser and CDN internals can still change independently of this application.
- A reachable common service does not prove all of that service is healthy; an unreachable target does not prove a global outage.
- Traceroute shows the reply path visible to ICMP TTL probes, not necessarily every forwarding decision or the return path.
- Host firewalls, container policies, and operating-system ICMP permissions can prevent ping, traceroute, or path-MTU replies even while ordinary web traffic works. A firewall can also block the optional LAN server port.
