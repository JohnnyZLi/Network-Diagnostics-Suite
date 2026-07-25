# Deployment guide

The complete application requires dynamic ping, fallback download, upload, and metadata endpoints. GitHub Pages can host a portfolio card or documentation, but it cannot run those endpoints. Deploy this repository as a Cloudflare Worker and link it from `johnnyli.dev`.

Recommended application URL: `https://network.johnnyli.dev`

Recommended direct-download URL: `https://speed.johnnyli.dev/network-diagnostics-speed-v1.bin`

## First Worker deployment

Install dependencies and validate the project:

```bash
npm ci
npm run typecheck
npm test
npm run build
```

Authenticate Wrangler and deploy:

```bash
npx wrangler login
npm run deploy
```

The Worker configuration serves `dist/` as static assets, routes `/api/*` and the Worker comparison path through `worker/index.ts`, falls back to the React entry page for client routes, and disables Worker observability.

## Attach the application subdomain

In the Cloudflare dashboard:

1. Open **Workers & Pages** and select `network-diagnostics-suite`.
2. Open **Settings**, then **Domains & Routes**.
3. Choose **Add**, then **Custom Domain**.
4. Enter `network.johnnyli.dev` and confirm.
5. Let Cloudflare create and manage the DNS record for the Worker.

Do not point this subdomain to GitHub Pages. The apex `johnnyli.dev` and its existing GitHub Pages records remain unchanged.

After the certificate is active, test:

```bash
curl -I https://network.johnnyli.dev/
curl https://network.johnnyli.dev/api/health
curl https://network.johnnyli.dev/api/meta
```

The health endpoint should return `{"status":"ok"}`. The metadata response should not contain a public IP address.

## Provision the direct R2 comparison path

The direct path intentionally bypasses the Worker response body. It requires one public R2 bucket, one 256 MiB deterministic object, a bucket CORS policy, a custom domain, and a Cache Rule.

### 1. Create the bucket

Run this once:

```bash
npm run r2:bucket:create
```

The expected bucket name is `network-diagnostics-speed`.

### 2. Generate and upload the object

```bash
npm run r2:upload
```

The generator writes an ignored deterministic file to `.r2-speed/network-diagnostics-speed-v1.bin`. The upload sets `Content-Type: application/octet-stream` and `Cache-Control: no-store, no-transform`. Browser caches therefore must not retain the response.

### 3. Apply the browser CORS policy

```bash
npm run r2:cors
```

`infra/r2-cors.json` allows `GET` and `HEAD` from `https://network.johnnyli.dev` and the local Vite origin. It exposes `CF-Cache-Status`, `Age`, `Content-Length`, `Content-Range`, and `ETag` so the browser can validate and report the direct path.

### 4. Connect the custom domain

In the Cloudflare dashboard:

1. Open **R2 Object Storage**.
2. Select `network-diagnostics-speed`.
3. Open **Settings**.
4. Under **Public access → Custom Domains**, connect `speed.johnnyli.dev`.
5. Wait for the domain status and certificate to become active.
6. Keep the `r2.dev` development URL disabled unless it is deliberately needed for troubleshooting.

Do not attach `speed.johnnyli.dev` to the Worker. It must resolve directly to the R2 bucket custom domain for the A/B comparison to be meaningful.

### 5. Add the cache rule

Create a Cache Rule for:

```text
(http.host eq "speed.johnnyli.dev" and
 http.request.uri.path eq "/network-diagnostics-speed-v1.bin")
```

Set:

- **Cache eligibility:** Eligible for cache.
- **Edge TTL:** Ignore origin cache-control and use a one-day TTL.
- **Browser TTL:** Respect origin.

The Edge TTL override lets Cloudflare cache the object even though the response sent to the browser remains `Cache-Control: no-store`. This separation is necessary: the edge should reuse the object, while each browser run must transfer it again.

The browser sends distinct single-range requests for each parallel worker. The R2 custom domain serves those ranges from the cached full object when available, avoiding identical-request coalescing without requiring query-string cache-key customization.

### 6. Verify the endpoint

After the custom domain, CORS policy, and cache rule are active:

```bash
npm run r2:verify
```

The verifier checks the full object size, production CORS origin, and a 1 KiB byte-range response. Then run the browser test twice using **R2 direct**. A cold first run may report `MISS`; the repeated run should normally report mostly or entirely `HIT`.

## A/B measurement procedure

Use the same browser, device, network, profile, and time window:

1. Run **R2 direct** twice and retain the second report.
2. Run **Worker** twice and retain the second report.
3. Compare whole-phase rate, steady rate, stability, peak, loaded latency, protocol, cache status, and request lifecycle.
4. Use **Auto** only after both explicit paths have been verified. Auto prefers R2 and falls back to the Worker path when the R2 probe or CORS contract fails.

A direct R2 improvement isolates Worker response composition as a meaningful limiter. Similar R2 and Worker results point instead toward the browser transport connection, Cloudflare route, local link, or congestion behavior.

## Production safeguards

A public bandwidth test can be automated by third parties. Before advertising the URL broadly:

- Add Cloudflare rate limiting for `/api/download`, `/api/upload`, and the R2 speed hostname.
- Set billing and usage notifications appropriate to the Cloudflare account plan.
- Keep Worker observability disabled unless logs are intentionally needed for troubleshooting.
- If logs are enabled temporarily, document the change and disable them afterward.
- Re-run Quick, Full, and Stress from both IPv4 and IPv6 networks.
- Confirm the Content Security Policy permits only the documented direct R2 and common-service targets.
- Keep the R2 bucket limited to deterministic test payloads; do not place private data in the public bucket.

The application caps dynamic download and upload requests, rejects standard cross-site requests to Worker endpoints, and requires explicit confirmation for the largest browser profile. R2 remains a public read-only object endpoint, so Cloudflare edge controls are part of the deployment model.

## Continuous integration

`.github/workflows/ci.yml` validates every push and pull request by:

- Type-checking, testing, and building the web application.
- Producing a dry-run Worker bundle.
- Running the .NET test suite.
- Testing and publishing native self-contained probes for Windows x64, macOS Apple Silicon and Intel, and Linux x64 and ARM64, each with a SHA-256 checksum.

Deployment and R2 provisioning are intentionally not automatic. This keeps Cloudflare credentials out of the repository setup and makes public storage/domain changes deliberate.

## Portfolio integration

Add a project card on `johnnyli.dev` with:

- **Title:** Network Diagnostics Suite
- **Summary:** Privacy-first browser testing plus a cross-platform native probe for throughput, latency distributions, bufferbloat, packet loss, route, DNS, MTU, and TLS diagnostics.
- **Live link:** `https://network.johnnyli.dev`
- **Source link:** `https://github.com/JohnnyZLi/Network-Diagnostics-Suite`
- **Evidence:** browser engine, Cloudflare Worker, direct R2 A/B path, cross-platform .NET probe, operating-system build matrix, automated tests, and documented measurement boundaries.
