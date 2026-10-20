using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Helm.NET;

var chartDirectory = Path.Combine(Path.GetTempPath(), $"helm-net-aot-{Guid.NewGuid():N}");
Directory.CreateDirectory(Path.Combine(chartDirectory, "templates"));
var clusterLifecycle = args.Contains("--cluster", StringComparer.Ordinal);

try
{
    File.WriteAllText(Path.Combine(chartDirectory, "Chart.yaml"), "apiVersion: v2\nname: aot-smoke\nversion: 1.0.0\n");
    File.WriteAllText(Path.Combine(chartDirectory, "values.yaml"), "setting: default\n");
    File.WriteAllText(Path.Combine(chartDirectory, "smoke-values.yaml"), "setting: from-file\n");
    File.WriteAllText(
        Path.Combine(chartDirectory, "templates", "configmap.yaml"),
        "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  setting: {{ .Values.setting | quote }}\n");

    var helm = new HelmClient(new HelmClientOptions { Namespace = "helm-net-aot" });
    if (clusterLifecycle)
    {
        RunClusterLifecycle(helm, chartDirectory);
    }
    else
    {
        var manifest = helm.Template("aot-smoke", chartDirectory);
        Require(manifest.Contains("name: aot-smoke", StringComparison.Ordinal), $"Unexpected rendered manifest:\n{manifest}");
    }

    Console.WriteLine(clusterLifecycle
        ? "NativeAOT Helm operation smoke test passed."
        : "NativeAOT Helm template smoke test passed.");
    return 0;
}
finally
{
    Directory.Delete(chartDirectory, recursive: true);
}

static void RunClusterLifecycle(HelmClient helm, string chartDirectory)
{
    const string releaseName = "aot-smoke";
    var manifest = helm.Template(
        releaseName,
        chartDirectory,
        valuesFiles: [Path.Combine(chartDirectory, "smoke-values.yaml")],
        values: new Dictionary<string, object?> { ["setting"] = "rendered" });
    Require(manifest.Contains("setting: \"rendered\"", StringComparison.Ordinal), "Template did not apply its supplied values.");

    var installed = helm.Install(
        releaseName,
        chartDirectory,
        values: new Dictionary<string, object?> { ["setting"] = "installed" },
        createNamespace: true,
        wait: true,
        timeout: TimeSpan.FromMinutes(1));
    Require(installed.Status == "deployed" && installed.Revision == "1", $"Unexpected install result: {installed}");
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

    var secondaryRelease = "aot-smoke-nowait";
    helm.Upgrade(secondaryRelease, chartDirectory, installIfMissing: true);
    Require(helm.ListReleases().Any(release => release.Name == secondaryRelease), "Upgrade with install-if-missing did not create a release.");
    helm.Uninstall(secondaryRelease);

    RunRepositoryOperations(chartDirectory);

    helm.Uninstall(releaseName, wait: true);
    Require(!helm.ListReleases().Any(release => release.Name == releaseName), "The release still appears after uninstall.");
}

static void RunRepositoryOperations(string chartDirectory)
{
    var repositoryDirectory = Path.Combine(chartDirectory, "repository-config");
    Directory.CreateDirectory(repositoryDirectory);
    using var repository = new LocalHelmRepository();
    var helm = new HelmClient(new HelmClientOptions
    {
        RepositoryConfig = Path.Combine(repositoryDirectory, "repositories.yaml"),
        RepositoryCache = Path.Combine(repositoryDirectory, "cache")
    });

    helm.AddRepository("aot-smoke", repository.Url);
    Require(helm.ListRepositories().Any(item => item.Name == "aot-smoke"), "The added chart repository is missing.");
    helm.UpdateRepositories();
    helm.RemoveRepository("aot-smoke");
    Require(!helm.ListRepositories().Any(item => item.Name == "aot-smoke"), "The removed chart repository is still listed.");
}

static void RequireValue(IReadOnlyDictionary<string, JsonElement> values, string expected)
{
    Require(values.TryGetValue("setting", out var setting) && setting.GetString() == expected, $"Unexpected release values: {values}");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class LocalHelmRepository : IDisposable
{
    private const string Index = "apiVersion: v1\nentries: {}\n";
    private readonly HttpListener _listener = new();
    private readonly Task _server;

    public LocalHelmRepository()
    {
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        Url = $"http://127.0.0.1:{port}";

        _listener.Prefixes.Add($"{Url}/");
        _listener.Start();
        _server = Task.Run(Serve);
    }

    public string Url { get; }

    public void Dispose()
    {
        _listener.Stop();
        _server.GetAwaiter().GetResult();
        _listener.Close();
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
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                return;
            }

            var body = Encoding.UTF8.GetBytes(Index);
            context.Response.ContentType = "application/yaml";
            context.Response.ContentLength64 = body.Length;
            context.Response.OutputStream.Write(body);
            context.Response.Close();
        }
    }
}
