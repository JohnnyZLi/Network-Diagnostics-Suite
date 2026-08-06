using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private void RenderProfileSelection()
    {
        var profileId = SelectedProfile();
        var method = SelectedMethod();
        var plan = NetworkDiagnosticsRunner.DescribePlan(profileId, method);
        var profile = selectedProfileIndex switch
        {
            1 => new ProfileCopy(
                "What performance am I getting now?",
                "A broader speed and responsiveness snapshot using the selected transfer method.",
                "Quick runs real throughput and loaded-responsiveness measurements without the deeper route and service probes."),
            2 => new ProfileCopy(
                "Where is the likely problem?",
                "Adds local-network, route, resolver, service, Wi-Fi, protocol, and responsiveness evidence.",
                "Full runs the native deep-probe stack after its Internet transfer measurements and can include an optional LAN isolation target."),
            3 => new ProfileCopy(
                "How does the connection behave under sustained load?",
                "Runs sustained capacity, connection scaling in Compare mode, loaded responsiveness, and the native deep probe.",
                "Stress uses the largest transfer ceiling. Cancellation remains available throughout the run."),
            _ => new ProfileCopy(
                "Is the connection working normally?",
                "A lightweight first-party reachability, latency, request-loss, download, and upload check with a clear verdict.",
                "Connection Check runs the real native engine and saves its report locally when complete.")
        };

        profileQuestion = profile.Question;
        profilePurpose = profile.Purpose;
        estimatedTime = $"About {plan.EstimatedSeconds} seconds";
        transferCap = $"Up to {FormatBytes(plan.TransferCapBytes)}";
        confirmation = profileId is TestProfileId.Standard or TestProfileId.Extended
            ? "Required"
            : "Not required";
        profileAvailability = $"{profile.Availability} Transfer method: {MethodName(method)}.";
    }

    private void RenderMethodSelection()
    {
        methodExplanation = SelectedMethod() switch
        {
            TransferMethod.Single => "One connection in each direction. Best for a single download, tunnel, or remote session.",
            TransferMethod.Aggregate => "Parallel connections in each direction. Best for total application capacity.",
            _ => "Measures isolated and aggregate behavior separately. Stress Compare also produces the connection-scaling curve."
        };
    }

    private void RenderPresentation(ConnectionCheckPresentation presentation) =>
        currentPresentationValue = presentation;

    private static string MethodName(TransferMethod method) => method switch
    {
        TransferMethod.Single => "Single",
        TransferMethod.Aggregate => "Aggregate",
        _ => "Compare"
    };

    private static string FormatBytes(long bytes) => bytes >= 1_000_000_000
        ? $"{bytes / 1_000_000_000d:0.###} GB"
        : $"{bytes / 1_000_000d:0.#} MB";

    private sealed record ProfileCopy(
        string Question,
        string Purpose,
        string Availability);
}
