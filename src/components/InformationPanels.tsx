const RELEASE_DOWNLOAD_BASE = "https://github.com/JohnnyZLi/Network-Diagnostics-Suite/releases/latest/download";

const NATIVE_BUILDS = [
  { label: "Windows x64", asset: "NetworkDiagnosticsDesktop-win-x64.zip" },
  { label: "macOS ARM64", asset: "NetworkDiagnosticsDesktop-osx-arm64.tar.gz" },
  { label: "macOS x64", asset: "NetworkDiagnosticsDesktop-osx-x64.tar.gz" },
  { label: "Linux ARM64", asset: "NetworkDiagnosticsDesktop-linux-arm64.tar.gz" },
  { label: "Linux x64", asset: "NetworkDiagnosticsDesktop-linux-x64.tar.gz" },
] as const;

export function InformationPanels() {
  return (
    <section className="information-grid">
      <article className="information-panel">
        <span className="eyebrow">Privacy model</span>
        <h2>No server-side result history.</h2>
        <p>The application has no accounts, analytics, database, advertising, or telemetry. It keeps up to 12 recent reports in this browser's local storage so useful runs are not lost; clearing site data or using the history control removes them.</p>
        <ul className="plain-list">
          <li><span>01</span> Cloudflare processes the test traffic but Worker request logging is disabled.</li>
          <li><span>02</span> The tool never displays, stores, or returns your public IP address.</li>
          <li><span>03</span> A full test contacts the named services only after you select it.</li>
        </ul>
      </article>
      <article className="information-panel information-panel--probe">
        <span className="eyebrow">Deep probe</span>
        <h2>Some answers require the operating system.</h2>
        <p>Browsers cannot perform honest traceroutes or expose raw packet loss. The optional native probe adds hops, Internet Control Message Protocol loss, Domain Name System timing, interface and gateway details, and path Maximum Transmission Unit discovery.</p>
        <div className="probe-status">
          <span>Native builds</span>
          <strong className="probe-status__links">
            {NATIVE_BUILDS.map((build, index) => (
              <span key={build.asset}>
                {index > 0 && <span aria-hidden="true"> · </span>}
                <a href={`${RELEASE_DOWNLOAD_BASE}/${build.asset}`} aria-label={`Download Network Diagnostics for ${build.label}`}>{build.label}</a>
              </span>
            ))}
          </strong>
        </div>
      </article>
    </section>
  );
}
