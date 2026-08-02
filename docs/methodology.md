# Measurement methodology

This document defines what each result means, how it is calculated, and where it can be misleading. The guiding rule is simple: do not give a browser-level approximation an operating-system-level label, and do not fill unavailable native fields with guesses.

## Shared profiles and transfer plans

The browser, desktop application, and opt-in CLI Internet-transfer mode consume the same canonical contract in `contracts/test-profiles.v1.json`.

Profiles define idle-sample counts, transfer durations, byte ceilings, download sample counts, aggregate connection counts, service checks, comparison allocations, and Stress scaling stages. Transfer methods produce ordered stages:

- **Compare:** independent single and aggregate stages. Quick compares download and then runs aggregate upload. Full compares both directions. Stress runs 1, 2, 4, 8, and 10 connection download stages, followed by single and aggregate upload.
- **Single:** one connection in each direction using the profile's full configured duration and cap.
- **Aggregate:** the profile's parallel connection count in each direction using the full configured duration and cap.

Every method preserves the selected profile's maximum combined transfer ceiling. The displayed estimated time is the larger of the profile base estimate and the sum of idle, transfer, service-check, and fixed-overhead allowances rounded to five seconds.

## Browser measurements

### Idle latency and request loss

The browser repeatedly performs an uncached request to the same-origin `/api/ping` endpoint with a 1.5-second timeout. Successful high-resolution elapsed times produce minimum, maximum, mean, median, p95, and consecutive-sample jitter.

Timed-out or failed requests divided by attempts are reported as **request loss**. This is not raw packet loss: TCP can retransmit beneath the browser, and failures can come from DNS, TLS, HTTP, extensions, the browser, or the server.

### Browser download delivery

Automatic mode probes the deterministic incompressible R2 object on `speed.johnnyli.dev`, prefers direct range requests, and falls back to the Worker stream if validation fails. The explicit R2 and Worker paths remain available for comparison.

The browser records direct and fallback requests, cache evidence, response validation failures, request generations, warm-up bytes, started/completed/interrupted requests, replacements, and Resource Timing protocols where cross-origin timing permission is available.

The Worker comparison path composes deterministic Static Assets into a fixed-length logical stream. Invalid or truncated responses are recorded before a smaller generated fallback is used.

### Browser upload delivery

Generated binary request bodies are posted to `/api/upload`; the Worker reads and discards them. Quick and Full use 16 MiB requests, while Stress uses 32 MiB requests. Workers start 40 milliseconds apart and use small deterministic restart delays to reduce synchronized turnover. Reports retain request size and generation/lifecycle counts.

## Native Internet transfer

The native core sends first-party HTTP traffic to the same project origin. Its transport implementation is independent from browser fetch APIs but uses the same stage plan, payload endpoints, byte ceilings, timing formulas, and result vocabulary.

- `/api/ping` provides HTTP idle and loaded latency.
- `/speed/v4/stream` provides deterministic download bytes.
- `/api/upload` accepts generated upload bodies.
- `SocketsHttpHandler` limits and reuses connections per origin while each stage controls active worker count.

Native Internet transfer is always part of a desktop run and explicit opt-in for the CLI through `--internet-transfer`. A deep-only CLI run does not transfer speed-test payloads.

## Throughput calculation

Each sample uses successfully transferred payload bytes and elapsed wall time:

```text
Mbps = transferred bytes × 8 ÷ elapsed seconds ÷ 1,000,000
```

Progress is sampled every 250 milliseconds. Intervals shorter than 100 milliseconds are omitted from graph and interval-derived statistics to prevent a few terminal bytes from creating an impossible rate spike. Those bytes remain in the whole-phase average.

The steady-state value excludes up to the first second of each sample. Download stages with multiple samples report medians for whole-phase rate, steady rate, stability, and ramp ratio; bytes and durations remain sums. Peak rate is the maximum retained interval.

A sample is classified as:

- **cap-limited** when its byte ceiling ends it substantially before its duration;
- **still-ramping** when the latter half is more than 20% faster;
- **declining** when the latter half is below 80% of the earlier half;
- **unstable** when steady-state coefficient of variation exceeds the project threshold;
- **qualified** otherwise.

The stability score is:

```text
stability = clamp(100 - coefficient of variation × 100, 0, 100)
```

This is a project comparison metric, not an industry certification.

## Loaded latency

Separate latency requests continue during every transfer stage. Added delay is:

```text
added delay = loaded median - idle median
```

The UI presents the measured difference; browser result summaries clamp negative values where appropriate. The project grade is:

| Added median delay | Grade |
| ---: | :---: |
| 0–5 ms | A+ |
| >5–15 ms | A |
| >15–30 ms | B |
| >30–60 ms | C |
| >60–100 ms | D |
| >100 ms | F |

The grade is a compact queueing-delay interpretation, not a standards-body rating.

## Single versus aggregate interpretation

Single and aggregate stages run independently so they do not compete with one another. The comparison reports:

```text
single share = single steady Mbps ÷ aggregate steady Mbps × 100
parallel gain = (aggregate steady Mbps ÷ single steady Mbps - 1) × 100
```

A lower single share means parallel transfers used materially more of the measured path. It does not identify the cause. TCP congestion control, endpoint limits, routing, packet loss, radio conditions, security software, CPU load, and server behavior can all affect a single flow.

## Native operating-system diagnostics

### ICMP latency and packet loss

The deep probe sends ICMP Echo Requests with a 1.5-second timeout and 120 milliseconds between attempts. The result is real ICMP loss for the selected target and sample, but devices can deprioritize or block ICMP while forwarding ordinary traffic.

The default gateway is measured separately when the operating system exposes an IPv4 gateway.

### Traceroute

Three ICMP probes are sent per time-to-live value until the destination is reached or the configured hop limit is exhausted. A missing reply does not prove that forwarding failed; many routers suppress or rate-limit TTL-expired responses. The visible route is not necessarily the return path.

Private and link-local hop addresses are redacted unless local identifiers are explicitly enabled.

### DNS timing

The native probe sends direct UDP port 53 queries to available system resolvers plus Cloudflare, Google, and Quad9. Attempts, success counts, minimum, median, p95, maximum, and errors are recorded. Resolver behavior can differ for cached versus uncached names and UDP versus encrypted DNS.

### Path MTU

IPv4 ICMP probes with Don't Fragment set are used to estimate the largest successful payload and corresponding IPv4 MTU. Firewalls, tunnels, VPNs, or ICMP filtering can make the estimate unavailable or conservative.

### DNS, TCP, and TLS phases

Cloudflare, Google, Microsoft, GitHub, Apple, and Amazon are resolved and connected on TCP port 443. DNS lookup, TCP connect, and TLS authentication are timed separately. The negotiated TLS protocol and HTTP application protocol are recorded. No HTTP page content is fetched after the handshake.

### Interfaces

For active non-loopback, non-tunnel interfaces, the report records name, description, media type, reported link speed, IPv4 MTU, and IPv4/IPv6 support. Reported link speed is a negotiated or driver-reported interface rate, not measured Internet throughput.

## Platform Wi-Fi and routing details

The native core runs fixed read-only operating-system commands with an eight-second timeout:

- Windows: `netsh wlan show interfaces` and `route print -4`.
- macOS: Apple's `airport -I` tool and `netstat -rn -f inet`.
- Linux: `iw dev`, `iw dev <interface> link`, and `ip route show`.

Only documented text fields required for the report are parsed. Missing tools, changed formats, denied permissions, disconnected radios, and unsupported operating systems produce explicit `unavailable` or `not-connected` statuses.

Wi-Fi fields can include interface name, signal percentage, RSSI, channel, band, protocol, receive/transmit link rates, security, and SSID. Route entries can include destination, gateway, interface, metric, address family, and default-route status. Sensitive names and addresses remain redacted unless the user enables local identifiers.

Signal percentage derived from RSSI on macOS and Linux is a bounded presentation estimate, not a calibrated radio measurement:

```text
signal percent = clamp((RSSI dBm + 100) × 2, 0, 100)
```

## Isolated LAN throughput

The optional LAN mode uses a second user-controlled machine instead of a public endpoint. The server listens on TCP port 8765 by default. The client measures eight request/response samples, then generated download and upload traffic using the configured duration and parallel stream count.

This removes the ISP, public transit, and public test platform. It does not remove either endpoint's operating system, CPU, adapter, firewall, switch, access point, cabling, or TCP implementation. A preferably wired server with a link rate above the expected client speed is recommended.

## Report schemas

- **1.0 and 1.1:** historical native deep-probe formats.
- **1.2:** additive deep-probe format with optional Wi-Fi and routing details.
- **2.0:** combined envelope containing run metadata, transfer plan, native Internet transfer, deep diagnostics, and optional LAN result.

The browser importer accepts all four versions. Missing optional sections mean that scope was not run or was unavailable.

## Important limitations

- Every result describes particular devices, software, routes, endpoints, and a moment in time.
- VPNs, security software, CPU load, power saving, Wi-Fi contention, browser scheduling, and background traffic can affect results.
- A short sample can miss intermittent faults. Repeat at different times and compare wired, wireless, browser, desktop, and LAN-isolated runs.
- Public transfer results can be limited by the selected edge and route as well as the access connection.
- A service connection success does not prove the whole service is healthy; failure does not prove a global outage.
- Platform command output varies by operating-system release and localization. Unavailable fields are not inferred.
