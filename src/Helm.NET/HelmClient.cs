using System.Diagnostics;

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
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows()) startInfo.KillOnParentExit = true;
        else if (OperatingSystem.IsLinux()) startInfo.KillOnParentExit = true;

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = startInfo };
        if (_options.Environment is not null) foreach (var pair in _options.Environment) process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
        try { process.Start(); } catch (Exception ex) { throw new HelmCommandException($"Unable to start Helm executable '{_executable}'.", null, null, null, ex); }
        using var timeout = _options.CommandTimeout is null ? null : new CancellationTokenSource(_options.CommandTimeout.Value);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout?.Token ?? CancellationToken.None);
        var output = process.ReadAllTextAsync(CancellationToken.None);
        try { await process.WaitForExitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception killException) when (killException is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            var (standardOutput, standardError) = await output.ConfigureAwait(false);
            throw new HelmCommandException("Helm command was cancelled or timed out.", null, standardOutput, standardError, ex);
        }
        var (capturedOutput, capturedError) = await output.ConfigureAwait(false);
        var result = new HelmCommandResult(process.ExitCode, capturedOutput, capturedError);
        if (result.ExitCode != 0) throw new HelmCommandException($"Helm command failed with exit code {result.ExitCode}.", result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }
}
