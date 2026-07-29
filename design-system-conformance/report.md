# Design-system conformance: network

- Contract: 1.0.0
- Design system: 1.8.2
- Source commit: abb6c44f588afe09e8f593a8c467b564ac9fef86
- Generated: 2026-07-29T01:04:06.387Z
- Blocking failures: 0
- Manual pending: 3

| Rule | Severity | Status | Evidence |
| --- | --- | --- | --- |
| DS-DIST-001 | required | passed | PASS: design-system.lock.json#package matches; PASS: design-system.lock.json#version matches /^\d+\.\d+\.\d+$/; PASS: design-system.lock.json#sourceCommit matches /^[0-9a-f]{40}$/ |
| DS-DIST-002 | required | passed | PASS: scripts/check-design-system.mjs contains 3/3 required fragments; PASS: scripts/validate-design-system-integration.mjs contains 2/2 required fragments |
| DS-HEADER-001 | required | passed | PASS: src/App.tsx contains 3/3 required fragments; PASS: .github/workflows/visual-audit.yml contains 3/3 required fragments |
| DS-SITES-001 | required | passed | PASS: .github/workflows/visual-audit.yml contains 2/2 required fragments |
| DS-SITES-002 | required | passed | PASS: .github/workflows/visual-audit.yml contains 3/3 required fragments |
| DS-PRIMITIVE-001 | required | passed | PASS: scripts/validate-design-system-integration.mjs contains 2/2 required fragments |
| DS-DIALOG-001 | required | passed | PASS: src/components/TestControls.tsx contains 5/5 required fragments; PASS: scripts/validate-design-system-integration.mjs contains 2/2 required fragments |
| DS-DIALOG-002 | required | passed | PASS: .github/workflows/visual-audit.yml contains 3/3 required fragments |
| DS-RESP-001 | required | passed | PASS: .github/workflows/visual-audit.yml contains 2/2 required fragments |
| DS-RESP-002 | manual | manual-pending | Actual 200 percent browser zoom remains a manual release check; the automated narrow-desktop viewport is not labeled as zoom. |
| DS-A11Y-001 | required | passed | PASS: .github/workflows/visual-audit.yml contains 3/3 required fragments |
| DS-A11Y-002 | manual | manual-pending | Real forced-colors and assistive-technology review remains a recorded manual release check. |
| DS-STATE-001 | required | passed | PASS: src/App.tsx contains 2/2 required fragments; PASS: scripts/validate-design-system-integration.mjs contains 1/1 required fragments |
| DS-STATE-002 | required | passed | PASS: scripts/validate-design-system-integration.mjs contains 1/1 required fragments; PASS: .github/workflows/visual-audit.yml contains 2/2 required fragments |
| DS-OWN-001 | required | passed | PASS: scripts/validate-design-system-integration.mjs contains 3/3 required fragments |
| DS-WORKFLOW-001 | required | passed | PASS: .github/workflows/design-system-sync.yml matches /uses: JohnnyZLi\/Web-Design-System\/.github\/workflows\/consumer-design-system-sync\.yml@[0-9a-f]{40}/; PASS: .github/workflows/design-system-sync.yml contains 0/2 forbidden fragments |
| DS-TEST-001 | required | passed | PASS: .github/workflows/visual-audit.yml exists; PASS: .github/workflows/design-system-conformance.yml contains 6/6 required fragments |
| DS-PERF-001 | manual | manual-pending | Reviewed bundle and user-experience performance baselines have not yet been recorded. |
