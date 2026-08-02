# Deployment and native distribution

The browser application requires dynamic ping, fallback download, upload, and metadata endpoints. GitHub Pages can host a portfolio card or documentation, but not the complete service. Deploy the browser application as a Cloudflare Worker and distribute native clients through reproducible GitHub Actions artifacts or a later signed release process.

Recommended application URL: `https://network.johnnyli.dev`

Recommended direct-download URL: `https://speed.johnnyli.dev/network-diagnostics-speed-v1.bin`

## Browser deployment

Install dependencies and validate:

```bash
npm ci
npm run design-system:check
npm run design-system:integration
npm run typecheck
npm test
npm run build
```

Authenticate and deploy:

```bash
npx wrangler login
npm run deploy
```

The Worker serves `dist/`, routes `/api/*` and the Worker stream through `worker/index.ts`, falls back to the React entry page for client routes, and disables Worker observability.

### Attach the application domain

In Cloudflare **Workers & Pages → network-diagnostics-suite → Settings → Domains & Routes**, add `network.johnnyli.dev` as a Custom Domain. Let Cloudflare manage the DNS record and certificate. Do not point this subdomain to GitHub Pages.

Verify:

```bash
curl -I https://network.johnnyli.dev/
curl https://network.johnnyli.dev/api/health
curl https://network.johnnyli.dev/api/meta
```

The health endpoint should return `{"status":"ok"}`. Metadata must not contain a public IP address.

## Direct R2 path

The browser's normal direct-download path requires one public R2 bucket, a deterministic 256 MiB object, CORS, a custom domain, an edge-cache rule, and a Timing-Allow-Origin response rule.

### Create and populate the bucket

```bash
npm run r2:bucket:create
npm run r2:upload
npm run r2:cors
```

The expected bucket is `network-diagnostics-speed`. The object is generated under the ignored `.r2-speed/` directory with `Content-Type: application/octet-stream` and `Cache-Control: no-store, no-transform`.

### Connect the custom domain

In **R2 Object Storage → network-diagnostics-speed → Settings → Public access**, connect `speed.johnnyli.dev`. Keep the `r2.dev` URL disabled unless intentionally used for troubleshooting. Do not attach the speed hostname to the Worker.

### Cache rule

Match:

```text
(http.host eq "speed.johnnyli.dev" and
 http.request.uri.path eq "/network-diagnostics-speed-v1.bin")
```

Set:

- Cache eligibility: eligible.
- Edge TTL: ignore origin cache control and use one day.
- Browser TTL: bypass.

The edge can reuse the deterministic object while every browser test still transfers the response.

### Resource Timing rule

Using the same filter, set:

```text
Timing-Allow-Origin: https://network.johnnyli.dev
```

This allows browser Resource Timing to expose `nextHopProtocol` for R2 requests. Purge the speed hostname after changing CORS or response headers.

Verify:

```bash
npm run r2:verify
```

The verifier checks object size, CORS, Timing-Allow-Origin, and a byte-range response.

## Native build and distribution

The native core has two hosts:

- `NetworkDeepProbe`: command-line deep diagnostics and LAN server/client, with optional first-party Internet transfer.
- `NetworkDiagnosticsDesktop`: graphical Windows, macOS, and Linux application using the same core and schema 2.0 runner.

Build all self-contained targets locally with .NET 10:

```bash
npm run probe:build
npm run desktop:build
```

CI builds both hosts for:

| Runtime | Platform |
| --- | --- |
| `win-x64` | Windows 11 x64 |
| `osx-arm64` | Apple Silicon macOS |
| `osx-x64` | Intel macOS |
| `linux-x64` | glibc Linux x64 |
| `linux-arm64` | glibc Linux ARM64 |

Each job:

1. runs the shared native test suite where applicable;
2. publishes a self-contained single-file binary;
3. launches a non-network smoke test on the matching operating system;
4. stages license and run/privacy notes;
5. creates ZIP or TAR distribution archives;
6. creates SHA-256 checksum files;
7. uploads 30-day workflow artifacts.

The desktop smoke test uses `--smoke-test`, loads the shared Quick/Compare plan, and exits without creating a window or contacting the network.

### Signing and installers

Current CI artifacts are not code-signed, notarized, or wrapped in platform installers. Do not label them as trusted signed releases.

A production release process should add:

- Windows Authenticode signing and an installer format such as MSIX or MSI;
- macOS application bundling, Developer ID signing, hardened runtime, and notarization;
- Linux package metadata or clearly documented portable archives;
- immutable release tags and retained checksums;
- manual launch and accessibility review on physical Windows, macOS, and Linux systems.

Signing credentials must be held in protected repository environments or an external signing service, never committed to the repository.

## Continuous integration

Pull requests and `main` runs retain all existing gates:

- design-system drift, integration, and conformance;
- browser and Worker typechecking, tests, build, and Wrangler dry run;
- browser UI regression and visual audit;
- performance baseline;
- CodeQL and secret scanning;
- native unit and contract tests;
- five command-line build/smoke/package jobs;
- five desktop build/smoke/package jobs.

Deployment, R2 provisioning, signing, notarization, and installer publication remain deliberate manual processes.

## Production safeguards

A public bandwidth test can be automated by third parties. Before broad promotion:

- rate-limit dynamic bandwidth endpoints and the R2 speed hostname;
- configure Cloudflare usage and billing notifications;
- keep Worker observability disabled unless logs are intentionally needed;
- re-run profiles from IPv4 and IPv6 networks;
- verify Content Security Policy targets;
- keep the public bucket limited to deterministic test data;
- monitor native release signing and checksum provenance separately from browser deployment.

Client-side controls cannot prevent custom scripts from calling public endpoints. Infrastructure limits are part of the deployment model.

## Portfolio integration

Recommended project card:

- **Title:** Network Diagnostics Suite
- **Summary:** Privacy-first browser and native network testing for single-flow and aggregate throughput, loaded latency, packet loss, routes, DNS, MTU, TLS, Wi-Fi, and LAN isolation.
- **Live:** `https://network.johnnyli.dev`
- **Source:** `https://github.com/JohnnyZLi/Network-Diagnostics-Suite`
- **Evidence:** React/TypeScript engine, Cloudflare Worker and R2 delivery, shared .NET core, cross-platform desktop application, deep-probe CLI, ten native package jobs, automated tests, and explicit measurement/privacy boundaries.
