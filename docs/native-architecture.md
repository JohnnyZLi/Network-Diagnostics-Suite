# Native application architecture

## Current boundary

The shipped native component is a self-contained .NET command-line probe. It owns operating-system diagnostics and optional two-machine LAN throughput, while the browser application owns Internet throughput, profiles, transfer-method selection, result history, and graphical presentation.

The migration keeps the command-line interface compatible while introducing reusable layers for a graphical desktop application.

## Target projects

```text
tools/NetworkDiagnostics.Core/       Shared diagnostics, contracts, planning, and report models
tools/DeepProbe/                     Backward-compatible command-line host
tools/NetworkDiagnostics.Desktop/    Cross-platform graphical host
tools/DeepProbe.Tests/               Core, CLI, contract, and integration tests
```

The first extraction intentionally keeps existing namespaces and source locations stable. The core project links the current diagnostics and report-model files so that moving code does not alter runtime behavior or make review harder.

## Shared product contract

`contracts/test-profiles.v1.json` is the canonical profile input for both TypeScript and .NET implementations. It records:

- Quick, Full, and Stress limits
- Compare, Single, and Aggregate method identifiers
- transfer durations and byte ceilings
- independent download and upload connection counts
- comparison allocations
- the Stress 1, 2, 4, 8, and 10 connection sequence

The browser configuration remains executable TypeScript, but tests must reject drift from this contract. The .NET core embeds and parses the same file.

## Report evolution

Native schema 1.0 and 1.1 remain supported for imports. New combined runs will use the `2.0` envelope described by `contracts/report-v2.schema.json`.

The envelope separates three scopes:

1. Internet transfer measurements
2. operating-system deep diagnostics
3. optional two-machine local-link measurements

A missing section means that scope did not run or was unavailable. It must not be replaced with guessed data.

## Planned implementation order

1. Extract the reusable .NET core without changing behavior.
2. Implement the shared transfer-plan builder and cross-language fixtures.
3. Add first-party native Internet download/upload stages and loaded latency.
4. Emit and import report schema 2.0 while retaining 1.x compatibility.
5. Add the cross-platform desktop host and shared product hierarchy.
6. Add platform-specific Wi-Fi and route providers with explicit capability states.
7. Extend packaging, accessibility, UI smoke testing, and release validation.

Each step is independently reviewable and must preserve all existing checks.
