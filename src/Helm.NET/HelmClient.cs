using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Helm.NET;

/// <summary>Provides typed operations backed by Helm's Go action library.</summary>
public sealed class HelmClient : IHelmClient
{
    private readonly HelmClientOptions _options;
    private readonly Func<byte[], string>? _nativeCall;
    private readonly IHelmNativeApi _nativeApi;

    /// <summary>Initializes a client that uses Helm's native action library.</summary>
    public HelmClient(HelmClientOptions? options = null) : this(options ?? new HelmClientOptions(), HelmNativeInterop.DefaultApi) { }

    internal HelmClient(HelmClientOptions options, Func<byte[], string> nativeCall) =>
        (_options, _nativeCall, _nativeApi) = (options, nativeCall, HelmNativeInterop.DefaultApi);

    internal HelmClient(HelmClientOptions options, IHelmNativeApi nativeApi) =>
        (_options, _nativeApi) = (options, nativeApi);

    /// <summary>Lists releases in the configured namespace.</summary>
    public IReadOnlyList<HelmRelease> ListReleases() => Invoke(new NativeHelmRequest("list"), NativeJsonContext.Default.HelmReleaseArray) ?? [];

    /// <summary>Lists releases and optionally searches all namespaces.</summary>
    public IReadOnlyList<HelmRelease> ListReleases(bool allNamespaces, string? filter = null) =>
        Invoke(new NativeHelmRequest("list", AllNamespaces: allNamespaces, Filter: filter), NativeJsonContext.Default.HelmReleaseArray) ?? [];

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    public string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, string? version = null)
    {
        return Invoke(new NativeHelmRequest("template", releaseName, chart, valuesFiles?.ToArray(), values, Version: version), NativeJsonContext.Default.String) ?? string.Empty;
    }

    /// <summary>Renders the chart and values stored in an installed release revision.</summary>
    public string TemplateRelease(string releaseName, int revision = 0) =>
        Invoke(new NativeHelmRequest("templateRelease", releaseName, Revision: revision), NativeJsonContext.Default.String) ?? string.Empty;

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    public string GetManifest(string releaseName, int revision = 0) =>
        Invoke(new NativeHelmRequest("getManifest", releaseName, Revision: revision), NativeJsonContext.Default.String) ?? string.Empty;

    /// <summary>Installs a chart using the default version resolution. Retained for binary compatibility.</summary>
    public HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles, IReadOnlyDictionary<string, object?>? values, bool createNamespace, bool wait, TimeSpan? timeout) =>
        Install(releaseName, chart, valuesFiles, values, createNamespace, wait, timeout, version: null);

    /// <summary>Installs a chart, optionally selecting a chart version, and returns the created release.</summary>
    public HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null)
    {
        var request = new NativeHelmRequest("install", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), Version: version);
        var response = Invoke(request, NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release for the install operation.");
    }

    /// <summary>Upgrades a release using the default version resolution. Retained for binary compatibility.</summary>
    public HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles, IReadOnlyDictionary<string, object?>? values, bool installIfMissing, bool createNamespace, bool wait, TimeSpan? timeout) =>
        Upgrade(releaseName, chart, valuesFiles, values, installIfMissing, createNamespace, wait, timeout, version: null);

    /// <summary>Upgrades an installed release, optionally selecting a chart version.</summary>
    public HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null)
    {
        var request = new NativeHelmRequest("upgrade", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), InstallIfMissing: installIfMissing, Version: version);
        var response = Invoke(request, NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release for the upgrade operation.");
    }

    /// <summary>Gets the current release status.</summary>
    public HelmRelease Status(string releaseName)
    {
        var response = Invoke(new NativeHelmRequest("status", releaseName), NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release status.");
    }

    /// <summary>Gets release revision history.</summary>
    public IReadOnlyList<HelmRelease> History(string releaseName) =>
        Invoke(new NativeHelmRequest("history", releaseName), NativeJsonContext.Default.HelmReleaseArray) ?? [];

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    public void Rollback(string releaseName, int revision = 0) =>
        Rollback(releaseName, revision, wait: false);

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    public void Rollback(string releaseName, int revision, bool wait) =>
        _ = Invoke(new NativeHelmRequest("rollback", releaseName, Wait: wait, Revision: revision), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Uninstalls a release.</summary>
    public void Uninstall(string releaseName) =>
        Uninstall(releaseName, wait: false);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    public void Uninstall(string releaseName, bool wait) =>
        _ = Invoke(new NativeHelmRequest("uninstall", releaseName, Wait: wait), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Gets the values applied to a release.</summary>
    public IReadOnlyDictionary<string, JsonElement> GetValues(string releaseName) =>
        Invoke(new NativeHelmRequest("values", releaseName, Namespace: _options.Namespace), NativeJsonContext.Default.ValueDictionary) ?? new Dictionary<string, JsonElement>();

    /// <summary>Lists configured chart repositories.</summary>
    public IReadOnlyList<HelmRepository> ListRepositories() =>
        Invoke(new NativeHelmRequest("repoList"), NativeJsonContext.Default.HelmRepositoryArray) ?? [];

    /// <summary>Searches configured repository indexes for charts and their available versions.</summary>
    public IReadOnlyList<HelmChartSearchResult> SearchCharts(string searchTerm) =>
        Invoke(new NativeHelmRequest("chartSearch", SearchTerm: searchTerm), NativeJsonContext.Default.HelmChartSearchResultArray) ?? [];

    /// <summary>Lists chart versions from cached indexes, optionally for one exact configured repository name.</summary>
    public IReadOnlyList<HelmChartSearchResult> ListCharts(string? repositoryName = null) =>
        Invoke(new NativeHelmRequest("chartList", RepositoryName: repositoryName), NativeJsonContext.Default.HelmChartSearchResultArray) ?? [];

    /// <summary>Adds or updates a chart repository.</summary>
    public void AddRepository(string name, string url, string? username = null, string? password = null) =>
        _ = Invoke(new NativeHelmRequest("repoAdd", RepositoryName: name, RepositoryURL: url, RepositoryUsername: username, RepositoryPassword: password), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Removes a configured chart repository.</summary>
    public void RemoveRepository(string name) =>
        _ = Invoke(new NativeHelmRequest("repoRemove", RepositoryName: name), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    public void UpdateRepositories() =>
        _ = Invoke(new NativeHelmRequest("repoUpdate"), NativeJsonContext.Default.NativeHelmAck);

    private T? Invoke<T>(NativeHelmRequest request, JsonTypeInfo<T> resultType)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(request with
        {
            Kubeconfig = _options.Kubeconfig,
            KubeContext = _options.KubeContext,
            Namespace = _options.Namespace,
            RepositoryConfig = _options.RepositoryConfig,
            RepositoryCache = _options.RepositoryCache,
            RegistryConfig = _options.RegistryConfig,
            HelmDriver = _options.HelmDriver
        }, NativeJsonContext.Default.NativeHelmRequest);
        var json = _nativeCall is null ? CallNative(payload) : _nativeCall(payload);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            throw new HelmActionException(error.GetString() ?? "Helm action failed.");
        return document.RootElement.TryGetProperty("data", out var data) ? data.Deserialize(resultType) : default;
    }

    private string CallNative(byte[] payload)
    {
        return HelmNativeInterop.Invoke(payload, _nativeApi);
    }
}
