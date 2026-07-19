export function InformationPanels() {
  return (
    <section className="information-grid">
      <article className="information-panel">
        <span className="eyebrow">Privacy model</span>
        <h2>Nothing is retained.</h2>
        <p>The application has no accounts, cookies, analytics, database, advertising, or telemetry. Results exist only in this browser tab unless you export them.</p>
        <ul className="plain-list">
          <li><span>01</span> Cloudflare processes the test traffic but Worker request logging is disabled.</li>
          <li><span>02</span> The tool never displays, stores, or returns your public IP address.</li>
          <li><span>03</span> A full test contacts the named services only after you select it.</li>
        </ul>
      </article>
      <article className="information-panel information-panel--probe">
        <span className="eyebrow">Deep probe</span>
        <h2>Some answers require the operating system.</h2>
        <p>Browsers cannot perform honest traceroutes or expose raw packet loss. The optional Windows probe adds hops, Internet Control Message Protocol loss, Domain Name System timing, interface and gateway details, and path Maximum Transmission Unit discovery.</p>
        <div className="probe-status"><span>In repository</span><strong>Windows 11 x64</strong></div>
      </article>
    </section>
  );
}
