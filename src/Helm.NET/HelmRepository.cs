namespace Helm.NET;

/// <summary>A configured Helm chart repository.</summary>
/// <param name="Name">The repository's local name.</param>
/// <param name="Url">The repository index URL.</param>
public sealed record HelmRepository(string Name, string Url);
