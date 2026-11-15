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

    /// <summary>Lists releases with Helm list filtering and pagination options.</summary>
    public IReadOnlyList<HelmRelease> List(string? namespaceName = null, bool allNamespaces = false, string? filter = null, int max = 256, int offset = 0, string? selector = null, bool deployed = false, bool failed = false, bool pending = false, bool superseded = false, bool uninstalled = false, bool uninstalling = false, bool dateSort = false, bool reverse = false) =>
        Invoke(new NativeHelmRequest("list", AllNamespaces: allNamespaces, Filter: filter, Max: max, Offset: offset, Selector: selector, Deployed: deployed, Failed: failed, Pending: pending, Superseded: superseded, Uninstalled: uninstalled, Uninstalling: uninstalling, Date: dateSort, Reverse: reverse, Namespace: namespaceName)).Releases?
            .Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    public string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null) =>
        Invoke(new NativeHelmRequest("template", releaseName, chart, valuesFiles?.ToArray(), values)).Text ?? string.Empty;

    /// <summary>Renders a chart with Helm CLI-compatible template options.</summary>
    public string Template(string name, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, string? version = null, IEnumerable<string>? apiVersions = null, string? kubeVersion = null, bool includeCrds = false, bool skipTests = false, bool dependencyUpdate = false, bool isUpgrade = false, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        return Invoke(new NativeHelmRequest("template", name, chart, values?.ToArray(), setValues, Version: version, Namespace: namespaceName, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), DependencyUpdate: dependencyUpdate, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp, ApiVersions: apiVersions?.ToArray(), KubeVersion: kubeVersion, IncludeCrds: includeCrds, SkipTests: skipTests, IsUpgrade: isUpgrade)).Text ?? string.Empty;
    }

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    public string GetManifest(string releaseName, string? namespaceName = null, int revision = 0) =>
        Invoke(new NativeHelmRequest("getManifest", releaseName, Revision: revision, Namespace: namespaceName)).Text ?? string.Empty;

    /// <summary>Returns a chart's default values.yaml content.</summary>
    public string ShowValues(string chart, string? version = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false) =>
        Invoke(new NativeHelmRequest("showValues", Chart: chart, Version: version, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp)).Text ?? string.Empty;

    /// <summary>Installs a chart and returns the created release.</summary>
    public HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null)
    {
        var request = new NativeHelmRequest("install", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0));
        return RequireRelease(Invoke(request), "Helm returned no release for the install operation.");
    }

    /// <summary>Installs a chart with Helm CLI-compatible options.</summary>
    public HelmRelease Install(string name, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool dependencyUpdate = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, string? waitStrategy = null, bool replace = false, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        var request = new NativeHelmRequest("install", name, chart, values?.ToArray(), setValues, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, Version: version, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), WaitForJobs: waitForJobs, RollbackOnFailure: rollbackOnFailure, Devel: devel, DependencyUpdate: dependencyUpdate, SkipCrds: skipCrds, SkipSchemaValidation: skipSchemaValidation, DisableOpenApiValidation: disableOpenApiValidation, Description: description, WaitStrategy: waitStrategy, Replace: replace, ForceReplace: forceReplace, ForceConflicts: forceConflicts, TakeOwnership: takeOwnership, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp);
        return RequireRelease(Invoke(request), "Helm returned no release for the install operation.");
    }

    /// <summary>Upgrades an installed release.</summary>
    public HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null)
    {
        var request = new NativeHelmRequest("upgrade", releaseName, chart, valuesFiles?.ToArray(), values, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), InstallIfMissing: installIfMissing);
        return RequireRelease(Invoke(request), "Helm returned no release for the upgrade operation.");
    }

    /// <summary>Upgrades a release with Helm CLI-compatible options.</summary>
    public HelmRelease Upgrade(string release, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, bool install = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool resetValues = false, bool reuseValues = false, bool resetThenReuseValues = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, bool dependencyUpdate = false, string? waitStrategy = null, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, bool cleanupOnFail = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false)
    {
        ValidateSetValues(setValues);
        var request = new NativeHelmRequest("upgrade", release, chart, values?.ToArray(), setValues, createNamespace, wait, (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, InstallIfMissing: install, Version: version, DryRun: dryRun, HideSecret: hideSecret, DisableHooks: disableHooks, PostRenderer: postRenderer, PostRendererArgs: postRendererArgs?.ToArray(), WaitForJobs: waitForJobs, RollbackOnFailure: rollbackOnFailure, ResetValues: resetValues, ReuseValues: reuseValues, ResetThenReuseValues: resetThenReuseValues, DependencyUpdate: dependencyUpdate, SkipCrds: skipCrds, Devel: devel, SkipSchemaValidation: skipSchemaValidation, DisableOpenApiValidation: disableOpenApiValidation, Description: description, WaitStrategy: waitStrategy, ForceReplace: forceReplace, ForceConflicts: forceConflicts, TakeOwnership: takeOwnership, CleanupOnFail: cleanupOnFail, Repo: repo, RepositoryUsername: username, RepositoryPassword: password, CaFile: caFile, CertFile: certFile, KeyFile: keyFile, InsecureSkipTlsVerify: insecureSkipTlsVerify, PassCredentials: passCredentials, PlainHttp: plainHttp);
        return RequireRelease(Invoke(request), "Helm returned no release for the upgrade operation.");
    }

    /// <summary>Gets the current release status.</summary>
    public HelmRelease Status(string releaseName) =>
        RequireRelease(Invoke(new NativeHelmRequest("status", releaseName)), "Helm returned no release status.");

    /// <summary>Gets a release status at the requested revision.</summary>
    public HelmRelease Status(string releaseName, string? namespaceName = null, int revision = 0) =>
        RequireRelease(Invoke(new NativeHelmRequest("status", releaseName, Revision: revision, Namespace: namespaceName)), "Helm returned no release status.");

    /// <summary>Gets release revision history.</summary>
    public IReadOnlyList<HelmRelease> History(string releaseName) =>
        Invoke(new NativeHelmRequest("history", releaseName)).Releases?
            .Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Gets release history, optionally limited to the most recent revisions.</summary>
    public IReadOnlyList<HelmRelease> History(string releaseName, string? namespaceName = null, int max = 256) =>
        Invoke(new NativeHelmRequest("history", releaseName, Max: max, Namespace: namespaceName)).Releases?
            .Select(static release => release.ToHelmRelease()).ToArray() ?? [];

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    public void Rollback(string releaseName, int revision = 0) =>
        Rollback(releaseName, revision, namespaceName: null, wait: false);

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    public void Rollback(string releaseName, int revision, bool wait) =>
        Rollback(releaseName, revision, namespaceName: null, wait: wait);

    /// <summary>Rolls a release back with Helm CLI-compatible options.</summary>
    public void Rollback(string releaseName, int revision, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool waitForJobs = false, bool disableHooks = false, string? dryRun = null, bool forceReplace = false, bool forceConflicts = false, bool cleanupOnFail = false, int historyMax = 10, string? description = null) =>
        _ = Invoke(new NativeHelmRequest("rollback", releaseName, Wait: wait, TimeoutMilliseconds: (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, Revision: revision, WaitForJobs: waitForJobs, DisableHooks: disableHooks, DryRun: dryRun, WaitStrategy: waitStrategy, ForceReplace: forceReplace, ForceConflicts: forceConflicts, CleanupOnFail: cleanupOnFail, HistoryMax: historyMax, Description: description));

    /// <summary>Uninstalls a release.</summary>
    public void Uninstall(string releaseName) =>
        Uninstall(releaseName, wait: false);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    public void Uninstall(string releaseName, bool wait) =>
        _ = Invoke(new NativeHelmRequest("uninstall", releaseName, Wait: wait));

    /// <summary>Uninstalls a release with Helm CLI-compatible options.</summary>
    public void Uninstall(string releaseName, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool disableHooks = false, bool dryRun = false, bool ignoreNotFound = false, bool keepHistory = false, string cascade = "background", string? description = null) =>
        _ = Invoke(new NativeHelmRequest("uninstall", releaseName, Wait: wait, TimeoutMilliseconds: (long)(timeout?.TotalMilliseconds ?? 0), Namespace: namespaceName, DisableHooks: disableHooks, WaitStrategy: waitStrategy, DryRunUninstall: dryRun, IgnoreNotFound: ignoreNotFound, KeepHistory: keepHistory, Cascade: cascade, Description: description));

    /// <summary>Gets the values applied to a release as a JSON element.</summary>
    public JsonElement GetValues(string releaseName) =>
        GetValues(releaseName, _options.DefaultNamespace);

    /// <summary>Gets release values as a JSON element.</summary>
    public JsonElement GetValues(string releaseName, string? namespaceName = null, int revision = 0, bool all = false)
    {
        var response = Invoke(new NativeHelmRequest("values", releaseName, Namespace: namespaceName, Revision: revision, AllValues: all));
        if (response.Values is JsonElement values && values.ValueKind != JsonValueKind.Undefined)
            return values.Clone();

        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    /// <summary>Lists configured chart repositories.</summary>
    public IReadOnlyList<HelmRepository> ListRepositories() =>
        Invoke(new NativeHelmRequest("repoList")).Repositories ?? [];

    /// <summary>Lists configured chart repositories.</summary>
    public IReadOnlyList<HelmRepository> RepoList() =>
        Invoke(new NativeHelmRequest("repoList")).Repositories ?? [];

    /// <summary>Searches chart repository indexes.</summary>
    public IReadOnlyList<HelmChartSearchResult> SearchRepo(string keyword = "", bool versions = false, bool devel = false, bool regexp = false, string? version = null, bool failOnNoResult = false) =>
        Invoke(new NativeHelmRequest("chartSearch", SearchTerm: keyword, Versions: versions, Devel: devel, Regexp: regexp, VersionConstraint: version, FailOnNoResult: failOnNoResult)).ChartSearchResults ?? [];

    /// <summary>Adds a chart repository, optionally replacing an existing entry.</summary>
    public void RepoAdd(string name, string url, string? username = null, string? password = null, bool forceUpdate = false) =>
        _ = Invoke(new NativeHelmRequest("repoAdd", RepositoryName: name, RepositoryURL: url, RepositoryUsername: username, RepositoryPassword: password, ForceUpdate: forceUpdate));

    /// <summary>Removes a configured chart repository.</summary>
    public void RepoRemove(string repository) =>
        _ = Invoke(new NativeHelmRequest("repoRemove", RepositoryName: repository));

    /// <summary>Updates configured chart repository indexes.</summary>
    public void RepoUpdate() =>
        _ = Invoke(new NativeHelmRequest("repoUpdate"));

    /// <summary>Adds or updates a chart repository.</summary>
    public void AddRepository(string name, string url, string? username = null, string? password = null) =>
        _ = Invoke(new NativeHelmRequest("repoAdd", RepositoryName: name, RepositoryURL: url, RepositoryUsername: username, RepositoryPassword: password, ForceUpdate: true));

    /// <summary>Removes a configured chart repository.</summary>
    public void RemoveRepository(string name) =>
        _ = Invoke(new NativeHelmRequest("repoRemove", RepositoryName: name));

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    public void UpdateRepositories() =>
        _ = Invoke(new NativeHelmRequest("repoUpdate"));

    private static void ValidateSetValues(JsonElement? setValues)
    {
        if (setValues is JsonElement values && values.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("setValues must be a JSON object.", nameof(setValues));
    }

    private NativeHelmResponse Invoke(NativeHelmRequest request)
    {
        var response = _nativeApi.Execute(request with
        {
            Kubeconfig = _options.Kubeconfig,
            KubeContext = _options.KubeContext,
            Namespace = request.Namespace ?? _options.DefaultNamespace,
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
