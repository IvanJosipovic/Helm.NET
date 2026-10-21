using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Helm.NET;

namespace Helm.NET.Scenarios;

/// <summary>Real-native chart catalog and version scenarios used by the E2E hosts.</summary>
public static partial class HelmClientScenarios
{
    public static void RunChartVersionScenarios(bool clusterLifecycle)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"helm-net-versions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var repository = new VersionedChartRepository();
            var options = new HelmClientOptions
            {
                DefaultNamespace = $"helm-net-versions-{Guid.NewGuid():N}",
                RepositoryConfig = Path.Combine(directory, "repositories.yaml"),
                RepositoryCache = Path.Combine(directory, "cache"),
                RegistryConfig = Path.Combine(directory, "registry.json")
            };
            var helm = new HelmClient(options);
            helm.RepoAdd("versions", repository.Url);
            helm.RepoAdd("versions-extra", repository.Url);
            helm.RepoUpdate();
            var requestCountAfterUpdate = repository.RequestCount;
            var search = helm.SearchRepo("extra", versions: true);
            Require(search.Count == 7 && search.Take(4).All(chart => chart.ChartName == "versioned-extra") && search.Skip(4).All(chart => chart.ChartName == "versioned"), "Search with versions enabled did not match and rank chart-name results ahead of repository-name results like Helm CLI.");
            Require(repository.RequestCount == requestCountAfterUpdate, "Search or listing accessed the remote repository instead of cached indexes.");

            var valuesFile = Path.Combine(directory, "values.yaml");
            File.WriteAllText(valuesFile, "setting: from-file\nretained: from-file\n");
            var values = ParseJsonElement("""{"setting":"inline","enabled":true,"replicas":2}""");
            const string chartReference = "versions/versioned";
            string Preview(string name, string? version) =>
                helm.Template(name, chartReference, values: [valuesFile], setValues: values, version: version);

            foreach (var version in VersionedChartRepository.Versions)
                CheckVersionManifest(Preview("preview", version), version);
            CheckVersionManifest(Preview("preview", null), "3.0.0");
            CheckVersionManifest(Preview("preview", ">=1.0.0 <3.0.0"), "2.0.0");
            var defaults = helm.Template("defaults", chartReference);
            Require(defaults.Contains("version: \"3.0.0\"", StringComparison.Ordinal) && defaults.Contains("setting: \"default\"", StringComparison.Ordinal), "Omitted template options did not preserve defaults.");
            var fileOnly = helm.Template("file-only", chartReference, values: [valuesFile], version: "1.0.0");
            Require(fileOnly.Contains("version: \"1.0.0\"", StringComparison.Ordinal) && fileOnly.Contains("setting: \"from-file\"", StringComparison.Ordinal), "Versioned template did not apply file-only values.");

            // Local archives and direct URLs are already resolved: Helm ignores version selection.
            var archive = Path.Combine(directory, "versioned-1.0.0.tgz");
            File.WriteAllBytes(archive, repository.OlderChart);
            CheckVersionManifest(helm.Template("archive", archive, values: [valuesFile], setValues: values, version: "99.0.0"), "1.0.0");
            CheckVersionManifest(helm.Template("url", $"{repository.Url}/versioned-1.0.0.tgz", values: [valuesFile], setValues: values, version: "99.0.0"), "1.0.0");

            foreach (var version in new[] { "99.0.0", "not-a-version" })
            {
                RequireActionFailure(() => Preview("missing", version), version);
                RequireActionFailure(() => helm.Install("missing", chartReference, version: version), version);
                RequireActionFailure(() => helm.Upgrade("missing", chartReference, install: true, version: version), version);
            }

            if (clusterLifecycle)
                RunVersionedDeployments(helm, chartReference, valuesFile, values, Preview);

            helm.RepoRemove("versions");
            helm.RepoRemove("versions-extra");
            Require(helm.RepoList().Count == 0, "Versioned repositories were not removed.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Exercises the public interface under NativeAOT.")]
    private static void RunVersionedDeployments(
        IHelmClient helm, string chart, string valuesFile, JsonElement values,
        Func<string, string?, string> preview)
    {
        var installed = new List<string>();
        try
        {
            HelmRelease Install(string name, string? version)
            {
                var release = helm.Install(name, chart, values: [valuesFile], setValues: values, createNamespace: true, wait: true, timeout: TimeSpan.FromMinutes(1), version: version);
                installed.Add(name);
                return release;
            }
            void Check(HelmRelease release, string version, string revision)
            {
                Require(release.ChartName == "versioned" && release.ChartVersion == version, $"Wrong chart metadata: {release}");
                Require(release.Revision == revision && release.Status == "deployed", $"Wrong release revision/status: {release}");
                var manifest = helm.GetManifest(release.Name);
                CheckVersionManifest(manifest, version);
                Require(manifest == preview(release.Name, version), "Preview and deployed manifests differ.");
                RequireValue(helm.GetValues(release.Name), "inline");
                var status = helm.Status(release.Name);
                Require(status.ChartVersion == version && status.Revision == revision, "Stored chart version/revision differs from action result.");
            }

            Check(Install("selected", "1.0.0"), "1.0.0", "1");
            Check(helm.Upgrade("selected", chart, values: [valuesFile], setValues: values, wait: true, timeout: TimeSpan.FromMinutes(1), version: "2.0.0"), "2.0.0", "2");
            CheckVersionManifest(helm.GetManifest("selected", revision: 1), "1.0.0");
            Check(helm.Upgrade("selected", chart, values: [valuesFile], setValues: values), "3.0.0", "3");
            Check(helm.Upgrade("selected", chart, values: [valuesFile], setValues: values, version: "1.0.0"), "1.0.0", "4");
            Check(helm.Upgrade("selected", chart, values: [valuesFile], setValues: values, version: ">=1.0.0 <3.0.0"), "2.0.0", "5");
            var history = helm.History("selected");
            Require(history.Count == 5 && history.Any(item => item.Revision == "2" && item.ChartVersion == "2.0.0"), "History lost chart version/revision metadata.");
            foreach (var version in new[] { "99.0.0", "not-a-version" })
                RequireActionFailure(() => helm.Upgrade("selected", chart, version: version), version);
            Require(helm.Status("selected").Revision == "5", "Failed version resolution changed the release.");

            Check(Install("latest", null), "3.0.0", "1");
            var omitted = helm.Install("omitted", chart, values: [valuesFile], setValues: values, createNamespace: true);
            installed.Add("omitted");
            Check(omitted, "3.0.0", "1");
            foreach (var (name, version, expected) in new (string Name, string? Version, string Expected)[]
            {
                ("missing-selected", "1.0.0", "1.0.0"),
                ("missing-latest", null, "3.0.0"),
                ("missing-constraint", ">=1.0.0 <3.0.0", "2.0.0")
            })
            {
                var release = helm.Upgrade(name, chart, values: [valuesFile], setValues: values, install: true, createNamespace: true, wait: true, timeout: TimeSpan.FromMinutes(1), version: version);
                installed.Add(name);
                Check(release, expected, "1");
            }
            Check(Install("constraint", ">=1.0.0 <3.0.0"), "2.0.0", "1");
            var omittedMissing = helm.Upgrade("missing-omitted", chart, values: [valuesFile], setValues: values, install: true);
            installed.Add("missing-omitted");
            Check(omittedMissing, "3.0.0", "1");
        }
        finally
        {
            foreach (var name in installed)
                helm.Uninstall(name, wait: true);
        }
    }

    private static void CheckVersionManifest(string manifest, string version)
    {
        foreach (var expected in new[] { $"version: \"{version}\"", "setting: \"inline\"", "retained: \"from-file\"", "enabled: \"true\"", "replicas: \"2\"", "dependency: \"packaged\"" })
            Require(manifest.Contains(expected, StringComparison.Ordinal), $"Manifest is missing {expected}:\n{manifest}");
    }

    private static void RequireActionFailure(Action action, string version)
    {
        try
        {
            action();
        }
        catch (HelmActionException exception) when (exception.Message.Contains(version, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException($"Expected HelmActionException containing {version}.");
    }
}

internal sealed class VersionedChartRepository : IDisposable
{
    internal static readonly string[] Versions = ["1.0.0", "2.0.0", "3.0.0"];
    private static readonly string[] ExtraVersions = ["1.0.0", "2.0.0"];
    private readonly HttpListener _listener = new();
    private readonly Task _server;
    private readonly Dictionary<string, byte[]> _responses = new(StringComparer.Ordinal);
    private int _stopping;
    private int _requestCount;

    internal VersionedChartRepository()
    {
        var index = new StringBuilder("apiVersion: v1\nentries:\n");
        AppendChartVersions(index, "versioned", Versions, includeArchives: true);
        AppendChartVersions(index, "versioned-extra", ExtraVersions, includeArchives: false);
        _responses.Add("/index.yaml", Encoding.UTF8.GetBytes(index.ToString()));
        int port;
        using (var reservation = new TcpListener(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }
        Url = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add($"{Url}/");
        _listener.Start();
        _server = Task.Run(Serve);
    }

    private void AppendChartVersions(StringBuilder index, string chartName, IEnumerable<string> versions, bool includeArchives)
    {
        index.Append(CultureInfo.InvariantCulture, $"  {chartName}:\n");
        foreach (var version in versions)
        {
            var archive = $"{chartName}-{version}.tgz";
            if (includeArchives)
                _responses.Add($"/{archive}", CreateChart(version));
            index.Append(CultureInfo.InvariantCulture, $"  - apiVersion: v2\n    name: {chartName}\n    version: {version}\n    urls:\n    - {archive}\n");
        }
    }

    internal string Url { get; }
    internal byte[] OlderChart => _responses["/versioned-1.0.0.tgz"];
    internal int RequestCount => Volatile.Read(ref _requestCount);

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

    private static byte[] CreateChart(string version)
    {
        using var archive = new MemoryStream();
        using (var gzip = new GZipStream(archive, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, leaveOpen: true))
        {
            void Add(string path, string text)
            {
                using var data = new MemoryStream(Encoding.UTF8.GetBytes(text));
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, $"versioned/{path}") { DataStream = data });
            }
            Add("Chart.yaml", $"apiVersion: v2\nname: versioned\nversion: {version}\ndependencies:\n- name: helpers\n  version: 1.0.0\n");
            Add("values.yaml", "setting: default\n");
            Add("templates/configmap.yaml", """
                apiVersion: v1
                kind: ConfigMap
                metadata:
                  name: {{ .Release.Name }}
                data:
                  version: {{ .Chart.Version | quote }}
                  setting: {{ .Values.setting | quote }}
                  retained: {{ .Values.retained | quote }}
                  enabled: {{ .Values.enabled | quote }}
                  replicas: {{ .Values.replicas | quote }}
                  dependency: {{ include "helpers.value" . | quote }}
                """);
            Add("charts/helpers/Chart.yaml", "apiVersion: v2\nname: helpers\nversion: 1.0.0\ntype: library\n");
            Add("charts/helpers/templates/_helpers.tpl", """{{- define "helpers.value" -}}packaged{{- end -}}""");
        }
        return archive.ToArray();
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
            Interlocked.Increment(ref _requestCount);
            if (_responses.TryGetValue(context.Request.Url!.AbsolutePath, out var body))
            {
                context.Response.ContentType = context.Request.Url.AbsolutePath.EndsWith(".tgz", StringComparison.Ordinal) ? "application/gzip" : "application/yaml";
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
            context.Response.Close();
        }
    }
}
