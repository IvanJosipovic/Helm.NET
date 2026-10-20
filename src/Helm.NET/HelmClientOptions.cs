namespace Helm.NET;

/// <summary>Configures direct calls to Helm's Go action library.</summary>
public sealed class HelmClientOptions
{
    /// <summary>Path to the kubeconfig file, or <see langword="null"/> to use Helm's default.</summary>
    public string? Kubeconfig { get; init; }

    /// <summary>Kubernetes context name, or <see langword="null"/> to use the current context.</summary>
    public string? KubeContext { get; init; }

    /// <summary>Default namespace for Helm operations.</summary>
    public string Namespace { get; init; } = "default";

    /// <summary>Path to Helm's repository configuration file.</summary>
    public string? RepositoryConfig { get; init; }

    /// <summary>Path to Helm's repository cache directory.</summary>
    public string? RepositoryCache { get; init; }

    /// <summary>Path to Helm's OCI registry configuration file.</summary>
    public string? RegistryConfig { get; init; }

    /// <summary>Helm release storage driver, such as <c>secret</c> or <c>configmap</c>.</summary>
    public string? HelmDriver { get; init; }
}
