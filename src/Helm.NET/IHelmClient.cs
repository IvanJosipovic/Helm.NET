using System.Text.Json;

namespace Helm.NET;

/// <summary>Defines typed operations backed by Helm's Go action library.</summary>
public interface IHelmClient
{
    /// <summary>Lists releases in the configured namespace.</summary>
    IReadOnlyList<HelmRelease> ListReleases();

    /// <summary>Lists releases and optionally searches all namespaces.</summary>
    IReadOnlyList<HelmRelease> ListReleases(bool allNamespaces, string? filter = null);

    /// <summary>Renders a chart locally and returns the rendered manifest.</summary>
    string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, string? version = null);

    /// <summary>Renders the chart and values stored in an installed release revision.</summary>
    string TemplateRelease(string releaseName, int revision = 0);

    /// <summary>Gets the manifest stored in an installed release revision.</summary>
    string GetManifest(string releaseName, int revision = 0);

    /// <summary>Installs a chart and returns the created release.</summary>
    HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null);

    /// <summary>Upgrades an installed release.</summary>
    HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, IReadOnlyDictionary<string, object?>? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null);

    /// <summary>Gets the current release status.</summary>
    HelmRelease Status(string releaseName);

    /// <summary>Gets release revision history.</summary>
    IReadOnlyList<HelmRelease> History(string releaseName);

    /// <summary>Rolls a release back to the specified revision, or its previous revision when zero.</summary>
    void Rollback(string releaseName, int revision = 0);

    /// <summary>Rolls a release back and optionally waits for its resources to become ready.</summary>
    void Rollback(string releaseName, int revision, bool wait);

    /// <summary>Uninstalls a release.</summary>
    void Uninstall(string releaseName);

    /// <summary>Uninstalls a release and optionally waits for its resources to be deleted.</summary>
    void Uninstall(string releaseName, bool wait);

    /// <summary>Gets the values applied to a release.</summary>
    IReadOnlyDictionary<string, JsonElement> GetValues(string releaseName);

    /// <summary>Lists configured chart repositories.</summary>
    IReadOnlyList<HelmRepository> ListRepositories();

    /// <summary>Searches configured repository indexes for charts and their available versions.</summary>
    IReadOnlyList<HelmChartSearchResult> SearchCharts(string searchTerm);

    /// <summary>Adds or updates a chart repository.</summary>
    void AddRepository(string name, string url, string? username = null, string? password = null);

    /// <summary>Removes a configured chart repository.</summary>
    void RemoveRepository(string name);

    /// <summary>Downloads updated indexes for configured chart repositories.</summary>
    void UpdateRepositories();
}
