namespace Helm.NET;

/// <summary>Identifies a Helm 4.3 command, including its required subcommand.</summary>
public enum HelmCommand
{
    /// <summary>Generates Bash shell completion.</summary>
    CompletionBash,
    /// <summary>Generates Fish shell completion.</summary>
    CompletionFish,
    /// <summary>Generates PowerShell shell completion.</summary>
    CompletionPowerShell,
    /// <summary>Generates Zsh shell completion.</summary>
    CompletionZsh,
    /// <summary>Creates a chart.</summary>
    Create,
    /// <summary>Builds chart dependencies.</summary>
    DependencyBuild,
    /// <summary>Lists chart dependencies.</summary>
    DependencyList,
    /// <summary>Updates chart dependencies.</summary>
    DependencyUpdate,
    /// <summary>Displays Helm environment information.</summary>
    Env,
    /// <summary>Displays all information for a release.</summary>
    GetAll,
    /// <summary>Displays release hooks.</summary>
    GetHooks,
    /// <summary>Displays a release manifest.</summary>
    GetManifest,
    /// <summary>Displays release metadata.</summary>
    GetMetadata,
    /// <summary>Displays release notes.</summary>
    GetNotes,
    /// <summary>Displays release values.</summary>
    GetValues,
    /// <summary>Displays release history.</summary>
    History,
    /// <summary>Installs a chart.</summary>
    Install,
    /// <summary>Checks a chart for issues.</summary>
    Lint,
    /// <summary>Lists releases.</summary>
    List,
    /// <summary>Packages a chart.</summary>
    Package,
    /// <summary>Installs a Helm plugin.</summary>
    PluginInstall,
    /// <summary>Lists Helm plugins.</summary>
    PluginList,
    /// <summary>Packages a Helm plugin.</summary>
    PluginPackage,
    /// <summary>Uninstalls Helm plugins.</summary>
    PluginUninstall,
    /// <summary>Updates Helm plugins.</summary>
    PluginUpdate,
    /// <summary>Verifies a Helm plugin.</summary>
    PluginVerify,
    /// <summary>Downloads a chart.</summary>
    Pull,
    /// <summary>Pushes a chart to a registry.</summary>
    Push,
    /// <summary>Logs in to an OCI registry.</summary>
    RegistryLogin,
    /// <summary>Logs out of an OCI registry.</summary>
    RegistryLogout,
    /// <summary>Adds a chart repository.</summary>
    RepoAdd,
    /// <summary>Creates a repository index.</summary>
    RepoIndex,
    /// <summary>Lists chart repositories.</summary>
    RepoList,
    /// <summary>Removes chart repositories.</summary>
    RepoRemove,
    /// <summary>Updates chart repository indexes.</summary>
    RepoUpdate,
    /// <summary>Rolls back a release.</summary>
    Rollback,
    /// <summary>Searches a chart hub.</summary>
    SearchHub,
    /// <summary>Searches chart repositories.</summary>
    SearchRepository,
    /// <summary>Displays all chart information.</summary>
    ShowAll,
    /// <summary>Displays a chart definition.</summary>
    ShowChart,
    /// <summary>Displays chart CRDs.</summary>
    ShowCrds,
    /// <summary>Displays a chart README.</summary>
    ShowReadme,
    /// <summary>Displays chart values.</summary>
    ShowValues,
    /// <summary>Displays release status.</summary>
    Status,
    /// <summary>Renders chart templates locally.</summary>
    Template,
    /// <summary>Runs tests for a release.</summary>
    Test,
    /// <summary>Uninstalls a release.</summary>
    Uninstall,
    /// <summary>Upgrades a release.</summary>
    Upgrade,
    /// <summary>Verifies a chart signature.</summary>
    Verify,
    /// <summary>Displays the Helm version.</summary>
    Version,
    /// <summary>Displays help.</summary>
    Help
}
