# Native application architecture

## Current boundary

The native suite ships two self-contained hosts over one .NET diagnostics engine:

- `NetworkDiagnosticsDesktop` embeds a React + TypeScript + Vite workbench in Photino.NET. It owns passive monitoring, guided and advanced diagnostics, native run coordination, local report history, comparisons, import/export, and settings.
- `NetworkDeepProbe` preserves the scriptable command-line interface for deep diagnostics, Internet transfer, and two-machine LAN testing.

Both hosts use the same planning, probe, findings, storage, and schema 2.0 report implementations. Unsupported operating-system data is represented as **Not measured**, not inferred.

## Projects

```text
tools/NetworkDiagnostics.Core/       Shared diagnostics, contracts, planning, and report models
tools/DeepProbe/                     Backward-compatible command-line host
tools/NetworkDiagnostics.Desktop/    Cross-platform graphical host
tools/DeepProbe.Tests/               Core, CLI, contract, and integration tests
```

The desktop's Photino bridge translates typed frontend messages into calls to the shared engine. The CLI calls the same engine directly. Frontend state does not reimplement diagnostic planning or report serialization.

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

## Distribution boundary

Tagged desktop versions build self-contained Windows x64, macOS Apple Silicon, macOS Intel, Linux x64, and Linux ARM64 archives. Ordinary CI builds are short-lived development artifacts; `desktop-vX.Y.Z` tags publish retained GitHub Release assets and checksums. Code signing and notarization remain a separate protected release process.
