# Contributing

Contributions should preserve the project's two core rules: measurements must be labeled at the layer actually observed, and no completed result may be retained without an explicit change to the documented privacy model.

## Development checks

Before opening a pull request, run:

```bash
npm run typecheck
npm test
npm run build
npm run probe:test
```

Changes to a formula, timeout, sample count, target, data cap, privacy behavior, or grade threshold must also update the relevant document in `docs/`.

## Pull requests

- Keep changes focused and explain the user-visible behavior.
- Add or update tests for calculation and parsing changes.
- Call out any new network destination or stored field.
- Do not relabel HTTP failures as packet loss or opaque browser timing as server processing time.
- Do not add analytics, advertising, trackers, or remote fonts.
