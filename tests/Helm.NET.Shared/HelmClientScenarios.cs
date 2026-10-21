using System.Text.Json;
using Helm.NET;

namespace Helm.NET.Scenarios;

/// <summary>Operation scenarios shared by response-scripted tests and NativeAOT smoke hosts.</summary>
public static class HelmClientScenarios
{
    public static void Run(HelmClient helm, Func<HelmClientOptions, HelmClient> createClient, string chartDirectory, string repositoryUrl, bool clusterLifecycle)
    {
        RunTemplateAndRepositoryOperations(helm, createClient, chartDirectory, repositoryUrl);
        if (clusterLifecycle)
            RunClusterLifecycle(helm, chartDirectory);
    }

    public static void RunTemplateAndRepositoryOperations(HelmClient helm, Func<HelmClientOptions, HelmClient> createClient, string chartDirectory, string repositoryUrl)
    {
        var manifest = helm.Template(
            "aot-smoke",
            chartDirectory,
            valuesFiles: [Path.Combine(chartDirectory, "smoke-values.yaml")],
            values: new Dictionary<string, object?> { ["setting"] = "rendered" },
            version: "1.0.0");
        Require(manifest.Contains("name: aot-smoke", StringComparison.Ordinal), $"Unexpected rendered manifest:\n{manifest}");
        Require(manifest.Contains("setting: \"rendered\"", StringComparison.Ordinal), "Template did not apply its supplied values.");

        var repositoryDirectory = Path.Combine(chartDirectory, "repository-config");
        Directory.CreateDirectory(repositoryDirectory);
        var client = createClient(new HelmClientOptions
        {
            RepositoryConfig = Path.Combine(repositoryDirectory, "repositories.yaml"),
            RepositoryCache = Path.Combine(repositoryDirectory, "cache")
        });

        client.AddRepository("aot-smoke", repositoryUrl);
        Require(client.ListRepositories().Any(item => item.Name == "aot-smoke"), "The added chart repository is missing.");
        client.UpdateRepositories();
        var result = client.SearchCharts("aot-smoke").Single();
        Require(result.Reference == "aot-smoke/aot-smoke" && result.ChartName == "aot-smoke" && result.Version == "1.0.0", $"Unexpected chart search result: {result}");
        client.RemoveRepository("aot-smoke");
        Require(!client.ListRepositories().Any(item => item.Name == "aot-smoke"), "The removed chart repository is still listed.");
    }

    private static void RunClusterLifecycle(HelmClient helm, string chartDirectory)
    {
        const string releaseName = "aot-smoke";
        var installed = helm.Install(
            releaseName,
            chartDirectory,
            values: new Dictionary<string, object?> { ["setting"] = "installed" },
            createNamespace: true,
            wait: true,
            timeout: TimeSpan.FromMinutes(1));
        Require(installed.Status == "deployed" && installed.Revision == "1", $"Unexpected install result: {installed}");
        Require(installed.ChartName == "aot-smoke" && installed.ChartVersion == "1.0.0", $"Unexpected chart metadata: {installed}");
        Require(helm.GetManifest(releaseName).Contains("name: aot-smoke", StringComparison.Ordinal), "GetManifest did not return the stored release manifest.");
        Require(helm.TemplateRelease(releaseName).Contains("name: aot-smoke", StringComparison.Ordinal), "TemplateRelease did not render the stored chart.");
        Require(helm.ListReleases().Any(release => release.Name == releaseName), "The release is missing from the namespace list.");
        Require(helm.ListReleases(allNamespaces: true, filter: "^aot-smoke$").Any(release => release.Name == releaseName), "The release is missing from the filtered all-namespaces list.");
        Require(helm.Status(releaseName).Status == "deployed", "Status did not report the installed release as deployed.");
        RequireValue(helm.GetValues(releaseName), "installed");

        var upgraded = helm.Upgrade(
            releaseName,
            chartDirectory,
            values: new Dictionary<string, object?> { ["setting"] = "upgraded" },
            installIfMissing: true,
            wait: true);
        Require(upgraded.Status == "deployed" && upgraded.Revision == "2", $"Unexpected upgrade result: {upgraded}");
        RequireValue(helm.GetValues(releaseName), "upgraded");
        Require(helm.History(releaseName).Count >= 2, "Release history did not include the install and upgrade revisions.");

        helm.Rollback(releaseName, revision: 1, wait: true);
        RequireValue(helm.GetValues(releaseName), "installed");
        helm.Rollback(releaseName, revision: 2);
        RequireValue(helm.GetValues(releaseName), "upgraded");

        const string secondaryRelease = "aot-smoke-nowait";
        helm.Upgrade(secondaryRelease, chartDirectory, installIfMissing: true);
        Require(helm.ListReleases().Any(release => release.Name == secondaryRelease), "Upgrade with install-if-missing did not create a release.");
        helm.Uninstall(secondaryRelease);

        helm.Uninstall(releaseName, wait: true);
        Require(!helm.ListReleases().Any(release => release.Name == releaseName), "The release still appears after uninstall.");
    }

    private static void RequireValue(IReadOnlyDictionary<string, JsonElement> values, string expected) =>
        Require(values.TryGetValue("setting", out var setting) && setting.GetString() == expected, $"Unexpected release values: {values}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
