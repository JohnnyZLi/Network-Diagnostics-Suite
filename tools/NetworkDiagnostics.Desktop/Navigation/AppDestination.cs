namespace NetworkDiagnostics.Desktop.Navigation;

public enum WorkspaceKind
{
    Test,
    Reports,
    Comparisons,
    Settings
}

public abstract record AppDestination
{
    public abstract WorkspaceKind Workspace { get; }

    public abstract IReadOnlyList<BreadcrumbSegment> Breadcrumbs { get; }
}

public sealed record BreadcrumbSegment(string Label, AppDestination? Destination = null);

public sealed record TestSetupDestination : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Test;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Test")];
}

public sealed record RunningTestDestination(Guid RunId) : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Test;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Test", new TestSetupDestination()), new("Running")];
}

public sealed record TestResultDestination(Guid ReportId, string Section = "Overview") : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Test;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Test", new TestSetupDestination()), new("Result"), new(Section)];
}

public sealed record ReportListDestination : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Reports;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Reports")];
}

public sealed record ReportDetailDestination(Guid ReportId, string Section = "Overview") : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Reports;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Reports", new ReportListDestination()), new(Section)];
}

public sealed record ComparisonDestination(Guid? BaselineId = null, Guid? CandidateId = null) : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Comparisons;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Comparisons")];
}

public sealed record SettingsDestination(string Section = "General") : AppDestination
{
    public override WorkspaceKind Workspace => WorkspaceKind.Settings;

    public override IReadOnlyList<BreadcrumbSegment> Breadcrumbs =>
        [new("Settings"), new(Section)];
}
