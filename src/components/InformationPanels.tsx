export function InformationPanels() {
  return (
    <section className="information-grid jl-grid-2 jl-responsive-region">
      <article className="information-panel jl-panel">
        <span className="eyebrow jl-eyebrow">Privacy model</span>
        <h2>No server-side result history.</h2>
        <div className="jl-prose">
          <p>The application has no accounts, analytics, database, advertising, or telemetry. It keeps up to 12 recent reports in this browser's local storage so useful runs are not lost; clearing site data or using the history control removes them.</p>
        </div>
        <ul className="plain-list">
          <li><span>01</span> Cloudflare processes the test traffic but Worker request logging is disabled.</li>
          <li><span>02</span> The tool never displays, stores, or returns your public IP address.</li>
          <li><span>03</span> A full test contacts the named services only after you select it.</li>
        </ul>
      </article>
      <article className="information-panel information-panel--probe jl-panel">
        <span className="eyebrow jl-eyebrow">Deep probe</span>
        <h2>Some answers require the operating system.</h2>
        <div className="jl-prose">
          <p>Browsers cannot perform honest traceroutes or expose raw packet loss. The optional native probe adds hops, Internet Control Message Protocol loss, Domain Name System timing, interface and gateway details, and path Maximum Transmission Unit discovery.</p>
        </div>
        <div className="probe-status jl-callout jl-callout--info"><span>Native builds</span><strong>Windows · macOS · Linux</strong></div>
      </article>
    </section>
  );
}
