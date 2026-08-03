import type { DeepProbeReport, NativeCombinedReport } from "./types/deep-probe";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function isDeepProbeReport(value: unknown): value is DeepProbeReport {
  if (!isRecord(value)) return false;
  return (
    ["1.0", "1.1", "1.2"].includes(String(value.schemaVersion)) &&
    isRecord(value.internetPing) &&
    isRecord(value.traceRoute) &&
    Array.isArray(value.dnsResolvers) &&
    Array.isArray(value.serviceEndpoints)
  );
}

export function isCombinedReport(value: unknown): value is NativeCombinedReport {
  if (!isRecord(value) || value.schemaVersion !== "2.0") return false;
  if (!isRecord(value.run)) return false;

  const profile = value.run.profile;
  const transferMethod = value.run.transferMethod;
  return (
    typeof value.run.id === "string" &&
    typeof value.run.platform === "string" &&
    ["connection-check", "quick", "standard", "extended"].includes(String(profile)) &&
    ["compare", "single", "aggregate"].includes(String(transferMethod)) &&
    typeof value.run.startedAt === "string" &&
    typeof value.run.completedAt === "string" &&
    (value.internetTransfer === undefined || value.internetTransfer === null || isRecord(value.internetTransfer)) &&
    (value.deepDiagnostics === undefined || value.deepDiagnostics === null || isRecord(value.deepDiagnostics))
  );
}

export function combinedReportHasDeepDiagnostics(
  report: NativeCombinedReport,
): report is NativeCombinedReport & { deepDiagnostics: DeepProbeReport } {
  return isDeepProbeReport(report.deepDiagnostics);
}
