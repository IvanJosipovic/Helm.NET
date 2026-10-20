using System.Text.Json.Serialization;

namespace Helm.NET;

/// <summary>A Helm release returned by the action library.</summary>
public sealed record HelmRelease(
    string Name,
    string Namespace,
    string Revision,
    string Updated,
    string Status,
    string Chart,
    [property: JsonPropertyName("app_version")] string AppVersion);

/// <summary>Represents a failed Helm action.</summary>
public sealed class HelmActionException : Exception
{
    /// <summary>Initializes an exception with the error returned by Helm.</summary>
    public HelmActionException(string message) : base(message) { }
}
