using System.Text.Json.Serialization;

namespace Helm.NET;

/// <summary>Configures a <see cref="HelmClient"/>.</summary>
public sealed class HelmClientOptions
{
    /// <summary>Gets the explicit Helm executable path.</summary>
    /// <value>The executable path, or <see langword="null"/> to use the bundled executable or PATH.</value>
    public string? ExecutablePath { get; init; }
    /// <summary>Gets whether the bundled executable is preferred.</summary>
    /// <value><see langword="true"/> to use the platform-specific bundled binary when available.</value>
    public bool UseBundledExecutable { get; init; } = true;
    /// <summary>Gets the directory used to cache an extracted bundled executable.</summary>
    /// <value>The cache directory, or <see langword="null"/> to use the default cache location.</value>
    public string? ExecutableCacheDirectory { get; init; }
    /// <summary>Gets the command working directory.</summary>
    /// <value>The working directory, or <see langword="null"/> to use the current directory.</value>
    public string? WorkingDirectory { get; init; }
    /// <summary>Gets the command timeout.</summary>
    /// <value>The maximum command duration, or <see langword="null"/> for no timeout.</value>
    public TimeSpan? CommandTimeout { get; init; }
    /// <summary>Gets environment variables added to Helm processes.</summary>
    /// <value>Environment variable overrides applied to each child process.</value>
    public IReadOnlyDictionary<string, string?>? Environment { get; init; }
}

/// <summary>Options for installing a chart as a Helm release.</summary>
/// <param name="ReleaseName">The release name to install.</param>
/// <param name="Chart">The chart reference, archive, or local chart directory.</param>
/// <param name="Namespace">The Kubernetes namespace for the release.</param>
/// <param name="ValuesFiles">Values files, each emitted as a repeated <c>--values</c> option.</param>
/// <param name="SetValues">Values assignments, each emitted as a repeated <c>--set</c> option.</param>
/// <param name="CreateNamespace">Whether to create the namespace if it does not exist.</param>
/// <param name="Wait">Whether to wait for resources to become ready.</param>
/// <param name="Timeout">The maximum wait duration.</param>
public sealed record HelmInstallOptions(
    string ReleaseName,
    string Chart,
    string? Namespace = null,
    IEnumerable<string>? ValuesFiles = null,
    IEnumerable<string>? SetValues = null,
    bool CreateNamespace = false,
    bool Wait = false,
    TimeSpan? Timeout = null);

/// <summary>Options for upgrading a Helm release.</summary>
/// <param name="ReleaseName">The release name to upgrade.</param>
/// <param name="Chart">The chart reference, archive, or local chart directory.</param>
/// <param name="Namespace">The Kubernetes namespace for the release.</param>
/// <param name="ValuesFiles">Values files, each emitted as a repeated <c>--values</c> option.</param>
/// <param name="SetValues">Values assignments, each emitted as a repeated <c>--set</c> option.</param>
/// <param name="CreateNamespace">Whether to create the namespace if it does not exist.</param>
/// <param name="Wait">Whether to wait for resources to become ready.</param>
/// <param name="Timeout">The maximum wait duration.</param>
/// <param name="InstallIfMissing">Whether to install the release when it does not exist.</param>
public sealed record HelmUpgradeOptions(
    string ReleaseName,
    string Chart,
    string? Namespace = null,
    IEnumerable<string>? ValuesFiles = null,
    IEnumerable<string>? SetValues = null,
    bool CreateNamespace = false,
    bool Wait = false,
    TimeSpan? Timeout = null,
    bool InstallIfMissing = false);

/// <summary>Options for listing Helm releases.</summary>
/// <param name="Namespace">The namespace to list, or <see langword="null"/> to use Helm's current namespace.</param>
/// <param name="AllNamespaces">Whether to list releases from all namespaces.</param>
/// <param name="Filter">A Perl-compatible regular expression used to filter release names.</param>
/// <param name="Output">The output format supported by Helm, such as <c>table</c>, <c>json</c>, or <c>yaml</c>.</param>
public sealed record HelmListOptions(
    string? Namespace = null,
    bool AllNamespaces = false,
    string? Filter = null,
    string? Output = null);

/// <summary>A release returned by <c>helm list --output json</c>.</summary>
public sealed record HelmRelease(
    string Name,
    string Namespace,
    string Revision,
    string Updated,
    string Status,
    string Chart,
    [property: JsonPropertyName("app_version")] string AppVersion);

/// <summary>The result of a Helm command.</summary>
/// <param name="ExitCode">The command's process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record HelmCommandResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Represents a failed Helm command.</summary>
public sealed class HelmCommandException : Exception
{
    /// <summary>Initializes a command exception with captured process details.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="exitCode">The command exit code, if the process started and exited.</param>
    /// <param name="standardOutput">Captured standard output, if available.</param>
    /// <param name="standardError">Captured standard error, if available.</param>
    /// <param name="innerException">The exception that caused the command failure, if any.</param>
    public HelmCommandException(string message, int? exitCode = null, string? standardOutput = null, string? standardError = null, Exception? innerException = null)
        : base(message, innerException) => (ExitCode, StandardOutput, StandardError) = (exitCode, standardOutput, standardError);

    /// <summary>Gets the process exit code.</summary>
    public int? ExitCode { get; }
    /// <summary>Gets captured standard output.</summary>
    public string? StandardOutput { get; }
    /// <summary>Gets captured standard error.</summary>
    public string? StandardError { get; }
}
