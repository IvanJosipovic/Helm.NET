using System.Text.Json.Serialization;
using System.Text.Json;

namespace Helm.NET;

/// <summary>A Helm release returned by the action library.</summary>
public sealed record HelmRelease(
    string Name,
    string Namespace,
    int Revision,
    DateTimeOffset Updated,
    string Status,
    string Chart,
    [property: JsonPropertyName("app_version")] string AppVersion,
    string ChartName = "",
    string ChartVersion = "")
{
    private IReadOnlyList<HelmReleaseHook> _hooks = [];
    /// <summary>Release description, including the Helm CLI status description.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Notes rendered by the chart for this release revision.</summary>
    public string Notes { get; init; } = string.Empty;
    /// <summary>Live resource objects returned by Helm status, grouped by resource type.</summary>
    public JsonElement? Resources { get; init; }
    /// <summary>Helm hook details, including test hooks and their last execution state.</summary>
    public IReadOnlyList<HelmReleaseHook> Hooks { get => _hooks; init => _hooks = value ?? []; }
}

/// <summary>Represents a Helm release hook and its last execution.</summary>
public sealed record HelmReleaseHook(string Name, string Kind, string Path, string Manifest, IReadOnlyList<string> Events, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string Phase, int Weight, IReadOnlyList<string> DeletePolicies);

/// <summary>Represents a failed Helm action.</summary>
public sealed class HelmActionException : Exception
{
    /// <summary>Initializes an exception with the error returned by Helm.</summary>
    public HelmActionException(string message) : base(message) { }
}
