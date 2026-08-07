# Cloudflare Workers Builds contract

`network.johnnyli.dev` is deployed from the `main` branch only.

The Cloudflare Workers Git integration must use:

- Production branch: `main`
- Production deploy command: `npx wrangler deploy`
- Non-production branch builds: disabled

If non-production previews are intentionally re-enabled later, their deploy command must be `npx wrangler versions upload`; they must never run `wrangler deploy` against the production Worker.

The browser build runs `scripts/verify-workers-build-context.mjs` before compilation. While a build is running under Workers Builds (`WORKERS_CI=1`), any branch other than `main` is rejected before Wrangler can execute. This is deliberate defense in depth against a dashboard branch-control regression.

The shared Web Design System is vendored at an immutable commit through `design-system.lock.json`. Long-lived desktop branches must be brought forward from `main` before they are used for browser previews so they cannot display an obsolete shared header or Sites menu.

The production Worker was deliberately rebuilt from the protected `main` branch after the Web Design System 1.9.0 rollout and branch guard were merged on 2026-08-06.

RolePacket has a separate deployment contract and is not governed by this document.
