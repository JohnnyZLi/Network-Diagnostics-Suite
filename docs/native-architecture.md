# Native application architecture

## Current boundary

The shipped native system has one .NET measurement and interpretation core with two hosts: a cross-platform Avalonia desktop application and a backward-compatible command-line probe. Both native hosts own Internet transfer, operating-system diagnostics, optional two-machine LAN throughput, endpoint preflight, and combined report generation. The React browser uses a separate browser transport implementation but consumes the same profile and interpretation contracts.

## Target projects

```text
tools/NetworkDiagnostics.Core/       Shared diagnostics, contracts, planning, and report models
tools/DeepProbe/                     Backward-compatible command-line host
tools/NetworkDiagnostics.Desktop/    Cross-platform graphical host
tools/DeepProbe.Tests/               Core, CLI, contract, and integration tests
```

The first extraction intentionally keeps existing namespaces and source locations stable. The core project links the current diagnostics and report-model files so that moving code does not alter runtime behavior or make review harder.

## Shared product contract

`contracts/test-profiles.v1.json` is the canonical profile input for both TypeScript and .NET implementations. `contracts/diagnostic-rules.v1.json` is the corresponding deterministic interpretation input. Together they record:

- Connection Check (`quick`), Full, and Stress limits
- Compare, Single, and Aggregate method identifiers
- transfer durations and byte ceilings
- independent download and upload connection counts
- comparison allocations
- the Stress 1, 2, 4, 8, and 10 connection sequence
- evidence thresholds that must produce the same category and severity from the same fixture

The browser configuration remains executable TypeScript, but tests must reject drift from this contract. The .NET core embeds and parses the same file.

## Report evolution

Native schema 1.0 and 1.1 remain supported for imports. New combined runs will use the `2.0` envelope described by `contracts/report-v2.schema.json`.

The envelope separates measurement context and three scopes:

0. engine capabilities, endpoint selection, and finding evidence

1. Internet transfer measurements
2. operating-system deep diagnostics
3. optional two-machine local-link measurements

A missing section means that scope did not run or was unavailable. It must not be replaced with guessed data.

## Implemented evolution

1. Reusable .NET core with a compatible CLI host.
2. Shared transfer-plan builder and cross-language fixtures.
3. First-party native Internet download/upload stages and loaded latency.
4. Additive schema 2.0 envelope while retaining 1.x imports.
5. Cross-platform Avalonia host with local report history and export.
6. Platform Wi-Fi and route providers with explicit capability states.
7. Endpoint preflight/selection and shared evidence-backed interpretation.
8. Packaging, accessibility, UI rendering, and external validation gates.

Each step is independently reviewable and must preserve all existing checks.
