namespace Helm.NET;

internal static class HelmArgumentBuilder
{
    private static readonly Dictionary<HelmCommand, string[]> CommandPrefixes = new()
    {
        [HelmCommand.CompletionBash] = ["completion", "bash"],
        [HelmCommand.CompletionFish] = ["completion", "fish"],
        [HelmCommand.CompletionPowerShell] = ["completion", "powershell"],
        [HelmCommand.CompletionZsh] = ["completion", "zsh"],
        [HelmCommand.Create] = ["create"],
        [HelmCommand.DependencyBuild] = ["dependency", "build"],
        [HelmCommand.DependencyList] = ["dependency", "list"],
        [HelmCommand.DependencyUpdate] = ["dependency", "update"],
        [HelmCommand.Env] = ["env"],
        [HelmCommand.GetAll] = ["get", "all"],
        [HelmCommand.GetHooks] = ["get", "hooks"],
        [HelmCommand.GetManifest] = ["get", "manifest"],
        [HelmCommand.GetMetadata] = ["get", "metadata"],
        [HelmCommand.GetNotes] = ["get", "notes"],
        [HelmCommand.GetValues] = ["get", "values"],
        [HelmCommand.History] = ["history"],
        [HelmCommand.Install] = ["install"],
        [HelmCommand.Lint] = ["lint"],
        [HelmCommand.List] = ["list"],
        [HelmCommand.Package] = ["package"],
        [HelmCommand.PluginInstall] = ["plugin", "install"],
        [HelmCommand.PluginList] = ["plugin", "list"],
        [HelmCommand.PluginPackage] = ["plugin", "package"],
        [HelmCommand.PluginUninstall] = ["plugin", "uninstall"],
        [HelmCommand.PluginUpdate] = ["plugin", "update"],
        [HelmCommand.PluginVerify] = ["plugin", "verify"],
        [HelmCommand.Pull] = ["pull"],
        [HelmCommand.Push] = ["push"],
        [HelmCommand.RegistryLogin] = ["registry", "login"],
        [HelmCommand.RegistryLogout] = ["registry", "logout"],
        [HelmCommand.RepoAdd] = ["repo", "add"],
        [HelmCommand.RepoIndex] = ["repo", "index"],
        [HelmCommand.RepoList] = ["repo", "list"],
        [HelmCommand.RepoRemove] = ["repo", "remove"],
        [HelmCommand.RepoUpdate] = ["repo", "update"],
        [HelmCommand.Rollback] = ["rollback"],
        [HelmCommand.SearchHub] = ["search", "hub"],
        [HelmCommand.SearchRepository] = ["search", "repo"],
        [HelmCommand.ShowAll] = ["show", "all"],
        [HelmCommand.ShowChart] = ["show", "chart"],
        [HelmCommand.ShowCrds] = ["show", "crds"],
        [HelmCommand.ShowReadme] = ["show", "readme"],
        [HelmCommand.ShowValues] = ["show", "values"],
        [HelmCommand.Status] = ["status"],
        [HelmCommand.Template] = ["template"],
        [HelmCommand.Test] = ["test"],
        [HelmCommand.Uninstall] = ["uninstall"],
        [HelmCommand.Upgrade] = ["upgrade"],
        [HelmCommand.Verify] = ["verify"],
        [HelmCommand.Version] = ["version"],
        [HelmCommand.Help] = ["help"]
    };

    public static IReadOnlyList<string> Build(HelmCommand command, IReadOnlyList<string>? arguments)
    {
        if (!CommandPrefixes.TryGetValue(command, out var prefix))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported Helm command.");
        }

        var result = new List<string>(prefix.Length + (arguments?.Count ?? 0));
        result.AddRange(prefix);
        if (arguments is not null) result.AddRange(arguments);
        return result;
    }

    public static IReadOnlyList<string> CreateChart(string name) => new[] { "create", name };

    public static IReadOnlyList<string> Install(HelmInstallOptions options)
    {
        var arguments = new List<string> { "install", options.ReleaseName, options.Chart };
        AddCommonReleaseOptions(arguments, options.Namespace, options.ValuesFiles, options.SetValues, options.CreateNamespace, options.Wait, options.Timeout);
        return arguments;
    }

    public static IReadOnlyList<string> Upgrade(HelmUpgradeOptions options)
    {
        var arguments = new List<string> { "upgrade", options.ReleaseName, options.Chart };
        if (options.InstallIfMissing) arguments.Add("--install");
        AddCommonReleaseOptions(arguments, options.Namespace, options.ValuesFiles, options.SetValues, options.CreateNamespace, options.Wait, options.Timeout);
        return arguments;
    }

    public static IReadOnlyList<string> Uninstall(string releaseName, string? namespaceName)
    {
        var arguments = new List<string> { "uninstall", releaseName };
        Add(arguments, "--namespace", namespaceName);
        return arguments;
    }

    public static IReadOnlyList<string> List(HelmListOptions options)
    {
        var arguments = new List<string> { "list" };
        Add(arguments, "--namespace", options.Namespace);
        if (options.AllNamespaces) arguments.Add("--all-namespaces");
        Add(arguments, "--filter", options.Filter);
        Add(arguments, "--output", options.Output);
        return arguments;
    }

    public static IReadOnlyList<string> Status(string releaseName, string? namespaceName)
    {
        var arguments = new List<string> { "status", releaseName };
        Add(arguments, "--namespace", namespaceName);
        return arguments;
    }

    private static void AddCommonReleaseOptions(
        List<string> arguments,
        string? namespaceName,
        IEnumerable<string>? valuesFiles,
        IEnumerable<string>? setValues,
        bool createNamespace,
        bool wait,
        TimeSpan? timeout)
    {
        Add(arguments, "--namespace", namespaceName);
        AddMany(arguments, "--values", valuesFiles);
        AddMany(arguments, "--set", setValues);
        if (createNamespace) arguments.Add("--create-namespace");
        if (wait) arguments.Add("--wait");
        if (timeout is not null)
        {
            arguments.Add("--timeout");
            arguments.Add(ToGoDuration(timeout.Value));
        }
    }

    private static void Add(List<string> arguments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        arguments.Add(name);
        arguments.Add(value);
    }

    private static void AddMany(List<string> arguments, string name, IEnumerable<string>? values)
    {
        if (values is null) return;
        foreach (var value in values)
        {
            arguments.Add(name);
            arguments.Add(value);
        }
    }

    private static string ToGoDuration(TimeSpan value) => value.TotalHours >= 1
        ? $"{value.TotalHours.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}h"
        : value.TotalMinutes >= 1
            ? $"{value.TotalMinutes.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}m"
            : $"{value.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s";
}
