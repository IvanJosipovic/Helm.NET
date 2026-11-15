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
    string? RepositoryPassword = null);

internal sealed record NativeHelmResponse(
    string? Error = null,
    string? Text = null,
    JsonElement? Values = null,
    NativeHelmRelease? Release = null,
    NativeHelmRelease[]? Releases = null,
    HelmRepository[]? Repositories = null);

internal sealed class NativeHelmRelease
{
    public string Name { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string Revision { get; init; } = string.Empty;
    public string Updated { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Chart { get; init; } = string.Empty;
    public string AppVersion { get; init; } = string.Empty;

    public HelmRelease ToHelmRelease() => new(Name, Namespace, Revision, Updated, Status, Chart, AppVersion);
}
