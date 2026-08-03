using System.Globalization;
using Avalonia.Interactivity;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private async void RunClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (runCancellation is not null) return;
        activeProfile = SelectedProfile();
        if (!await ConfirmDataUseAsync(activeProfile)) return;

        var cancellation = new CancellationTokenSource();
        runCancellation = cancellation;
        currentReport = null;
        ResetRunningState();
        RunningProfileText.Text = DiagnosticReportPresenter.ProfileName(activeProfile).ToUpperInvariant();
        ShowTestState(TestViewState.Running);
        var progress = new Progress<NativeRunProgress>(RenderRunProgress);

        try
        {
            var report = await diagnosticRunService.RunAsync(
                activeProfile,
                settings.IncludeLocalIdentifiers,
                settings.ParsedTestOrigin,
                progress,
                cancellation.Token);
            currentReport = report;
            await reportStore.SaveAsync(report, cancellation.Token);
            currentPresentation = DiagnosticReportPresenter.FromReport(report);
            CompleteRunningState();
            RenderPresentation(currentPresentation);
            await RefreshHistoryAsync();
            ShowTestState(TestViewState.Results);
        }
        catch (OperationCanceledException)
        {
            currentPresentation = DiagnosticReportPresenter.FromCancellation(activeProfile);
            RenderPresentation(currentPresentation);
            ShowTestState(TestViewState.Results);
        }
        catch (Exception error)
        {
            currentPresentation = DiagnosticReportPresenter.FromFailure(activeProfile, error);
            RenderPresentation(currentPresentation);
            ShowTestState(TestViewState.Results);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(runCancellation, cancellation)) runCancellation = null;
            RenderProfileSelection();
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs) => runCancellation?.Cancel();

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
    }

    private void ChooseQuickClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ProfileSelector.SelectedIndex = 1;
        ShowArea(DesktopArea.Test);
        ShowTestState(TestViewState.Setup);
    }

    private void RenderRunProgress(NativeRunProgress progress)
    {
        CurrentPhaseText.Text = progress.Message;
        LiveMeasurementText.Text = LiveProgressText(progress);
        displayedRunProgress = Math.Max(displayedRunProgress, OverallProgress(progress));
        RunProgress.Value = displayedRunProgress;

        var deepProfile = activeProfile is TestProfileId.Standard or TestProfileId.Extended;
        if (progress.Phase == "diagnostics")
        {
            if (displayedRunProgress < 20)
            {
                NetworkPhaseStatus.Text = "In progress";
            }
            else if (deepProfile)
            {
                DeepPhaseStatus.Text = "In progress";
                displayedRunProgress = Math.Min(96, displayedRunProgress + 2.5);
                RunProgress.Value = displayedRunProgress;
            }
            return;
        }

        switch (progress.Phase)
        {
            case "idle":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "In progress";
                break;
            case "download":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "In progress";
                break;
            case "upload":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "Complete";
                UploadPhaseStatus.Text = "In progress";
                break;
            case "complete":
                NetworkPhaseStatus.Text = "Complete";
                LatencyPhaseStatus.Text = "Complete";
                DownloadPhaseStatus.Text = "Complete";
                UploadPhaseStatus.Text = "Complete";
                DeepPhaseStatus.Text = deepProfile ? "Waiting" : "Not included";
                break;
        }
    }

    private void ResetRunningState()
    {
        displayedRunProgress = 0;
        RunProgress.Value = 0;
        CurrentPhaseText.Text = "Preparing the test…";
        LiveMeasurementText.Text = "Starting…";
        NetworkPhaseStatus.Text = "Waiting";
        LatencyPhaseStatus.Text = "Waiting";
        DownloadPhaseStatus.Text = "Waiting";
        UploadPhaseStatus.Text = "Waiting";
        DeepPhaseStatus.Text = activeProfile is TestProfileId.Standard or TestProfileId.Extended
            ? "Waiting"
            : "Not included";
    }

    private void CompleteRunningState()
    {
        displayedRunProgress = 100;
        RunProgress.Value = 100;
        NetworkPhaseStatus.Text = "Complete";
        LatencyPhaseStatus.Text = "Complete";
        DownloadPhaseStatus.Text = "Complete";
        UploadPhaseStatus.Text = "Complete";
        DeepPhaseStatus.Text = activeProfile is TestProfileId.Standard or TestProfileId.Extended
            ? "Complete"
            : "Not included";
    }

    private async Task<bool> ConfirmDataUseAsync(TestProfileId profile)
    {
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, TransferMethod.Compare);
        if (profile is not (TestProfileId.Standard or TestProfileId.Extended)
            || settings.HasDataApproval(profile, plan.TransferCapBytes))
        {
            return true;
        }

        var confirmation = await new DataUseDialog(plan).ShowDialog<DataUseConfirmation>(this);
        if (!confirmation.Confirmed) return false;
        if (confirmation.Remember)
        {
            settings = settings.WithDataApproval(profile, plan.TransferCapBytes);
            await PersistSettingsAsync();
        }
        return true;
    }

    private double OverallProgress(NativeRunProgress progress)
    {
        var deepProfile = activeProfile is TestProfileId.Standard or TestProfileId.Extended;
        return progress.Phase switch
        {
            "idle" => 8 + progress.Fraction * 17,
            "download" => 25 + progress.Fraction * 27,
            "upload" => 52 + progress.Fraction * 20,
            "complete" => deepProfile ? 74 : 100,
            "diagnostics" => displayedRunProgress < 8 ? 5 : displayedRunProgress,
            _ => displayedRunProgress
        };
    }

    private static string LiveProgressText(NativeRunProgress progress)
    {
        var values = new List<string>();
        if (progress.LiveMbps is { } mbps)
        {
            values.Add($"{mbps.ToString(mbps >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} Mbps");
        }
        if (progress.LiveLatencyMs is { } latency)
        {
            values.Add($"{latency.ToString(latency >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture)} ms live latency");
        }
        if (progress.BytesTransferred > 0)
        {
            values.Add($"{(progress.BytesTransferred / 1_000_000d).ToString("0.0", CultureInfo.InvariantCulture)} MB measured");
        }
        return values.Count == 0 ? progress.Message : string.Join(" · ", values);
    }
}
