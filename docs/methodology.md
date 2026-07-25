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

The normal download path uses a deterministic, incompressible 256 MiB object in Cloudflare R2 through the bucket custom domain `speed.johnnyli.dev`. A browser probe checks the object size and required CORS contract before the timed condition begins. Automatic mode prefers this direct path and falls back to the Worker stream if the probe fails. The explicit Worker path remains available under advanced controls for comparison.

Parallel browser workers request distinct 192 MiB byte ranges from the same R2 object. The ranges are large enough to remain open for most connections, while distinct offsets prevent all parallel requests from being identical. Cloudflare edge caching is forced by a Cache Rule with a one-day Edge TTL, while Browser TTL bypass and the object's `Cache-Control: no-store, no-transform` prevent the browser cache from satisfying a measured transfer.

Quick, Full, and Stress start six, eight, and ten parallel download requests respectively. Each profile divides its configured download duration and byte ceiling across three consecutive samples. The displayed whole-sample rate, steady-state rate, and stability are the medians of those three samples. Total transferred bytes and total download duration remain sums across all samples. Peak rate is the maximum observed interval across the set.

A median of three short samples is less sensitive to one unusually fast or slow interval than a single run of the same total duration. It does not eliminate route, radio, browser, or congestion variability, and the exported report retains every sample's rate, stability, qualification, duration, and byte count.

If an R2 range fails validation or ends early, that worker uses the Worker stream for the remainder of that request and the report preserves the fallback reason. The Worker comparison path uses four deterministic, incompressible 24 MiB Static Assets. The Worker retrieves missing segments through the static-assets binding, stores full cacheable copies in its local `caches.default`, and pipes them sequentially into a Cloudflare `FixedLengthStream` to create a 96 MiB logical response. A truncated or invalid Worker response is recorded before using the smaller dynamically generated fallback.

The report records cache status, cache age, direct R2 requests, Worker stream requests, dynamic fallbacks, response rejections, started/completed/interrupted requests, replacement requests, and bytes by request generation. `Timing-Allow-Origin` on the R2 response permits Resource Timing to expose `nextHopProtocol`; without it, the transfer still works but the cross-origin protocol normally remains unavailable.

The download byte ceilings are 600 MB for Quick, 900 MB for Full, and 3 GB for Stress. They are safety ceilings rather than targets and are divided across the three samples. Reaching a sample's portion of the ceiling aborts that sample and marks it cap-limited when it ends substantially before its configured duration.

The upload phase sends generated binary request bodies and the Worker reads and discards them. Upload workers use 16 MiB requests for Quick and Full and 32 MiB requests for Stress, begin 40 milliseconds apart, and apply a small deterministic delay before replacement requests. This reduces synchronized request turnover without hiding request boundaries. The report records upload request generations and completed, interrupted, and replacement requests. Quick and Full use 128 MB and 256 MB upload safety ceilings respectively; Stress uses 512 MB.

Each individual throughput sample uses successfully transferred payload bytes and elapsed wall time:

```text
Mbps = transferred bytes × 8 ÷ elapsed seconds ÷ 1,000,000
```

The graph samples recent transfer rate every 250 milliseconds. Each sample's steady-state value excludes up to its first second, while the report also retains the complete sample average. A terminal interval shorter than 100 milliseconds is omitted from the graph and interval statistics because dividing a few final progress bytes by only a few milliseconds can create a false rate spike; those bytes remain included in the whole-phase average. The median headline is computed only after all three download samples finish.

A sample is marked **cap-limited** when its byte ceiling ends it substantially before the configured duration, **still ramping** when the latter half is more than 20% faster than the earlier measured half, **declining** when the latter half is below 80% of the earlier measured half, and **unstable** when the steady-state coefficient of variation is high. The aggregate download report carries the most cautionary qualification among its three samples.

The displayed stability score is a bounded project metric:

```text
stability = clamp(100 - coefficient of variation × 100, 0, 100)
```

It is useful for comparing runs but is not an industry certification.

### Loaded latency and bufferbloat signal

Latency requests continue throughout all three download samples and again while upload traffic saturates the connection. Added delay is:

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

Full and Stress profiles make one cache-bypassed, credential-free, no-referrer browser request to each of these targets: Cloudflare, Google, Microsoft, GitHub, Apple, and Amazon. The Cloudflare entry uses the deployed first-party `/api/ping` Worker; the other five are external opaque requests.

Because the external requests use `no-cors`, the browser intentionally hides response status and content. The app can only report whether the fetch completed before the timeout and how long the browser waited. A failure does not prove that the service is down.

### Edge context

The Worker returns the servicing edge code, network organization and ASN, HTTP protocol, TLS version, and whether the connection reached Cloudflare over IPv4 or IPv6. It uses the connecting address only to infer the IP version and never returns the address itself. The R2 download may terminate at the same or a different Cloudflare edge; Resource Timing reports its protocol when the browser and response headers permit it.

## Native deep probe
