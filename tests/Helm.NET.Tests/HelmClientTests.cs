namespace Helm.NET.Tests;

using System.Runtime.InteropServices;

public sealed class HelmClientTests
{
    private static string[] Args(params string[] values) => values;
    private static readonly (HelmCommand Command, string[] Prefix)[] _s_helmCommands =
    [
        (HelmCommand.CompletionBash, Args("completion", "bash")),
        (HelmCommand.CompletionFish, Args("completion", "fish")),
        (HelmCommand.CompletionPowerShell, Args("completion", "powershell")),
        (HelmCommand.CompletionZsh, Args("completion", "zsh")),
        (HelmCommand.Create, Args("create")),
        (HelmCommand.DependencyBuild, Args("dependency", "build")),
        (HelmCommand.DependencyList, Args("dependency", "list")),
        (HelmCommand.DependencyUpdate, Args("dependency", "update")),
        (HelmCommand.Env, Args("env")),
        (HelmCommand.GetAll, Args("get", "all")),
        (HelmCommand.GetHooks, Args("get", "hooks")),
        (HelmCommand.GetManifest, Args("get", "manifest")),
        (HelmCommand.GetMetadata, Args("get", "metadata")),
        (HelmCommand.GetNotes, Args("get", "notes")),
        (HelmCommand.GetValues, Args("get", "values")),
        (HelmCommand.History, Args("history")),
        (HelmCommand.Install, Args("install")),
        (HelmCommand.Lint, Args("lint")),
        (HelmCommand.List, Args("list")),
        (HelmCommand.Package, Args("package")),
        (HelmCommand.PluginInstall, Args("plugin", "install")),
        (HelmCommand.PluginList, Args("plugin", "list")),
        (HelmCommand.PluginPackage, Args("plugin", "package")),
        (HelmCommand.PluginUninstall, Args("plugin", "uninstall")),
        (HelmCommand.PluginUpdate, Args("plugin", "update")),
        (HelmCommand.PluginVerify, Args("plugin", "verify")),
        (HelmCommand.Pull, Args("pull")),
        (HelmCommand.Push, Args("push")),
        (HelmCommand.RegistryLogin, Args("registry", "login")),
        (HelmCommand.RegistryLogout, Args("registry", "logout")),
        (HelmCommand.RepoAdd, Args("repo", "add")),
        (HelmCommand.RepoIndex, Args("repo", "index")),
        (HelmCommand.RepoList, Args("repo", "list")),
        (HelmCommand.RepoRemove, Args("repo", "remove")),
        (HelmCommand.RepoUpdate, Args("repo", "update")),
        (HelmCommand.Rollback, Args("rollback")),
        (HelmCommand.SearchHub, Args("search", "hub")),
        (HelmCommand.SearchRepository, Args("search", "repo")),
        (HelmCommand.ShowAll, Args("show", "all")),
        (HelmCommand.ShowChart, Args("show", "chart")),
        (HelmCommand.ShowCrds, Args("show", "crds")),
        (HelmCommand.ShowReadme, Args("show", "readme")),
        (HelmCommand.ShowValues, Args("show", "values")),
        (HelmCommand.Status, Args("status")),
        (HelmCommand.Template, Args("template")),
        (HelmCommand.Test, Args("test")),
        (HelmCommand.Uninstall, Args("uninstall")),
        (HelmCommand.Upgrade, Args("upgrade")),
        (HelmCommand.Verify, Args("verify")),
        (HelmCommand.Version, Args("version")),
        (HelmCommand.Help, Args("help"))
    ];
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string ShellPath => IsWindows
        ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
        : "/bin/sh";

    private static string[] ShellArguments(string command) => IsWindows
        ? Args("/c", command)
        : Args("-c", command);

    [Fact]
    public async Task TypedCommandOverloadCoversEveryHelmCommandAndPreservesArguments()
    {
        Enum.GetValues<HelmCommand>().Length.ShouldBe(_s_helmCommands.Length);

        foreach (var (command, prefix) in _s_helmCommands)
        {
            IReadOnlyList<string>? actual = null;
            var client = new HelmClient(new HelmClientOptions { UseBundledExecutable = false }, (arguments, _) =>
            {
                actual = arguments;
                return Task.FromResult(new HelmCommandResult(0, "", ""));
            });

            await client.ExecuteAsync(command, Args("operand with spaces", "--flag", "value with spaces"));

            actual.ShouldBe(prefix.Concat(Args("operand with spaces", "--flag", "value with spaces")));
        }
    }

    [Fact]
    public async Task TypedCommandOverloadRejectsUnknownCommand()
    {
        var client = new HelmClient(new HelmClientOptions { UseBundledExecutable = false }, (_, _) =>
            Task.FromResult(new HelmCommandResult(0, "", "")));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => client.ExecuteAsync((HelmCommand)int.MaxValue));
    }

    [Fact]
    public async Task TypedCommandOverloadSupportsCommandsWithoutArguments()
    {
        IReadOnlyList<string>? actual = null;
        var client = new HelmClient(new HelmClientOptions { UseBundledExecutable = false }, (arguments, _) =>
        {
            actual = arguments;
            return Task.FromResult(new HelmCommandResult(0, "", ""));
        });

        await client.ExecuteAsync(HelmCommand.Version);

        actual.ShouldBe(Args("version"));
    }

    [Fact]
    public async Task TypedOperationsBuildHelmCommands()
    {
        var calls = new List<IReadOnlyList<string>>();
        var client = new HelmClient(new HelmClientOptions { UseBundledExecutable = false }, (args, _) =>
        {
            calls.Add(args);
            return Task.FromResult(new HelmCommandResult(0, "helm output\n", ""));
        });

        await client.CreateChartAsync("my-chart");
        await client.InstallAsync(new HelmInstallOptions("release", "./my-chart", "apps"));
        await client.UpgradeAsync(new HelmUpgradeOptions("release", "./my-chart", InstallIfMissing: true));
        await client.UninstallAsync("release", "apps");
        await client.ListAsync(new HelmListOptions(AllNamespaces: true));
        await client.StatusAsync("release");
        (await client.GetVersionAsync()).ShouldBe("helm output\n");
        (await client.GenerateCompletionAsync("powershell")).ShouldBe("helm output\n");

        calls.Count.ShouldBe(8);
        calls[0].ShouldBe(Args("create", "my-chart"));
        calls[1].ShouldBe(Args("install", "release", "./my-chart", "--namespace", "apps"));
        calls[2].ShouldBe(Args("upgrade", "release", "./my-chart", "--install"));
        calls[3].ShouldBe(Args("uninstall", "release", "--namespace", "apps"));
        calls[4].ShouldBe(Args("list", "--all-namespaces"));
        calls[5].ShouldBe(Args("status", "release"));
        calls[6].ShouldBe(Args("version"));
        calls[7].ShouldBe(Args("completion", "powershell"));
    }

    [Fact]
    public async Task GenericExecutionPropagatesCommandFailure()
    {
        var exception = await Should.ThrowAsync<HelmCommandException>(() => new HelmClient(new HelmClientOptions { UseBundledExecutable = false }, (_, _) =>
            Task.FromException<HelmCommandResult>(new HelmCommandException("failed"))).ExecuteAsync(Args("version")));
        exception.Message.ShouldBe("failed");
    }

    [Fact]
    public async Task ExecuteAsyncCapturesSuccessfulProcessOutput()
    {
        var result = await new HelmClient(new HelmClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments("echo hello"));
        result.ExitCode.ShouldBe(0);
        result.StandardOutput!.ShouldContain("hello");
    }

    [Fact]
    public async Task ExecuteAsyncAppliesWorkingDirectoryAndEnvironment()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        try
        {
            var command = IsWindows ? "echo %HELM_NET_TEST%" : "printf '%s' \"$HELM_NET_TEST\"";
            var client = new HelmClient(new HelmClientOptions
            {
                ExecutablePath = ShellPath,
                WorkingDirectory = workingDirectory,
                Environment = new Dictionary<string, string?> { ["HELM_NET_TEST"] = "configured" }
            });

            var result = await client.ExecuteAsync(ShellArguments(command));

            result.StandardOutput.ShouldContain("configured");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsyncReportsNonzeroExitCodeAndOutput()
    {
        var command = IsWindows ? "echo error 1>&2 & exit 7" : "echo error >&2; exit 7";
        var exception = await Should.ThrowAsync<HelmCommandException>(() => new HelmClient(new HelmClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments(command)));
        exception.ExitCode.ShouldBe(7);
        exception.StandardError!.ShouldContain("error");
    }

    [Fact]
    public async Task ExecuteAsyncReportsCancellationAndStopsProcess()
    {
        var command = IsWindows ? "for /L %i in (0,0,1) do @rem" : "while true; do :; done";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var exception = await Should.ThrowAsync<HelmCommandException>(() =>
            new HelmClient(new HelmClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments(command), cancellation.Token));

        exception.Message.ShouldBe("Helm command was cancelled or timed out.");
        exception.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsyncStopsProcessWhenConfiguredTimeoutExpires()
    {
        var command = IsWindows ? "for /L %i in (0,0,1) do @rem" : "while true; do :; done";
        var client = new HelmClient(new HelmClientOptions
        {
            ExecutablePath = ShellPath,
            CommandTimeout = TimeSpan.FromMilliseconds(100)
        });

        var exception = await Should.ThrowAsync<HelmCommandException>(() => client.ExecuteAsync(ShellArguments(command)));

        exception.Message.ShouldBe("Helm command was cancelled or timed out.");
        exception.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsyncReportsUnstartableExecutable()
    {
        var exception = await Should.ThrowAsync<HelmCommandException>(() => new HelmClient(new HelmClientOptions { ExecutablePath = "missing-helm-executable" }).ExecuteAsync(Args("version")));
        exception.InnerException.ShouldNotBeNull();
    }
}
