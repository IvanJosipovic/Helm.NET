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
    string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null);

    /// <summary>Installs a chart and returns the created release.</summary>
    HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null);

    /// <summary>Upgrades an installed release.</summary>
    HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null);

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

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    void Rollback(string releaseName, int revision, bool wait);

    /// <summary>Uninstalls a release.</summary>
    void Uninstall(string releaseName, string? namespaceName = null, bool wait = false, string? waitStrategy = null, TimeSpan? timeout = null, bool disableHooks = false, bool dryRun = false, bool ignoreNotFound = false, bool keepHistory = false, string cascade = "background", string? description = null);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    void Uninstall(string releaseName, bool wait);

    /// <summary>Gets the values applied to a release as a JSON element.</summary>
    JsonElement GetValues(string releaseName);

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
