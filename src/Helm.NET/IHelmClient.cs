using System.Text.Json;

namespace Helm.NET;

/// <summary>Defines typed operations backed by Helm's Go action library.</summary>
public interface IHelmClient
{
    /// <summary>Lists releases, optionally across all namespaces or matching a filter.</summary>
    /// <param name="filter">Regular expression used to filter release names.</param>
    /// <param name="max">Maximum number of releases to return.</param>
    /// <param name="offset">Number of matching releases to skip.</param>
    /// <param name="selector">Label selector used to filter releases.</param>
    /// <param name="deployed">Includes deployed releases.</param>
    /// <param name="failed">Includes failed releases.</param>
    /// <param name="pending">Includes pending releases.</param>
    /// <param name="superseded">Includes superseded releases.</param>
    /// <param name="uninstalled">Includes uninstalled releases.</param>
    /// <param name="uninstalling">Includes uninstalling releases.</param>
    /// <param name="dateSort">Sorts releases by date.</param>
    /// <param name="reverse">Reverses the sort order.</param>
    /// <param name="allNamespaces">Lists releases across every namespace; when true, namespaceName does not limit results.</param>
    /// <param name="namespaceName">Namespace for this call, or the configured default namespace when omitted.</param>
    IReadOnlyList<HelmRelease> List(string? namespaceName = null, bool allNamespaces = false, string? filter = null, int max = 256, int offset = 0, string? selector = null, bool deployed = false, bool failed = false, bool pending = false, bool superseded = false, bool uninstalled = false, bool uninstalling = false, bool dateSort = false, bool reverse = false) => throw new NotSupportedException();

    /// <summary>Lists releases in the configured namespace.</summary>
    IReadOnlyList<HelmRelease> ListReleases() => List();

    /// <summary>Lists releases and optionally searches all namespaces.</summary>
    IReadOnlyList<HelmRelease> ListReleases(bool allNamespaces, string? filter = null) => List(namespaceName: null, allNamespaces: allNamespaces, filter: filter);

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null) => Template(releaseName, chart, namespaceName: null, values: valuesFiles, setValues: values);

    /// <summary>Renders a chart with Helm CLI-compatible template options.</summary>
    string Template(string name, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, string? version = null, IEnumerable<string>? apiVersions = null, string? kubeVersion = null, bool includeCrds = false, bool skipTests = false, bool dependencyUpdate = false, bool isUpgrade = false, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false) => throw new NotSupportedException();

    /// <summary>Returns a chart's default values.yaml content.</summary>
    string ShowValues(string chart, string? version = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false) => throw new NotSupportedException();

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    string GetManifest(string releaseName, string? namespaceName = null, int revision = 0) => throw new NotSupportedException();

    /// <summary>Installs a chart and returns the created release.</summary>
    HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null) => Install(releaseName, chart, namespaceName: null, values: valuesFiles, setValues: values, createNamespace: createNamespace, wait: wait, timeout: timeout);

    /// <summary>Installs a chart with Helm CLI-compatible options.</summary>
    HelmRelease Install(string name, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool dependencyUpdate = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, string? waitStrategy = null, bool replace = false, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false) => throw new NotSupportedException();

    /// <summary>Upgrades an installed release.</summary>
    HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null) => Upgrade(releaseName, chart, namespaceName: null, values: valuesFiles, setValues: values, install: installIfMissing, createNamespace: createNamespace, wait: wait, timeout: timeout);

    /// <summary>Upgrades a release with Helm CLI-compatible options.</summary>
    HelmRelease Upgrade(string release, string chart, string? namespaceName, IEnumerable<string>? values = null, JsonElement? setValues = null, bool install = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool resetValues = false, bool reuseValues = false, bool resetThenReuseValues = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, bool dependencyUpdate = false, string? waitStrategy = null, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, bool cleanupOnFail = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false) => throw new NotSupportedException();

    /// <summary>Gets the current release status.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="revision">Release revision, or zero for the latest revision.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    HelmRelease Status(string releaseName, string? namespaceName = null, int revision = 0) => throw new NotSupportedException();

    /// <summary>Gets the current release status.</summary>
    HelmRelease Status(string releaseName) => Status(releaseName, namespaceName: null);

    /// <summary>Gets release revision history.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="max">Maximum number of revisions to return.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    IReadOnlyList<HelmRelease> History(string releaseName, string? namespaceName = null, int max = 256) => throw new NotSupportedException();

    /// <summary>Gets release revision history.</summary>
    IReadOnlyList<HelmRelease> History(string releaseName) => History(releaseName, namespaceName: null, max: 256);

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    void Rollback(string releaseName, int revision, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool waitForJobs = false, bool disableHooks = false, string? dryRun = null, bool forceReplace = false, bool forceConflicts = false, bool cleanupOnFail = false, int historyMax = 10, string? description = null) => throw new NotSupportedException();

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    void Rollback(string releaseName, int revision = 0) => Rollback(releaseName, revision, namespaceName: null, wait: false, historyMax: 10);

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    void Rollback(string releaseName, int revision, bool wait) => Rollback(releaseName, revision, namespaceName: null, wait: wait, historyMax: 10);

    /// <summary>Uninstalls a release.</summary>
    void Uninstall(string releaseName, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool disableHooks = false, bool dryRun = false, bool ignoreNotFound = false, bool keepHistory = false, string cascade = "background", string? description = null) => throw new NotSupportedException();

    /// <summary>Uninstalls a release.</summary>
    void Uninstall(string releaseName) => Uninstall(releaseName, namespaceName: null);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    void Uninstall(string releaseName, bool wait) => Uninstall(releaseName, namespaceName: null, wait: wait);

    /// <summary>Gets the values applied to a release as a JSON element.</summary>
    JsonElement GetValues(string releaseName) => GetValues(releaseName, namespaceName: null, revision: 0, all: false);

    /// <summary>Gets release values as a JSON object.</summary>
    JsonElement GetValues(string releaseName, string? namespaceName = null, int revision = 0, bool all = false) => throw new NotSupportedException();

    /// <summary>Lists configured chart repositories.</summary>
    IReadOnlyList<HelmRepository> ListRepositories() => RepoList();

    /// <summary>Lists configured chart repositories.</summary>
    IReadOnlyList<HelmRepository> RepoList() => throw new NotSupportedException();

    /// <summary>Searches configured repository indexes for charts matching a Helm CLI search keyword.</summary>
    IReadOnlyList<HelmChartSearchResult> SearchRepo(string keyword = "", bool versions = false, bool devel = false, bool regexp = false, string? version = null, bool failOnNoResult = false) => throw new NotSupportedException();

    /// <summary>Adds a chart repository; replaces an existing entry only when forceUpdate is true.</summary>
    void RepoAdd(string name, string url, string? username = null, string? password = null, bool forceUpdate = false) => throw new NotSupportedException();

    /// <summary>Adds or updates a chart repository.</summary>
    void AddRepository(string name, string url, string? username = null, string? password = null) => RepoAdd(name, url, username, password, forceUpdate: true);

    /// <summary>Removes a configured chart repository.</summary>
    void RepoRemove(string repository) => throw new NotSupportedException();

    /// <summary>Removes a configured chart repository.</summary>
    void RemoveRepository(string name) => RepoRemove(name);

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    void RepoUpdate() => throw new NotSupportedException();

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    void UpdateRepositories() => RepoUpdate();
}
