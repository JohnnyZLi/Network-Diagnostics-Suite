namespace NetworkDiagnostics.Desktop.Presentation;

public enum ConnectionCheckOutcome
{
    Healthy,
    Problematic,
    Inconclusive,
    Unavailable,
    Failed
}

public sealed record MetricPresentation(
    string Label,
    string Value,
    string Detail,
    bool WasMeasured = true);

public sealed record FindingPresentation(
    string Label,
    string Title,
    string Summary);

public sealed record ConnectionCheckPresentation(
    ConnectionCheckOutcome Outcome,
    string Label,
    string Verdict,
    string Summary,
    string NextAction,
    IReadOnlyList<MetricPresentation> Metrics,
    IReadOnlyList<FindingPresentation> Findings,
    IReadOnlyList<string> TechnicalEvidence);

public static class ConnectionCheckFixtures
{
    public static IReadOnlyList<ConnectionCheckPresentation> All { get; } =
    [
        new(
            ConnectionCheckOutcome.Healthy,
            "Connection looks normal",
            "Your connection is working normally.",
            "The test reached the Internet without packet loss and stayed responsive while transferring data.",
            "Run Quick when you want a fuller performance snapshot.",
            [
                new("Latency", "18 ms", "Median idle latency"),
                new("Packet loss", "0%", "8 of 8 replies received"),
                new("Download", "143 Mbps", "Short aggregate sample"),
                new("Upload", "21 Mbps", "Short aggregate sample")
            ],
            [
                new("Summary", "No immediate problem detected", "The lightweight checks completed without a strong sign of a connection fault.")
            ],
            [
                "Profile: connection-check",
                "Transfer ceiling: 28 MB reported payload",
                "Endpoint: first-party project origin",
                "Native-only sections not collected by this profile remain Not measured"
            ]),
        new(
            ConnectionCheckOutcome.Problematic,
            "Problem detected",
            "The connection works, but responsiveness is poor.",
            "Traffic reached the Internet, but latency rose sharply during upload and a small amount of packet loss was observed.",
            "Run Full to separate a local-network problem from an upstream path problem.",
            [
                new("Latency", "42 ms", "Median idle latency"),
                new("Packet loss", "1.2%", "One reply was lost"),
                new("Download", "88 Mbps", "Short aggregate sample"),
                new("Upload", "9.8 Mbps", "Short aggregate sample")
            ],
            [
                new("Responsiveness", "Upload load caused high delay", "Latency increased by 176 ms while upload traffic was active."),
                new("Reliability", "Packet loss was detected", "A repeated Full test can show whether the loss is persistent or incidental.")
            ],
            [
                "Profile: connection-check",
                "Loaded upload latency increase: 176 ms",
                "ICMP replies: 79 of 80",
                "Finding confidence: medium"
            ]),
        new(
            ConnectionCheckOutcome.Inconclusive,
            "Result is inconclusive",
            "The connection is reachable, but the evidence conflicts.",
            "Latency and throughput varied enough that this short check cannot identify a reliable pattern.",
            "Repeat Connection Check once, then use Full if the result remains inconsistent.",
            [
                new("Latency", "65 ms", "Median varied between samples"),
                new("Packet loss", "0.5%", "Intermittent reply loss"),
                new("Download", "54–121 Mbps", "Large short-run variation"),
                new("Upload", "12 Mbps", "Short aggregate sample")
            ],
            [
                new("Confidence", "Measurements were inconsistent", "The short profile collected evidence, but not enough stable evidence for a firm diagnosis.")
            ],
            [
                "Profile: connection-check",
                "Download coefficient of variation exceeded the lightweight threshold",
                "No single finding reached high confidence",
                "All completed measurements remain present in the report"
            ]),
        new(
            ConnectionCheckOutcome.Unavailable,
            "Partially measured",
            "The connection works, but performance was not measured.",
            "Internet reachability succeeded, but the transfer endpoint was unavailable during the test.",
            "Try again later. This is an unavailable measurement, not a failed connection.",
            [
                new("Latency", "24 ms", "Median idle latency"),
                new("Packet loss", "0%", "8 of 8 replies received"),
                new("Download", "Not measured", "Transfer endpoint unavailable", false),
                new("Upload", "Not measured", "Transfer endpoint unavailable", false)
            ],
            [
                new("Availability", "Performance endpoint unavailable", "Reachability evidence was retained and the unsupported transfer section is marked Not measured.")
            ],
            [
                "Profile: connection-check",
                "Reachability phase completed",
                "Transfer endpoint returned no usable preflight candidate",
                "Unavailable sections are neutral, not failures"
            ]),
        new(
            ConnectionCheckOutcome.Failed,
            "Test did not complete",
            "Connection Check could not finish.",
            "The local gateway responded, but the Internet reachability phase failed before transfer measurements began.",
            "Check the device connection, VPN, or captive portal, then run the test again.",
            [
                new("Gateway", "2 ms", "Local gateway responded"),
                new("Internet latency", "Not measured", "Internet target unreachable", false),
                new("Download", "Not measured", "Test stopped before transfer", false),
                new("Upload", "Not measured", "Test stopped before transfer", false)
            ],
            [
                new("Failure", "Internet target was unreachable", "Partial local evidence is preserved instead of reporting every section as failed.")
            ],
            [
                "Profile: connection-check",
                "Default gateway responded",
                "Internet ping phase failed",
                "Transfer phases did not run"
            ])
    ];

    public static ConnectionCheckPresentation Get(int index) =>
        index >= 0 && index < All.Count ? All[index] : All[0];
}
