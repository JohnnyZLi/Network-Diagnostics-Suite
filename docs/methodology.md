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

The download phase runs parallel same-origin streams of incompressible response data. The upload phase sends generated binary request bodies and the Worker reads and discards them. Results use successfully transferred payload bytes and elapsed wall time:

```text
Mbps = transferred bytes × 8 ÷ elapsed seconds ÷ 1,000,000
```

The graph samples recent transfer rate every 250 milliseconds. The displayed stability score is a bounded project metric:

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

## Windows deep probe

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

## Important limitations

- Results describe one device, browser, route, server edge, and moment in time.
- VPNs, content blockers, endpoint security, power-saving modes, CPU load, Wi-Fi contention, and browser scheduling can affect results.
- A short sample can miss intermittent faults. Repeat runs at different times and compare wired versus wireless paths.
- Throughput can be limited by the test edge or Worker platform as well as the access connection.
- A reachable common service does not prove all of that service is healthy; an unreachable target does not prove a global outage.
- Traceroute shows the reply path visible to ICMP TTL probes, not necessarily every forwarding decision or the return path.
