const RELEASE_DOWNLOAD_BASE = "https://github.com/JohnnyZLi/Network-Diagnostics-Suite/releases/latest/download";

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
            <a href={`${RELEASE_DOWNLOAD_BASE}/NetworkDiagnosticsDesktop-win-x64.zip`} aria-label="Download Network Diagnostics for Windows x64">Windows x64</a>
            <span aria-hidden="true">·</span>
            <a href={`${RELEASE_DOWNLOAD_BASE}/NetworkDiagnosticsDesktop-osx-arm64.tar.gz`} aria-label="Download Network Diagnostics for macOS ARM64">macOS ARM64</a>
            <span aria-hidden="true">·</span>
            <a href={`${RELEASE_DOWNLOAD_BASE}/NetworkDiagnosticsDesktop-osx-x64.tar.gz`} aria-label="Download Network Diagnostics for macOS x64">macOS x64</a>
            <span aria-hidden="true">·</span>
            <a href={`${RELEASE_DOWNLOAD_BASE}/NetworkDiagnosticsDesktop-linux-x64.tar.gz`} aria-label="Download Network Diagnostics for Linux x64">Linux x64</a>
            <span aria-hidden="true">·</span>
            <a href={`${RELEASE_DOWNLOAD_BASE}/NetworkDiagnosticsDesktop-linux-arm64.tar.gz`} aria-label="Download Network Diagnostics for Linux ARM64">Linux ARM64</a>
          </strong>
        </div>
      </article>
    </section>
  );
}
