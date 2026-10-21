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
                "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  setting: {{ .Values.setting | quote }}\n  namespace: {{ .Release.Namespace | quote }}\n  customApi: {{ .Capabilities.APIVersions.Has \"example.io/v1\" | quote }}\n  kubeVersion: {{ .Capabilities.KubeVersion.Version | quote }}\n  upgrade: {{ .Release.IsUpgrade | quote }}\n");

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
        private int _stopping;

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
            Interlocked.Exchange(ref _stopping, 1);
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
                catch (HttpListenerException) when (Volatile.Read(ref _stopping) != 0)
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
            values: [Path.Combine(chartDirectory, "smoke-values.yaml")],
            setValues: ParseJsonElement("""{"setting":"rendered"}"""),
            version: "1.0.0",
            namespaceName: "render-preview",
            apiVersions: ["example.io/v1"],
            kubeVersion: "1.30.0",
            dependencyUpdate: true,
            isUpgrade: true);
        Require(manifest.Contains("name: aot-smoke", StringComparison.Ordinal), $"Unexpected rendered manifest:\n{manifest}");
        Require(manifest.Contains("setting: \"rendered\"", StringComparison.Ordinal), "Template did not apply its supplied values.");
        Require(manifest.Contains("namespace: \"render-preview\"", StringComparison.Ordinal), "Template did not use its namespace override.");
        Require(manifest.Contains("customApi: \"true\"", StringComparison.Ordinal), "Template did not use the supplied API versions.");
        Require(manifest.Contains("kubeVersion: \"v1.30.0\"", StringComparison.Ordinal), "Template did not use its Kubernetes version override.");
        Require(manifest.Contains("upgrade: \"true\"", StringComparison.Ordinal), "Template did not set the upgrade release context.");

        var repositoryDirectory = Path.Combine(chartDirectory, "repository-config");
        Directory.CreateDirectory(repositoryDirectory);
        var client = createClient(new HelmClientOptions
        {
            RepositoryConfig = Path.Combine(repositoryDirectory, "repositories.yaml"),
            RepositoryCache = Path.Combine(repositoryDirectory, "cache")
        });

        client.RepoAdd("aot-smoke", repositoryUrl);
        Require(client.RepoList().Any(item => item.Name == "aot-smoke"), "The added chart repository is missing.");
        client.RepoUpdate();
        var result = client.SearchRepo("aot-smoke").Single();
        Require(result.Reference == "aot-smoke/aot-smoke" && result.ChartName == "aot-smoke" && result.Version == "1.0.0", $"Unexpected chart search result: {result}");
        client.RepoRemove("aot-smoke");
        Require(!client.RepoList().Any(item => item.Name == "aot-smoke"), "The removed chart repository is still listed.");
    }

    private static void RunClusterLifecycle(HelmClient helm, string chartDirectory)
    {
        const string releaseName = "aot-smoke";
        var installed = helm.Install(
            releaseName,
            chartDirectory,
            setValues: ParseJsonElement("""{"setting":"installed"}"""),
            createNamespace: true,
            wait: true,
            waitStrategy: "watcher",
            takeOwnership: true,
            timeout: TimeSpan.FromMinutes(1),
            version: "1.0.0");
        Require(installed.Status == "deployed" && installed.Revision == "1", $"Unexpected install result: {installed}");
        Require(installed.ChartName == "aot-smoke" && installed.ChartVersion == "1.0.0", $"Unexpected chart metadata: {installed}");
        Require(helm.GetManifest(releaseName).Contains("name: aot-smoke", StringComparison.Ordinal), "GetManifest did not return the stored release manifest.");
        Require(helm.List().Any(release => release.Name == releaseName), "The release is missing from the namespace list.");
        Require(helm.List(allNamespaces: true, filter: "^aot-smoke$").Any(release => release.Name == releaseName), "The release is missing from the filtered all-namespaces list.");
        Require(helm.Status(releaseName).Status == "deployed", "Status did not report the installed release as deployed.");
        RequireValue(helm.GetValues(releaseName), "installed");

        var upgraded = helm.Upgrade(
            releaseName,
            chartDirectory,
            setValues: ParseJsonElement("""{"setting":"upgraded"}"""),
            install: true,
            wait: true,
            waitStrategy: "watcher",
            version: "1.0.0");
        Require(upgraded.Status == "deployed" && upgraded.Revision == "2", $"Unexpected upgrade result: {upgraded}");
        RequireValue(helm.GetValues(releaseName), "upgraded");
        Require(helm.History(releaseName).Count >= 2, "Release history did not include the install and upgrade revisions.");

        helm.Rollback(releaseName, revision: 1, wait: true, waitStrategy: "watcher", timeout: TimeSpan.FromMinutes(1), waitForJobs: true, historyMax: 10, description: "scenario rollback");
        RequireValue(helm.GetValues(releaseName), "installed");
        helm.Rollback(releaseName, revision: 2, dryRun: "client");
        RequireValue(helm.GetValues(releaseName), "installed");
        helm.Rollback(releaseName, revision: 2);
        RequireValue(helm.GetValues(releaseName), "upgraded");

        const string secondaryRelease = "aot-smoke-nowait";
        helm.Upgrade(secondaryRelease, chartDirectory, install: true, version: "1.0.0");
        Require(helm.List().Any(release => release.Name == secondaryRelease), "Upgrade with install-if-missing did not create a release.");
        helm.Uninstall(secondaryRelease, dryRun: true, cascade: "foreground");
        Require(helm.List().Any(release => release.Name == secondaryRelease), "Uninstall dry-run changed the release.");
        helm.Uninstall(secondaryRelease, ignoreNotFound: true);
        helm.Uninstall(secondaryRelease, ignoreNotFound: true);

        helm.Uninstall(releaseName, wait: true, waitStrategy: "watcher", timeout: TimeSpan.FromMinutes(1), cascade: "foreground");
        Require(!helm.List().Any(release => release.Name == releaseName), "The release still appears after uninstall.");
    }

    private static void RequireValue(JsonElement values, string expected) =>
        Require(values.ValueKind == JsonValueKind.Object && values.TryGetProperty("setting", out var setting) && setting.GetString() == expected, $"Unexpected release values: {values}");

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
