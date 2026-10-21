using System.Net;
using System.Net.Sockets;
using System.Text;
using Helm.NET;
using Helm.NET.Scenarios;

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
    using var repository = new LocalHelmRepository("apiVersion: v1\nentries:\n  aot-smoke:\n  - apiVersion: v2\n    name: aot-smoke\n    version: 1.0.0\n    urls:\n    - aot-smoke-1.0.0.tgz\n");
    HelmClientScenarios.Run(helm, options => new HelmClient(options), chartDirectory, repository.Url, clusterLifecycle);
    HelmClientScenarios.RunChartVersionScenarios(clusterLifecycle);

    Console.WriteLine(clusterLifecycle
        ? "NativeAOT Helm operation smoke test passed."
        : "NativeAOT Helm template smoke test passed.");
    return 0;
}
finally
{
    Directory.Delete(chartDirectory, recursive: true);
}

sealed class LocalHelmRepository : IDisposable
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
            catch (HttpListenerException)
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
