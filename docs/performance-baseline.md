# Performance baseline

Network Diagnostics records a reproducible engineering baseline for the built browser application. The baseline is separate from throughput-test results: it measures the application shell itself, not the user's network connection.

## What is recorded

- Total raw and gzip-compressed production asset size
- SHA-256 provenance and the largest built assets
- Median desktop and mobile navigation timing
- First Contentful Paint and Largest Contentful Paint
- Cumulative Layout Shift and observed long tasks
- Transferred and decoded resource bytes
- Resource count and rendered DOM-node count

The recorder uses the idle production application state, reduced motion, a local Vite preview, two fixed viewports, and three runs by default. Results are written to `performance-baseline/report.json` and `performance-baseline/report.md`.

## Local use

```bash
npm ci --no-audit --no-fund
npm install --no-save playwright@1.54.1
npx playwright install chromium
npm run build
npm run performance:baseline
```

Set `PERFORMANCE_RUNS` to an integer from 1 through 10 to change the sample count. Set `PERFORMANCE_BASE_URL` only when measuring an already-running production-equivalent preview.

## Continuous integration

`.github/workflows/performance-baseline.yml` runs for relevant pull requests, can be dispatched manually, and runs monthly to expose gradual drift. It uploads the generated reports for 30 days.

These browser timings are same-environment engineering evidence, not field measurements or universal performance claims. Compare runs only when the runner image, browser version, application state, and workload are reasonably equivalent. Review large changes against the visual and functional suites before turning any observed value into a blocking budget.
