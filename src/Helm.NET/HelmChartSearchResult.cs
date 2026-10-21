using System.Text.Json.Serialization;

namespace Helm.NET;

/// <summary>A chart found in a configured repository index.</summary>
/// <param name="Reference">The resolvable repository-qualified chart reference.</param>
/// <param name="ChartName">The chart name.</param>
/// <param name="Version">An available chart version.</param>
/// <param name="Description">The chart description from the repository index, when available.</param>
/// <param name="AppVersion">The application version from the repository index, when available.</param>
public sealed record HelmChartSearchResult(string Reference, string ChartName, string Version, string? Description = null, [property: JsonPropertyName("app_version")] string? AppVersion = null);
