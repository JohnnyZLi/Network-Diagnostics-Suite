using System.Globalization;
using System.Net;
using System.Text;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop.Monitoring;

public static class MonitoringExportService
{
    public static string BuildShareHtml(NetworkExperiencePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var builder = new StringBuilder();
        builder.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        builder.Append("<title>Network health snapshot</title><style>");
        builder.Append("body{margin:0;background:#0f0b16;color:#f4eff7;font:14px system-ui,-apple-system,sans-serif}");
        builder.Append("main{max-width:980px;margin:0 auto;padding:42px 24px 60px}.eyebrow{color:#d87657;font-size:11px;font-weight:700;letter-spacing:.13em;text-transform:uppercase}");
        builder.Append(".hero{display:grid;grid-template-columns:260px 1fr;gap:22px;margin-top:18px}.panel{background:#1d1427;border-radius:16px;padding:22px}");
        builder.Append(".score{width:180px;height:180px;border-radius:50%;display:grid;place-content:center;text-align:center;margin:12px auto;background:#163b25;color:#f4eff7}");
        builder.Append(".score strong{font-size:56px;line-height:1}.score span{font-size:13px;margin-top:5px}.components{display:grid;gap:12px}.component{background:#1d1427;border-radius:14px;padding:18px}");
        builder.Append(".component h2{margin:0 0 4px;font-size:17px}.status{float:right;font-weight:700}.summary{color:#c0b7c7;margin:4px 0 14px}");
        builder.Append(".metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}.metric{border-left:1px solid #382943;padding-left:12px}.metric:first-child{border-left:0;padding-left:0}");
        builder.Append(".metric small{display:block;color:#8e8397;margin-bottom:3px}.metric strong{font-size:15px}.timeline{display:flex;align-items:end;gap:2px;height:68px;margin-top:14px}");
        builder.Append(".bar{flex:1;min-width:2px;border-radius:2px;background:#55d47e}.bar.laggy{background:#f19758}.bar.down{background:#f06e64}.bar.inactive{background:#392d42}.bar.diagnostic{background:#d87657;box-shadow:inset 0 0 0 1px #efa086}");
        builder.Append(".legend{display:flex;gap:16px;flex-wrap:wrap;margin-top:12px;color:#8e8397;font-size:12px}.legend b{color:#d87657}.alerts{margin-top:22px}.alert{border-top:1px solid #382943;padding:12px 0}.muted{color:#8e8397}@media(max-width:720px){.hero{grid-template-columns:1fr}.metrics{grid-template-columns:1fr 1fr}}");
        builder.Append("</style></head><body><main>");
        builder.Append("<div class=\"eyebrow\">Network Diagnostics · Local snapshot</div>");
        builder.Append("<h1>").Append(Html(presentation.DeviceName)).Append("</h1>");
        builder.Append("<div class=\"hero\"><section class=\"panel\">");
        builder.Append("<div class=\"muted\">").Append(Html(presentation.InterfaceName)).Append(" · ").Append(Html(presentation.LastUpdated)).Append("</div>");
        builder.Append("<div class=\"score\"><strong>").Append(presentation.Score?.ToString(CultureInfo.InvariantCulture) ?? "—").Append("</strong><span>").Append(Html(presentation.Status)).Append("</span></div>");
        builder.Append("<p>").Append(Html(presentation.Summary)).Append("</p></section><section class=\"components\">");
        AppendComponent(builder, presentation.Responsiveness);
        AppendComponent(builder, presentation.Reliability);
        AppendComponent(builder, presentation.Speed);
        builder.Append("</section></div>");
        builder.Append("<section class=\"panel\" style=\"margin-top:22px\"><div class=\"eyebrow\">Timeline · ").Append(Html(presentation.Window.ContractId())).Append("</div><div class=\"timeline\">");
        var diagnosticSamples = 0;
        foreach (var sample in presentation.Timeline)
        {
            var className = sample.IsDiagnosticLoad
                ? "bar diagnostic"
                : sample.State switch
                {
                    MonitorSampleState.Unresponsive => "bar down",
                    MonitorSampleState.Laggy => "bar laggy",
                    MonitorSampleState.Inactive => "bar inactive",
                    _ => "bar"
                };
            if (sample.IsDiagnosticLoad) diagnosticSamples++;
            var height = sample.State == MonitorSampleState.Unresponsive
                ? 100
                : Math.Clamp(15 + (sample.LatencyMs ?? 0) / 4, 15, 95);
            var title = sample.IsDiagnosticLoad
                ? sample.LatencyMs is null ? "Controlled diagnostic load" : $"Controlled diagnostic load · {sample.LatencyMs:0.#} ms"
                : sample.State == MonitorSampleState.Unresponsive ? "Unreachable" : $"{sample.LatencyMs:0.#} ms";
            builder.Append("<span class=\"").Append(className).Append("\" style=\"height:")
                .Append(height.ToString("0", CultureInfo.InvariantCulture)).Append("%\" title=\"")
                .Append(Html(title))
                .Append("\"></span>");
        }
        builder.Append("</div>");
        if (diagnosticSamples > 0)
        {
            builder.Append("<div class=\"legend\"><span><b>Diagnostic load</b> marks traffic generated intentionally by Network Diagnostics; those samples are not used to lower passive health scores.</span></div>");
        }
        builder.Append("</section>");
        if (presentation.Alerts.Count > 0)
        {
            builder.Append("<section class=\"panel alerts\"><div class=\"eyebrow\">Recent alerts</div>");
            foreach (var alert in presentation.Alerts)
            {
                builder.Append("<div class=\"alert\"><strong>").Append(Html(alert.Title)).Append("</strong><div>")
                    .Append(Html(alert.Detail)).Append("</div><small class=\"muted\">")
                    .Append(Html(alert.Timestamp.ToLocalTime().ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture)))
                    .Append("</small></div>");
            }
            builder.Append("</section>");
        }
        builder.Append("<p class=\"muted\" style=\"margin-top:24px\">Generated locally by Network Diagnostics. Scores summarize passive health in the selected time window and are not an Internet service guarantee.</p>");
        builder.Append("</main></body></html>");
        return builder.ToString();
    }

    public static string BuildHistoryCsv(
        MonitorSnapshot snapshot,
        MonitorWindow window,
        bool includeIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var cutoff = DateTimeOffset.UtcNow - window.Duration();
        var builder = new StringBuilder();
        builder.AppendLine("timestamp_utc,state,latency_ms,jitter_ms,dns_ms,time_to_first_byte_ms,packet_loss_percent,download_mbps,upload_mbps,is_speed_measurement,is_diagnostic_load,interface,network_signature");
        foreach (var sample in snapshot.Samples.Where(sample => sample.Timestamp >= cutoff).OrderBy(sample => sample.Timestamp))
        {
            builder.Append(Csv(sample.Timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(sample.State.ToString())).Append(',')
                .Append(Number(sample.LatencyMs)).Append(',')
                .Append(Number(sample.JitterMs)).Append(',')
                .Append(Number(sample.DnsMs)).Append(',')
                .Append(Number(sample.TimeToFirstByteMs)).Append(',')
                .Append(sample.PacketLossPercent.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(Number(sample.DownloadMbps)).Append(',')
                .Append(Number(sample.UploadMbps)).Append(',')
                .Append(sample.IsSpeedMeasurement ? "true" : "false").Append(',')
                .Append(sample.IsDiagnosticLoad ? "true" : "false").Append(',')
                .Append(Csv(includeIdentifiers ? sample.InterfaceName : "redacted")).Append(',')
                .Append(Csv(includeIdentifiers ? sample.NetworkSignature : "redacted"))
                .AppendLine();
        }
        return builder.ToString();
    }

    private static void AppendComponent(StringBuilder builder, ExperienceComponentPresentation component)
    {
        builder.Append("<article class=\"component\"><span class=\"status\">")
            .Append(component.Score?.ToString(CultureInfo.InvariantCulture) ?? "—").Append(" · ")
            .Append(Html(component.Status)).Append("</span><h2>").Append(Html(component.Title)).Append("</h2><p class=\"summary\">")
            .Append(Html(component.Summary)).Append("</p><div class=\"metrics\">");
        foreach (var metric in component.Metrics)
        {
            builder.Append("<div class=\"metric\"><small>").Append(Html(metric.Label)).Append("</small><strong>")
                .Append(Html(metric.Value)).Append("</strong></div>");
        }
        builder.Append("</div></article>");
    }

    private static string Number(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
