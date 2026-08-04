using System.Globalization;
using Avalonia.Interactivity;
using NetworkDeepProbe.Diagnostics;
using NetworkDeepProbe.Planning;
using NetworkDiagnostics.Desktop.Models;
using NetworkDiagnostics.Desktop.Navigation;
using NetworkDiagnostics.Desktop.Presentation;

namespace NetworkDiagnostics.Desktop;

public sealed partial class MainWindow
{
    private async void RunClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (activeRunSession.Snapshot.IsActive) return;
        var runProfile = SelectedProfile();
        var runMethod = SelectedMethod();
        if (!await ConfirmDataUseAsync(runProfile, runMethod)) return;

        activeProfile = runProfile;
        activeMethod = runMethod;
        activeRunNavigationId = activeRunSession.Start(runProfile, runMethod);
        currentReport = null;
        ResetRunningState();
        ShowTestState(TestViewState.Running);
        var progress = new Progress<NativeRunProgress>(RenderRunProgress);

        try
        {
            var report = await diagnosticRunService.RunAsync(
                runProfile,
                runMethod,
                settings,
                progress,
                activeRunSession.CancellationToken);
            currentReport = report;
            await reportStore.SaveAsync(report, activeRunSession.CancellationToken);
            currentPresentation = DiagnosticReportPresenter.FromReport(report);
            activeRunSession.Complete(report);
            CompleteRunningState();
            RenderPresentation(currentPresentation);
            await RefreshHistoryAsync();
            PresentRunOutcome(report.Run.Id);
        }
        catch (OperationCanceledException)
        {
            activeRunSession.MarkCancelled();
            currentPresentation = DiagnosticReportPresenter.FromCancellation(runProfile);
            RenderPresentation(currentPresentation);
            PresentRunOutcome(Guid.Empty);
        }
        catch (Exception error)
        {
            activeRunSession.Fail(error);
            currentPresentation = DiagnosticReportPresenter.FromFailure(runProfile, error);
            RenderPresentation(currentPresentation);
            PresentRunOutcome(Guid.Empty);
        }
        finally
        {
            RenderProfileSelection();
            SyncTestWorkspace();
            RefreshWorkbenchChrome();
        }
    }

    private void StopClicked(object? sender, RoutedEventArgs eventArgs)
    {
        activeRunSession.RequestCancel();
        RefreshWorkbenchChrome();
    }

    private void RunAgainClicked(object? sender, RoutedEventArgs eventArgs) =>
        NavigateToDestination(new TestSetupDestination());

    private void ChooseQuickClicked(object? sender, RoutedEventArgs eventArgs)
    {
        ProfileSelector.SelectedIndex = 1;
        NavigateToDestination(new TestSetupDestination());
    }

    private void RenderRunProgress(NativeRunProgress progress)
    {
        var nextProgress = Math.Max(displayedRunProgress, OverallProgress(progress));
        var deepProfile = activeRunSession.Snapshot.Profile is TestProfileId.Standard or TestProfileId.Extended;
        if (progress.Phase == "diagnostics" && nextProgress >= 20 && deepProfile)
        {
            nextProgress = Math.Min(96, nextProgress + 2.5);
        }

        displayedRunProgress = nextProgress;
        activeRunSession.UpdateProgress(
            progress.Phase,
            LiveProgressText(progress),
            displayedRunProgress,
            progress.LiveMbps,
            progress.LiveLatencyMs,
            progress.BytesTransferred);
        RefreshWorkbenchChrome();
    }

    private void ResetRunningState()
    {
        displayedRunProgress = 0;
        SyncTestWorkspace();
        RefreshWorkbenchChrome();
    }

    private void CompleteRunningState()
    {
        displayedRunProgress = 100;
        SyncTestWorkspace();
        RefreshWorkbenchChrome();
    }

    private async Task<bool> ConfirmDataUseAsync(TestProfileId profile, TransferMethod method)
    {
        var plan = NetworkDiagnosticsRunner.DescribePlan(profile, method);
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
        var deepProfile = activeRunSession.Snapshot.Profile is TestProfileId.Standard or TestProfileId.Extended;
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
