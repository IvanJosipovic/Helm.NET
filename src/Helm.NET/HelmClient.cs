using CliWrap;
using CliWrap.Buffered;

namespace Helm.NET;

/// <summary>Provides access to the Helm command-line interface.</summary>
public sealed class HelmClient
{
    private static readonly string[] VersionArguments = { "version" };
    private readonly HelmClientOptions _options;
    private readonly string _executable;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<HelmCommandResult>>? _executor;

    /// <summary>Initializes a client using the bundled or configured executable.</summary>
    /// <param name="options">Executable resolution and process execution settings.</param>
    public HelmClient(HelmClientOptions? options = null)
    {
        _options = options ?? new HelmClientOptions();
        _executable = !string.IsNullOrWhiteSpace(_options.ExecutablePath)
            ? _options.ExecutablePath!
            : HelmExecutableResolver.Resolve(_options, HelmPlatform.Current, cacheDirectory: _options.ExecutableCacheDirectory);
    }

    internal HelmClient(HelmClientOptions options, Func<IReadOnlyList<string>, CancellationToken, Task<HelmCommandResult>> executor)
        : this(options) => _executor = executor;

    /// <summary>Executes an arbitrary Helm command.</summary>
    /// <param name="arguments">The complete Helm command and its arguments.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) => ExecuteCoreAsync(arguments, cancellationToken);
    /// <summary>Executes a known Helm 4.3 command with its CLI arguments.</summary>
    /// <param name="command">The Helm command to execute.</param>
    /// <param name="arguments">The exact CLI tokens after the command path, including positional operands and flags. Each list item is passed as one argument without shell splitting; <see langword="null"/> means no additional arguments.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    /// <remarks>Argument tokens follow the syntax shown by <c>helm &lt;command&gt; --help</c>. Use <see cref="ExecuteAsync(IReadOnlyList{string}, CancellationToken)"/> for future or plugin-provided commands.</remarks>
    public Task<HelmCommandResult> ExecuteAsync(HelmCommand command, IReadOnlyList<string>? arguments = null, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.Build(command, arguments), cancellationToken);
    /// <summary>Creates a new chart directory.</summary>
    /// <param name="name">The directory and chart name to create.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> CreateChartAsync(string name, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.CreateChart(name), cancellationToken);
    /// <summary>Installs a chart as a release.</summary>
    /// <param name="options">The release name, chart, and installation settings.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> InstallAsync(HelmInstallOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.Install(options), cancellationToken);
    /// <summary>Upgrades a release.</summary>
    /// <param name="options">The release name, chart, and upgrade settings.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> UpgradeAsync(HelmUpgradeOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.Upgrade(options), cancellationToken);
    /// <summary>Uninstalls a release.</summary>
    /// <param name="releaseName">The name of the release to uninstall.</param>
    /// <param name="namespaceName">The namespace containing the release.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> UninstallAsync(string releaseName, string? namespaceName = null, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.Uninstall(releaseName, namespaceName), cancellationToken);
    /// <summary>Lists releases.</summary>
    /// <param name="options">Filters and formatting options for the release list.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> ListAsync(HelmListOptions? options = null, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.List(options ?? new HelmListOptions()), cancellationToken);
    /// <summary>Gets release status.</summary>
    /// <param name="releaseName">The name of the release.</param>
    /// <param name="namespaceName">The namespace containing the release.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<HelmCommandResult> StatusAsync(string releaseName, string? namespaceName = null, CancellationToken cancellationToken = default) => ExecuteAsync(HelmArgumentBuilder.Status(releaseName, namespaceName), cancellationToken);
    /// <summary>Rolls a release back to a revision (or its previous revision when revision is zero).</summary>
    public Task<HelmCommandResult> RollbackAsync(string releaseName, int revision = 0, string? namespaceName = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(HelmCommand.Rollback, AddNamespace(new[] { releaseName, revision.ToString(System.Globalization.CultureInfo.InvariantCulture) }, namespaceName), cancellationToken);
    /// <summary>Returns release revision history.</summary>
    public Task<HelmCommandResult> HistoryAsync(string releaseName, string? namespaceName = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(HelmCommand.History, AddNamespace(new[] { releaseName }, namespaceName), cancellationToken);
    /// <summary>Returns the values applied to a release.</summary>
    public Task<HelmCommandResult> GetValuesAsync(string releaseName, string? namespaceName = null, CancellationToken cancellationToken = default) =>
        ExecuteAsync(HelmCommand.GetValues, AddNamespace(new[] { releaseName }, namespaceName), cancellationToken);
    /// <summary>Checks a chart for issues.</summary>
    public Task<HelmCommandResult> LintAsync(string chart, CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.Lint, new[] { chart }, cancellationToken);
    /// <summary>Renders chart templates locally.</summary>
    public Task<HelmCommandResult> TemplateAsync(string releaseName, string chart, CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.Template, new[] { releaseName, chart }, cancellationToken);
    /// <summary>Adds a chart repository.</summary>
    public Task<HelmCommandResult> AddRepositoryAsync(string name, string url, CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.RepoAdd, new[] { name, url }, cancellationToken);
    /// <summary>Refreshes chart repository indexes.</summary>
    public Task<HelmCommandResult> UpdateRepositoriesAsync(CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.RepoUpdate, cancellationToken: cancellationToken);
    /// <summary>Lists configured chart repositories.</summary>
    public Task<HelmCommandResult> ListRepositoriesAsync(CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.RepoList, cancellationToken: cancellationToken);
    /// <summary>Removes a configured chart repository.</summary>
    public Task<HelmCommandResult> RemoveRepositoryAsync(string name, CancellationToken cancellationToken = default) => ExecuteAsync(HelmCommand.RepoRemove, new[] { name }, cancellationToken);
    /// <summary>Lists releases as strongly typed records.</summary>
    public async Task<IReadOnlyList<HelmRelease>> ListReleasesAsync(HelmListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var value = options ?? new HelmListOptions();
        var arguments = HelmArgumentBuilder.List(value with { Output = "json" });
        var json = (await ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false)).StandardOutput;
        return System.Text.Json.JsonSerializer.Deserialize(json, HelmJsonContext.Default.HelmReleaseArray) ?? [];
    }

    private static IReadOnlyList<string> AddNamespace(IReadOnlyList<string> arguments, string? namespaceName) =>
        string.IsNullOrWhiteSpace(namespaceName) ? arguments : arguments.Concat(new[] { "--namespace", namespaceName }).ToArray();
    /// <summary>Gets the installed Helm version.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(VersionArguments, cancellationToken).ConfigureAwait(false)).StandardOutput;
    /// <summary>Generates a shell completion script.</summary>
    /// <param name="shell">The shell name accepted by <c>helm completion</c>.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<string> GenerateCompletionAsync(string shell, CancellationToken cancellationToken = default) => (await ExecuteAsync(new[] { "completion", shell }, cancellationToken).ConfigureAwait(false)).StandardOutput;

    private async Task<HelmCommandResult> ExecuteCoreAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (_executor is not null) return await _executor(arguments, cancellationToken).ConfigureAwait(false);
        using var timeout = _options.CommandTimeout is null ? null : new CancellationTokenSource(_options.CommandTimeout.Value);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout?.Token ?? CancellationToken.None);
        try
        {
            var command = Cli.Wrap(_executable)
                .WithArguments(arguments)
                .WithWorkingDirectory(_options.WorkingDirectory ?? Environment.CurrentDirectory)
                .WithValidation(CommandResultValidation.None);
            if (_options.Environment is not null) command = command.WithEnvironmentVariables(_options.Environment);

            var result = await command.ExecuteBufferedAsync(linked.Token).ConfigureAwait(false);
            var helmResult = new HelmCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
            if (helmResult.ExitCode != 0)
                throw new HelmCommandException($"Helm command failed with exit code {helmResult.ExitCode}.", helmResult.ExitCode, helmResult.StandardOutput, helmResult.StandardError);
            return helmResult;
        }
        catch (OperationCanceledException ex)
        {
            throw new HelmCommandException("Helm command was cancelled or timed out.", null, null, null, ex);
        }
        catch (Exception ex) when (ex is not HelmCommandException)
        {
            throw new HelmCommandException($"Unable to execute Helm executable '{_executable}'.", null, null, null, ex);
        }
    }
}
