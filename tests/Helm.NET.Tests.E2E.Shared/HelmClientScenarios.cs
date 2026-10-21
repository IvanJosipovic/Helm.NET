using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Helm.NET;

namespace Helm.NET.Scenarios;

/// <summary>Public API scenarios reused by managed and NativeAOT end-to-end hosts.</summary>
public static partial class HelmClientScenarios
{
    public static void RunAll(HelmClient helm, Func<HelmClientOptions, HelmClient> createClient, bool clusterLifecycle)
    {
        var chartDirectory = Path.Combine(Path.GetTempPath(), $"helm-net-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(chartDirectory, "templates"));
        try
        {
            File.WriteAllText(Path.Combine(chartDirectory, "Chart.yaml"), "apiVersion: v2\nname: aot-smoke\nversion: 1.0.0\n");
            File.WriteAllText(Path.Combine(chartDirectory, "values.yaml"), "setting: default\n");
            File.WriteAllText(Path.Combine(chartDirectory, "smoke-values.yaml"), "setting: from-file\n");
            File.WriteAllText(
                Path.Combine(chartDirectory, "templates", "configmap.yaml"),
                "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  setting: {{ .Values.setting | quote }}\n");

            using var repository = new LocalHelmRepository(
                "apiVersion: v1\nentries:\n  aot-smoke:\n  - apiVersion: v2\n    name: aot-smoke\n    version: 1.0.0\n    urls:\n    - aot-smoke-1.0.0.tgz\n");
            Run(helm, createClient, chartDirectory, repository.Url, clusterLifecycle);
            RunChartVersionScenarios(clusterLifecycle);
        }
        finally
        {
            Directory.Delete(chartDirectory, recursive: true);
        }
    }

    public static void Run(HelmClient helm, Func<HelmClientOptions, HelmClient> createClient, string chartDirectory, string repositoryUrl, bool clusterLifecycle)
    {
        RunTemplateAndRepositoryOperations(helm, createClient, chartDirectory, repositoryUrl);
        if (clusterLifecycle)
            RunClusterLifecycle(helm, chartDirectory);
    }

    internal sealed class LocalHelmRepository : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _server;
        private readonly string _index;

        public LocalHelmRepository(string index = "apiVersion: v1\nentries: {}\n")
        {
            _index = index;
            int port;
            using (var portReservation = new TcpListener(IPAddress.Loopback, 0))
            {
                portReservation.Start();
                port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
            }

            Url = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add($"{Url}/");
            _listener.Start();
            _server = Task.Run(Serve);
        }

        public string Url { get; }

        public void Dispose()
        {
            _listener.Stop();
            try
            {
                _server.GetAwaiter().GetResult();
            }
            finally
            {
                _listener.Close();
            }
        }

        private void Serve()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException exception) when (!_listener.IsListening || exception.ErrorCode == 995)
                {
                    return;
                }

                var body = Encoding.UTF8.GetBytes(_index);
                context.Response.ContentType = "application/yaml";
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body);
                context.Response.Close();
            }
        }
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
            timeout: TimeSpan.FromMinutes(1),
            version: "1.0.0");
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
            wait: true,
            version: "1.0.0");
        Require(upgraded.Status == "deployed" && upgraded.Revision == "2", $"Unexpected upgrade result: {upgraded}");
        RequireValue(helm.GetValues(releaseName), "upgraded");
        Require(helm.History(releaseName).Count >= 2, "Release history did not include the install and upgrade revisions.");

        helm.Rollback(releaseName, revision: 1, wait: true);
        RequireValue(helm.GetValues(releaseName), "installed");
        helm.Rollback(releaseName, revision: 2);
        RequireValue(helm.GetValues(releaseName), "upgraded");

        const string secondaryRelease = "aot-smoke-nowait";
        helm.Upgrade(secondaryRelease, chartDirectory, installIfMissing: true, version: "1.0.0");
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
