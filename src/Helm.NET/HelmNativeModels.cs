using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helm.NET;

internal sealed record NativeHelmRequest(
    string Operation,
    string? ReleaseName = null,
    string? Chart = null,
    string[]? ValuesFiles = null,
    IReadOnlyDictionary<string, object?>? Values = null,
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
    [property: JsonPropertyName("repositoryUrl")] string? RepositoryURL = null,
    string? RepositoryUsername = null,
    string? RepositoryPassword = null);

internal sealed class NativeHelmAck { }

internal sealed class NativeHelmRelease
{
    public string Name { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string Revision { get; init; } = string.Empty;
    public string Updated { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Chart { get; init; } = string.Empty;
    [JsonPropertyName("app_version")] public string AppVersion { get; init; } = string.Empty;
    public string Manifest { get; init; } = string.Empty;

    public HelmRelease ToHelmRelease() => new(Name, Namespace, Revision, Updated, Status, Chart, AppVersion);
}
