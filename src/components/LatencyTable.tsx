import { formatLatency } from "../core/format";
import type { LatencySummary, LoadedLatencySummary } from "../types/diagnostics";

interface LatencyTableProps {
  idle: LatencySummary;
  download: LoadedLatencySummary;
  upload: LoadedLatencySummary;
}

export function LatencyTable({ idle, download, upload }: LatencyTableProps) {
  const rows = [
    { name: "Idle", data: idle, increase: null, grade: null },
    { name: "During download", data: download, increase: download.increaseMs, grade: download.grade },
    { name: "During upload", data: upload, increase: upload.increaseMs, grade: upload.grade }
  ];

  return (
    <div className="latency-table-wrap">
      <table className="latency-table">
        <thead>
          <tr>
            <th scope="col">Condition</th>
            <th scope="col">Mean</th>
            <th scope="col">Median</th>
            <th scope="col">Minimum</th>
            <th scope="col">Maximum</th>
            <th scope="col">95th pct.</th>
            <th scope="col">Jitter</th>
            <th scope="col">Request loss</th>
            <th scope="col">Added delay</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.name}>
              <th scope="row">{row.name}{row.grade && <span className={`grade grade--${row.grade.replace("+", "plus")}`}>{row.grade}</span>}</th>
              <td>{formatLatency(row.data.meanMs)} ms</td>
              <td>{formatLatency(row.data.medianMs)} ms</td>
              <td>{formatLatency(row.data.minMs)} ms</td>
              <td>{formatLatency(row.data.maxMs)} ms</td>
              <td>{formatLatency(row.data.p95Ms)} ms</td>
              <td>{formatLatency(row.data.jitterMs)} ms</td>
              <td>{row.data.lossPercent.toFixed(1)}%</td>
              <td>{row.increase === null ? "—" : `+${formatLatency(row.increase)} ms`}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
