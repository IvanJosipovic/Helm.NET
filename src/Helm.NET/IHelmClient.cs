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
    IReadOnlyList<HelmRelease> List(string? namespaceName = null, bool allNamespaces = false, string? filter = null, int max = 256, int offset = 0, string? selector = null, bool deployed = false, bool failed = false, bool pending = false, bool superseded = false, bool uninstalled = false, bool uninstalling = false, bool dateSort = false, bool reverse = false);

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    /// <param name="name">The release name passed as the Helm CLI name argument.</param>
    /// <param name="chart">The chart argument or reference.</param>
    /// <param name="namespaceName">The release namespace passed through Helm CLI <c>--namespace</c>.</param>
    /// <param name="values">Values files passed through Helm CLI <c>--values</c>.</param>
    /// <param name="setValues">Inline values passed through Helm CLI <c>--set</c>.</param>
    /// <param name="version">The chart version passed through Helm CLI <c>--version</c>.</param>
    /// <param name="apiVersions">Kubernetes API versions used for chart capabilities.</param>
    /// <param name="kubeVersion">Kubernetes version used for chart capabilities.</param>
    /// <param name="includeCrds">Includes CRDs in rendered output.</param>
    /// <param name="skipTests">Omits test hooks from rendered output.</param>
    /// <param name="dependencyUpdate">Updates missing chart dependencies before rendering.</param>
    /// <param name="isUpgrade">Sets the rendered release context to upgrade.</param>
    /// <param name="dryRun">Selects client or server dry-run behavior.</param>
    /// <param name="hideSecret">Hides rendered Secret data.</param>
    /// <param name="disableHooks">Omits hooks from rendered output.</param>
    /// <param name="postRenderer">Post-renderer executable.</param>
    /// <param name="postRendererArgs">Arguments passed to the post-renderer.</param>
    /// <param name="repo">Chart repository URL passed through Helm CLI <c>--repo</c>.</param>
    /// <param name="username">Chart repository username.</param>
    /// <param name="password">Chart repository password.</param>
    /// <param name="caFile">CA bundle used to verify the chart repository.</param>
    /// <param name="certFile">Client certificate used for chart repository access.</param>
    /// <param name="keyFile">Client key used for chart repository access.</param>
    /// <param name="insecureSkipTlsVerify">Skips TLS certificate verification for chart downloads.</param>
    /// <param name="passCredentials">Passes chart repository credentials to all domains.</param>
    /// <param name="plainHttp">Uses plain HTTP for chart downloads.</param>
    string Template(string name, string chart, string? namespaceName = null, IEnumerable<string>? values = null, IReadOnlyDictionary<string, object?>? setValues = null, string? version = null, IEnumerable<string>? apiVersions = null, string? kubeVersion = null, bool includeCrds = false, bool skipTests = false, bool dependencyUpdate = false, bool isUpgrade = false, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false);

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    /// <param name="revision">Release revision, or zero for the latest revision.</param>
    string GetManifest(string releaseName, string? namespaceName = null, int revision = 0);

    /// <summary>Installs a chart with Helm CLI-compatible install options.</summary>
    HelmRelease Install(string name, string chart, string? namespaceName = null, IEnumerable<string>? values = null, IReadOnlyDictionary<string, object?>? setValues = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool dependencyUpdate = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, string? waitStrategy = null, bool replace = false, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false);

    /// <summary>Upgrades a chart with Helm CLI-compatible upgrade options.</summary>
    HelmRelease Upgrade(string release, string chart, string? namespaceName = null, IEnumerable<string>? values = null, IReadOnlyDictionary<string, object?>? setValues = null, bool install = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null, string? version = null, string? dryRun = null, bool hideSecret = false, bool disableHooks = false, string? postRenderer = null, IEnumerable<string>? postRendererArgs = null, bool waitForJobs = false, bool rollbackOnFailure = false, bool resetValues = false, bool reuseValues = false, bool resetThenReuseValues = false, bool skipCrds = false, bool devel = false, bool skipSchemaValidation = false, bool disableOpenApiValidation = false, string? description = null, bool dependencyUpdate = false, string? waitStrategy = null, bool forceReplace = false, bool forceConflicts = false, bool takeOwnership = false, bool cleanupOnFail = false, string? repo = null, string? username = null, string? password = null, string? caFile = null, string? certFile = null, string? keyFile = null, bool insecureSkipTlsVerify = false, bool passCredentials = false, bool plainHttp = false);

    /// <summary>Gets the current release status.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="revision">Release revision, or zero for the latest revision.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    HelmRelease Status(string releaseName, string? namespaceName = null, int revision = 0);

    /// <summary>Gets release revision history.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="max">Maximum number of revisions to return.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    IReadOnlyList<HelmRelease> History(string releaseName, string? namespaceName = null, int max = 256);

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    void Rollback(string releaseName, int revision = 0, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool waitForJobs = false, bool disableHooks = false, string? dryRun = null, bool forceReplace = false, bool forceConflicts = false, bool cleanupOnFail = false, int historyMax = 10, string? description = null);

    /// <summary>Uninstalls a release.</summary>
    void Uninstall(string releaseName, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool disableHooks = false, bool dryRun = false, bool ignoreNotFound = false, bool keepHistory = false, string cascade = "background", string? description = null);

    /// <summary>Gets the values applied to a release.</summary>
    /// <param name="releaseName">Release name.</param>
    /// <param name="revision">Release revision, or zero for the latest revision.</param>
    /// <param name="all">Returns all computed values when true.</param>
    /// <param name="namespaceName">Release namespace, or the configured default namespace when omitted.</param>
    IReadOnlyDictionary<string, JsonElement> GetValues(string releaseName, string? namespaceName = null, int revision = 0, bool all = false);

    /// <summary>Lists configured chart repositories.</summary>
    IReadOnlyList<HelmRepository> RepoList();

    /// <summary>Searches configured repository indexes for charts matching a Helm CLI search keyword.</summary>
    IReadOnlyList<HelmChartSearchResult> SearchRepo(string keyword = "", bool versions = false, bool devel = false, bool regexp = false, string? version = null, bool failOnNoResult = false);

    /// <summary>Adds a chart repository; replaces an existing entry only when forceUpdate is true.</summary>
    void RepoAdd(string name, string url, string? username = null, string? password = null, bool forceUpdate = false);

    /// <summary>Removes a configured chart repository.</summary>
    void RepoRemove(string repository);

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    void RepoUpdate();
}
