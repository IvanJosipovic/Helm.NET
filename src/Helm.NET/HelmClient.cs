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

    /// <summary>Lists releases, optionally across all namespaces or matching a filter.</summary>
    public IReadOnlyList<HelmRelease> List(string? namespaceName = null, bool allNamespaces = false, string? filter = null, int max = 256, int offset = 0, string? selector = null, bool deployed = false, bool failed = false, bool pending = false, bool superseded = false, bool uninstalled = false, bool uninstalling = false, bool dateSort = false, bool reverse = false) =>
        Invoke(new NativeHelmRequest("list", AllNamespaces: allNamespaces, Filter: filter, Max: max, Offset: offset, Selector: selector, Deployed: deployed, Failed: failed, Pending: pending, Superseded: superseded, Uninstalled: uninstalled, Uninstalling: uninstalling, Date: dateSort, Reverse: reverse, Namespace: namespaceName), NativeJsonContext.Default.NativeHelmReleaseArray)?.Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    public string Template(string name, string chart, string? namespaceName = null, IEnumerable<string>? values = null, JsonElement? setValues = null, string? version = null, IEnumerable<string>? apiVersions = null, string? kubeVersion = null, bool includeCrds = false, bool skipTests = false, bool dependencyUpdate = false, bool isUpgrade = false, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        return Invoke(new NativeHelmRequest("template", name, chart, values?.ToArray(), setValues, Version: version, Namespace: namespaceName, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), DependencyUpdate: dependencyUpdate, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp, ApiVersions: apiVersions?.ToArray(), KubeVersion: kubeVersion, IncludeCrds: includeCrds, SkipTests: skipTests, IsUpgrade: isUpgrade), NativeJsonContext.Default.String) ?? string.Empty;
    }

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    public string GetManifest(string releaseName, string? namespaceName = null, int revision = 0) =>
        Invoke(new NativeHelmRequest("getManifest", releaseName, Revision: revision, Namespace: namespaceName), NativeJsonContext.Default.String) ?? string.Empty;

    /// <summary>Installs a chart, optionally selecting a chart version, and returns the created release.</summary>
    public HelmRelease Install(string name, string chart, string? namespaceName = null, IEnumerable<string>? values = null, JsonElement? setValues = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool dependencyUpdate = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, string? waitStrategy = null, bool replace = false, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        var request = new NativeHelmRequest("install", name, chart, values?.ToArray(), setValues, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, Version: version, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), WaitForJobs: waitForJobs, RollbackOnFailure: rollbackOnFailure, Devel: devel, DependencyUpdate: dependencyUpdate, SkipCrds: skipCrds, SkipSchemaValidation: skipSchemaValidation, DisableOpenApiValidation: disableOpenApiValidation, Description: description, WaitStrategy: waitStrategy, Replace: replace, ForceReplace: forceReplace, ForceConflicts: forceConflicts, TakeOwnership: takeOwnership, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp);
        var response = Invoke(request, NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release for the install operation.");
    }

    /// <summary>Upgrades an installed release, optionally selecting a chart version.</summary>
    public HelmRelease Upgrade(string release, string chart, string? namespaceName = null, IEnumerable<string>? values = null, JsonElement? setValues = null, bool install = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool resetValues = false, bool reuseValues = false, bool resetThenReuseValues = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, bool dependencyUpdate = false, string? waitStrategy = null, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, bool cleanupOnFail = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        var request = new NativeHelmRequest("upgrade", release, chart, values?.ToArray(), setValues, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, InstallIfMissing: install, Version: version, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), WaitForJobs: waitForJobs, RollbackOnFailure: rollbackOnFailure, ResetValues: resetValues, ReuseValues: reuseValues, ResetThenReuseValues: resetThenReuseValues, DependencyUpdate: dependencyUpdate, SkipCrds: skipCrds, Devel: devel, SkipSchemaValidation: skipSchemaValidation, DisableOpenApiValidation: disableOpenApiValidation, Description: description, WaitStrategy: waitStrategy, ForceReplace: forceReplace, ForceConflicts: forceConflicts, TakeOwnership: takeOwnership, CleanupOnFail: cleanupOnFail, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp);
        var response = Invoke(request, NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release for the upgrade operation.");
    }

    /// <summary>Gets the current release status.</summary>
    public HelmRelease Status(string releaseName, string? namespaceName = null, int revision = 0)
    {
        var response = Invoke(new NativeHelmRequest("status", releaseName, Revision: revision, Namespace: namespaceName), NativeJsonContext.Default.NativeHelmRelease);
        return response?.ToHelmRelease() ?? throw new InvalidOperationException("Helm returned no release status.");
    }

    /// <summary>Gets release revision history.</summary>
    public IReadOnlyList<HelmRelease> History(string releaseName, string? namespaceName = null, int max = 256) =>
        Invoke(new NativeHelmRequest("history", releaseName, Max: max, Namespace: namespaceName), NativeJsonContext.Default.NativeHelmReleaseArray)?.Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    public void Rollback(string releaseName, int revision = 0, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool waitForJobs = false, bool disableHooks = false, string? dryRun = null, bool forceReplace = false, bool forceConflicts = false, bool cleanupOnFail = false, int historyMax = 10, string? description = null) =>
        _ = Invoke(new NativeHelmRequest("rollback", releaseName, Wait: wait, TimeoutMilliseconds: (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, Revision: revision, WaitForJobs: waitForJobs, DisableHooks: disableHooks, DryRun: dryRun, WaitStrategy: waitStrategy, ForceReplace: forceReplace, ForceConflicts: forceConflicts, CleanupOnFail: cleanupOnFail, HistoryMax: historyMax, Description: description), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Uninstalls a release.</summary>
    public void Uninstall(string releaseName, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool disableHooks = false, bool dryRun = false, bool ignoreNotFound = false, bool keepHistory = false, string cascade = "background", string? description = null) =>
        _ = Invoke(new NativeHelmRequest("uninstall", releaseName, Wait: wait, TimeoutMilliseconds: (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, DisableHooks: disableHooks, WaitStrategy: waitStrategy, DryRunUninstall: dryRun, IgnoreNotFound: ignoreNotFound, KeepHistory: keepHistory, Cascade: cascade, Description: description), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Gets the values applied to a release.</summary>
    public JsonElement GetValues(string releaseName, string? namespaceName = null, int revision = 0, bool all = false)
    {
        var values = Invoke(new NativeHelmRequest("values", releaseName, Namespace: namespaceName, Revision: revision, AllValues: all), NativeJsonContext.Default.JsonElement);
        return values.ValueKind == JsonValueKind.Undefined ? CreateEmptyJsonObject() : values;
    }

    /// <summary>Lists configured chart repositories.</summary>
    public IReadOnlyList<HelmRepository> RepoList() =>
        Invoke(new NativeHelmRequest("repoList"), NativeJsonContext.Default.HelmRepositoryArray) ?? [];

    /// <summary>Searches configured repository indexes for charts and their available versions.</summary>
    public IReadOnlyList<HelmChartSearchResult> SearchRepo(string keyword = "", bool versions = false, bool devel = false, bool regexp = false, string? version = null, bool failOnNoResult = false) =>
        Invoke(new NativeHelmRequest("chartSearch", SearchTerm: keyword, Versions: versions, Devel: devel, Regexp: regexp, VersionConstraint: version, FailOnNoResult: failOnNoResult), NativeJsonContext.Default.HelmChartSearchResultArray) ?? [];

    /// <summary>Adds a chart repository; replaces an existing entry only when forceUpdate is true.</summary>
    public void RepoAdd(string name, string url, string? username = null, string? password = null, bool forceUpdate = false) =>
        _ = Invoke(new NativeHelmRequest("repoAdd", RepositoryName: name, RepositoryURL: url, RepositoryUsername: username, RepositoryPassword: password, ForceUpdate: forceUpdate), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Removes a configured chart repository.</summary>
    public void RepoRemove(string repository) =>
        _ = Invoke(new NativeHelmRequest("repoRemove", RepositoryName: repository), NativeJsonContext.Default.NativeHelmAck);

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    public void RepoUpdate() =>
        _ = Invoke(new NativeHelmRequest("repoUpdate"), NativeJsonContext.Default.NativeHelmAck);

    private T? Invoke<T>(NativeHelmRequest request, JsonTypeInfo<T> resultType)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(request with
        {
            Kubeconfig = _options.Kubeconfig,
            KubeContext = _options.KubeContext,
            Namespace = request.Namespace ?? _options.DefaultNamespace,
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

    private static void ValidateSetValues(JsonElement? setValues)
    {
        if (setValues is JsonElement values && values.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("setValues must be a JSON object.", nameof(setValues));
    }

    private static JsonElement CreateEmptyJsonObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    private string CallNative(byte[] payload)
    {
        return HelmNativeInterop.Invoke(payload, _nativeApi);
    }
}
