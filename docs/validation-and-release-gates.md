# Validation and release gates

Features that depend on infrastructure, hardware, operating-system trust, or a third party are not considered complete merely because an interface or configuration hook exists. This document separates implemented product behavior from evidence still required before a public claim.

## Independent measurement provider

The engine and report contracts support multiple candidate endpoints today. Production remains on the first-party Cloudflare endpoint until a second provider meets all of these gates:

- independently operated compute and network path;
- HTTPS, CORS, upload, ping, metadata, and deterministic download semantics compatible with the measurement contract;
- explicit operator authorization, retention statement, abuse controls, and capacity budget;
- automated payload-integrity and byte-accounting tests;
- at least 30 days of availability and latency monitoring;
- browser and native comparison runs across multiple regions without a systematic endpoint bottleneck.

Only after those gates pass may the UI describe a result as cross-provider. Until then, reports say exactly which single provider was measured.

## Multi-gigabit throughput claims

The engine already records cap-limited, ramping, declining, and unstable samples, but no public multi-gigabit accuracy claim is allowed until validation includes:

- wired 2.5, 5, and 10 GbE clients with documented CPU and adapter details;
- a local reference tool and a public reference service run on the same path and time window;
- browser and native runs with protocol, endpoint, cache, byte, duration, and thermal evidence retained;
- repeated samples across Apple Silicon, Windows x64, and Linux x64;
- an error envelope and known saturation points published in the methodology.

## macOS signing and notarization

Self-contained macOS builds are locally runnable but are not a finished consumer distribution. A signed release requires:

- an Apple Developer ID Application identity controlled by the project owner;
- hardened-runtime signing of the final application bundle;
- notarization and stapling of the exact distributed archive;
- verification with `codesign`, `spctl`, and Gatekeeper on a clean Mac;
- checksum provenance tying the verified archive to the release.

No workflow should simulate these steps with an ad-hoc identity or publish an unsigned artifact as notarized.

## Visual approval and pull request state

Desktop smoke tests validate startup and named controls only. Approval requires screenshots of the actual rendered osx-arm64 application in light and dark appearance, at wide and compact widths, with setup, running, findings, detailed results, and report history states reviewed for clipping and keyboard focus. PR #101 remains a draft until that approval is explicit.
