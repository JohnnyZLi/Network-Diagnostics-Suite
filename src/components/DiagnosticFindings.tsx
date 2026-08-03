import { classifyDiagnosticResult } from "../diagnostics/findings";
import type { DiagnosticFinding, DiagnosticResult } from "../types/diagnostics";

const severityLabels: Record<DiagnosticFinding["severity"], string> = {
  critical: "Action recommended",
  warning: "Worth investigating",
  info: "Context"
};

export function DiagnosticFindingList({
  findings,
  context
}: {
  findings: DiagnosticFinding[];
  context: string;
}) {
  return (
    <section className="diagnostic-findings" aria-labelledby="diagnostic-findings-title">
      <header className="diagnostic-findings__header">
        <div>
          <span className="eyebrow">Evidence-based interpretation</span>
          <h3 id="diagnostic-findings-title">What this run <em>suggests</em></h3>
        </div>
        <p>
          {context}
        </p>
      </header>

      <ol className="diagnostic-findings__list">
        {findings.map((finding) => (
          <li className={`diagnostic-finding diagnostic-finding--${finding.severity}`} key={finding.id}>
            <div className="diagnostic-finding__summary">
              <span className="diagnostic-finding__status">{severityLabels[finding.severity]}</span>
              <h4>{finding.title}</h4>
              <p>{finding.summary}</p>
              <span className="diagnostic-finding__confidence">{finding.confidence} confidence</span>
            </div>

            <div className="diagnostic-finding__support">
              <dl className="diagnostic-finding__evidence">
                {finding.evidence.map((item) => (
                  <div key={item.metric}>
                    <dt>{item.label}</dt>
                    <dd>{item.value}</dd>
                    {item.detail && <small>{item.detail}</small>}
                  </div>
                ))}
              </dl>
              <div className="diagnostic-finding__actions">
                <strong>What to try</strong>
                <ul>{finding.recommendations.map((recommendation) => <li key={recommendation}>{recommendation}</li>)}</ul>
                {finding.nextTest && <p><span>Next test</span>{finding.nextTest}</p>}
              </div>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}

export function DiagnosticFindings({ result }: { result: DiagnosticResult }) {
  const findings = result.findings?.length ? result.findings : classifyDiagnosticResult(result);
  const endpoint = result.measurement?.selectedEndpoint;
  const context = result.measurement
    ? `${result.measurement.engine} engine · ${endpoint?.name ?? "selected endpoint"}`
    : "Imported legacy report · interpreted with current rules";
  return <DiagnosticFindingList findings={findings} context={context} />;
}
