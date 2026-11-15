using System.Text.Json;

namespace Helm.NET;

internal sealed record NativeHelmRequest(
    string Operation,
    string? ReleaseName = null,
    string? Chart = null,
    string[]? ValuesFiles = null,
    JsonElement? Values = null,
    bool CreateNamespace = false,
    bool Wait = false,
    long TimeoutMilliseconds = 0,
    string? Kubeconfig = null,
    string? KubeContext = null,
    string? Namespace = null,
    bool AllNamespaces = false,
    string? Filter = null,
    int Revision = 0,
    bool InstallIfMissing = false,
    string? RepositoryConfig = null,
    string? RepositoryCache = null,
    string? RegistryConfig = null,
    string? HelmDriver = null,
    string? RepositoryName = null,
    string? RepositoryURL = null,
    string? RepositoryUsername = null,
    string? RepositoryPassword = null,
    string? Version = null,
    string? SearchTerm = null,
    bool AllValues = false,
    bool Versions = false,
    bool Devel = false,
    bool Regexp = false,
    string? VersionConstraint = null,
    bool FailOnNoResult = false,
    int Max = 0,
    int Offset = 0,
    string? Selector = null,
    bool Deployed = false,
    bool Failed = false,
    bool Pending = false,
    bool Superseded = false,
    bool Uninstalled = false,
    bool Uninstalling = false,
    bool Date = false,
    bool ResetValues = false,
    bool ReuseValues = false,
    bool ResetThenReuseValues = false,
    string? DryRun = null,
    bool HideSecret = false,
    bool DisableHooks = false,
    string? PostRenderer = null,
    string[]? PostRendererArgs = null,
    bool WaitForJobs = false,
    bool RollbackOnFailure = false,
    bool Reverse = false,
    bool DependencyUpdate = false,
    bool SkipCrds = false,
    bool SkipSchemaValidation = false,
    bool DisableOpenApiValidation = false,
    string? Description = null,
    bool ForceUpdate = false,
    string? WaitStrategy = null,
    bool Replace = false,
    bool ForceReplace = false,
    bool ForceConflicts = false,
    bool TakeOwnership = false,
    bool CleanupOnFail = false,
    int HistoryMax = 0,
    bool DryRunUninstall = false,
    bool IgnoreNotFound = false,
    bool KeepHistory = false,
    string? Cascade = null,
    string? Repo = null,
    string? CaFile = null,
    string? CertFile = null,
    string? KeyFile = null,
    bool InsecureSkipTlsVerify = false,
    bool PassCredentials = false,
    bool PlainHttp = false,
    string[]? ApiVersions = null,
    string? KubeVersion = null,
    bool IncludeCrds = false,
    bool SkipTests = false,
    bool IsUpgrade = false);

internal sealed record NativeHelmResponse(
    string? Error = null,
    string? Text = null,
    JsonElement? Values = null,
    NativeHelmRelease? Release = null,
    NativeHelmRelease[]? Releases = null,
    HelmRepository[]? Repositories = null,
    HelmChartSearchResult[]? ChartSearchResults = null);

internal sealed class NativeHelmRelease
{
    public string Name { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public int Revision { get; init; }
    public required DateTimeOffset Updated { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Chart { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;
    public string ChartName { get; init; } = string.Empty;
    public string ChartVersion { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public JsonElement? Resources { get; init; }
    public NativeHelmHook[] Hooks { get; init; } = [];

    public HelmRelease ToHelmRelease() => new(Name, Namespace, Revision, Updated, Status, Chart, AppVersion, ChartName, ChartVersion)
    {
        Description = Description,
        Notes = Notes,
        Resources = Resources,
        Hooks = Hooks.Select(static hook => hook.ToHelmReleaseHook()).ToArray()
    };
}

internal sealed class NativeHelmHook
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Manifest { get; init; } = string.Empty;
    public string[] Events { get; init; } = [];
    public NativeHelmHookExecution LastRun { get; init; } = new();
    public int Weight { get; init; }
    public string[] DeletePolicies { get; init; } = [];

    public HelmReleaseHook ToHelmReleaseHook() => new(
        Name, Kind, Path, Manifest, Events, LastRun.StartedAt, LastRun.CompletedAt,
        LastRun.Phase, Weight, DeletePolicies);
}

internal sealed class NativeHelmHookExecution
{
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string Phase { get; init; } = string.Empty;
}
