# Deployment guide

The complete application requires dynamic ping, download, upload, and metadata endpoints. GitHub Pages can host a portfolio card or documentation, but it cannot run those endpoints. Deploy this repository as a Cloudflare Worker and link it from `johnnyli.dev`.

Recommended URL: `https://network.johnnyli.dev`

## First deployment

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

The Worker configuration serves `dist/` as static assets, routes `/api/*` through `worker/index.ts`, falls back to the React entry page for client routes, and disables Worker observability.

## Attach the subdomain

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

## Production safeguards

A public bandwidth test can be automated by third parties. Before advertising the URL broadly:

- Add a Cloudflare rate-limiting rule for `/api/download` and `/api/upload` that permits a legitimate Stress run but blocks sustained automated repetition.
- Set billing and usage notifications appropriate to the Cloudflare account plan.
- Keep Worker observability disabled unless logs are intentionally needed for troubleshooting.
- If logs are enabled temporarily, document the change and disable them afterward.
- Re-run Quick, Full, and Stress from both IPv4 and IPv6 networks.
- Confirm the Content Security Policy still permits only the documented common-service targets.

The application already caps each download response at 24 MiB and each upload request at 8 MiB, rejects standard cross-site browser requests, and requires explicit confirmation for the largest browser profile. Those are guardrails, not a substitute for edge rate limiting.

## Continuous integration

`.github/workflows/ci.yml` validates every push and pull request by:

- Type-checking, testing, and building the web application.
- Producing a dry-run Worker bundle.
- Running the .NET test suite.
- Testing and publishing native self-contained probes for Windows x64, macOS Apple Silicon and Intel, and Linux x64 and ARM64, each with a SHA-256 checksum.

Deployment is intentionally not automatic in the initial version. This keeps Cloudflare credentials out of the repository setup and makes the first production release deliberate. A deployment workflow can be added later with a scoped Cloudflare API token and the account ID stored as GitHub Actions secrets.

## Portfolio integration

Add a project card on `johnnyli.dev` with:

- **Title:** Network Diagnostics Suite
- **Summary:** Privacy-first browser testing plus a cross-platform native probe for throughput, latency distributions, bufferbloat, packet loss, route, DNS, MTU, and TLS diagnostics.
- **Live link:** `https://network.johnnyli.dev`
- **Source link:** `https://github.com/JohnnyZLi/Network-Diagnostics-Suite`
- **Evidence:** browser engine, Cloudflare Worker, cross-platform .NET probe, operating-system build matrix, automated tests, and documented measurement boundaries.
