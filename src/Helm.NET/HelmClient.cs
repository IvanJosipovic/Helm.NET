using System.Text.Json;

namespace Helm.NET;

/// <summary>Provides typed operations backed by Helm's Go action library.</summary>
public sealed class HelmClient : IHelmClient
{
    private readonly HelmClientOptions _options;
    private readonly IHelmNativeApi _nativeApi;

    /// <summary>Initializes a client that uses Helm's native action library.</summary>
    public HelmClient(HelmClientOptions? options = null) : this(options ?? new HelmClientOptions(), HelmNativeInterop.DefaultApi) { }

    internal HelmClient(HelmClientOptions options, IHelmNativeApi nativeApi) =>
        (_options, _nativeApi) = (options, nativeApi);

    /// <summary>Lists releases in the configured namespace.</summary>
    public IReadOnlyList<HelmRelease> ListReleases() =>
        Invoke(new NativeHelmRequest("list")).Releases?.Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Lists releases and optionally searches all namespaces.</summary>
    public IReadOnlyList<HelmRelease> ListReleases(bool allNamespaces, string? filter = null) =>
        Invoke(new NativeHelmRequest("list", AllNamespaces: allNamespaces, Filter: filter)).Releases?
            .Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    public string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null) =>
        Invoke(new NativeHelmRequest("template", releaseName, chart, valuesFiles?.ToArray(), values)).Text ?? string.Empty;

    /// <summary>Installs a chart and returns the created release.</summary>
    public HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null)
    {
        var request = new NativeHelmRequest("install", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0));
        return RequireRelease(Invoke(request), "Helm returned no release for the install operation.");
    }

    /// <summary>Upgrades an installed release.</summary>
    public HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null)
    {
        var request = new NativeHelmRequest("upgrade", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), InstallIfMissing: installIfMissing);
        return RequireRelease(Invoke(request), "Helm returned no release for the upgrade operation.");
    }

    /// <summary>Gets the current release status.</summary>
    public HelmRelease Status(string releaseName) =>
        RequireRelease(Invoke(new NativeHelmRequest("status", releaseName)), "Helm returned no release status.");

    /// <summary>Gets release revision history.</summary>
    public IReadOnlyList<HelmRelease> History(string releaseName) =>
        Invoke(new NativeHelmRequest("history", releaseName)).Releases?
            .Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    public void Rollback(string releaseName, int revision = 0) =>
        Rollback(releaseName, revision, wait: false);

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    public void Rollback(string releaseName, int revision, bool wait) =>
        _ = Invoke(new NativeHelmRequest("rollback", releaseName, Wait: wait, Revision: revision));

    /// <summary>Uninstalls a release.</summary>
    public void Uninstall(string releaseName) =>
        Uninstall(releaseName, wait: false);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    public void Uninstall(string releaseName, bool wait) =>
        _ = Invoke(new NativeHelmRequest("uninstall", releaseName, Wait: wait));

    /// <summary>Gets the values applied to a release as a JSON element.</summary>
    public JsonElement GetValues(string releaseName)
    {
        var response = Invoke(new NativeHelmRequest("values", releaseName, Namespace: _options.Namespace));
        if (response.Values is JsonElement values && values.ValueKind != JsonValueKind.Undefined)
            return values.Clone();

        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    /// <summary>Lists configured chart repositories.</summary>
    public IReadOnlyList<HelmRepository> ListRepositories() =>
        Invoke(new NativeHelmRequest("repoList")).Repositories ?? [];

    /// <summary>Adds or updates a chart repository.</summary>
    public void AddRepository(string name, string url, string? username = null, string? password = null) =>
        _ = Invoke(new NativeHelmRequest("repoAdd", RepositoryName: name, RepositoryURL: url, RepositoryUsername: username, RepositoryPassword: password));

    /// <summary>Removes a configured chart repository.</summary>
    public void RemoveRepository(string name) =>
        _ = Invoke(new NativeHelmRequest("repoRemove", RepositoryName: name));

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    public void UpdateRepositories() =>
        _ = Invoke(new NativeHelmRequest("repoUpdate"));

    private NativeHelmResponse Invoke(NativeHelmRequest request)
    {
        var response = _nativeApi.Execute(request with
        {
            Kubeconfig = _options.Kubeconfig,
            KubeContext = _options.KubeContext,
            Namespace = _options.Namespace,
            RepositoryConfig = _options.RepositoryConfig,
            RepositoryCache = _options.RepositoryCache,
            RegistryConfig = _options.RegistryConfig,
            HelmDriver = _options.HelmDriver
        });
        if (response.Error is not null)
            throw new HelmActionException(response.Error);
        return response;
    }

    private static HelmRelease RequireRelease(NativeHelmResponse response, string message) =>
        response.Release?.ToHelmRelease() ?? throw new InvalidOperationException(message);
}
