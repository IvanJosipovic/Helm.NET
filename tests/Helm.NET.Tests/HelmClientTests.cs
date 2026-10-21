using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Helm.NET.Scenarios;

namespace Helm.NET.Tests;

public sealed class HelmClientTests
{
    private const string ReleaseJson = """
        {"name":"web","namespace":"apps","revision":"3","updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"nginx-1.2.3","app_version":"2.0"}
        """;

    [Fact]
    public void ConstructorUsesDefaultOptions()
    {
        var client = new HelmClient();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void ListReleasesUsesDefaultsAndMapsRecords()
    {
        var client = CreateClient("{\"data\":[" + ReleaseJson + "]}", out var request);

        var releases = client.ListReleases();

        request().GetProperty("operation").GetString().ShouldBe("list");
        request().GetProperty("namespace").GetString().ShouldBe("default");
        releases.ShouldHaveSingleItem().ShouldBe(new HelmRelease("web", "apps", "3", "2026-01-02T03:04:05Z", "deployed", "nginx-1.2.3", "2.0"));
    }

    [Fact]
    public void ListReleasesCanSelectAllNamespacesAndFilter()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.ListReleases(allNamespaces: true, filter: "^web$");

        var json = request();
        json.GetProperty("allNamespaces").GetBoolean().ShouldBeTrue();
        json.GetProperty("filter").GetString().ShouldBe("^web$");
    }

    [Fact]
    public void TemplateSerializesValuesAndReturnsManifest()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);
        var values = new Dictionary<string, object?> { ["replicas"] = 2 };

        client.Template("web", "./chart", ["prod.yaml"], values).ShouldBe("rendered yaml");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("template");
        json.GetProperty("releaseName").GetString().ShouldBe("web");
        json.GetProperty("chart").GetString().ShouldBe("./chart");
        json.GetProperty("valuesFiles")[0].GetString().ShouldBe("prod.yaml");
        json.GetProperty("values").GetProperty("replicas").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void TemplateSerializesRequestedVersion()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);

        client.Template("web", "stable/nginx", version: "1.2.3");

        request().GetProperty("version").GetString().ShouldBe("1.2.3");
    }

    [Fact]
    public void TemplateReleaseSerializesRevision()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);

        client.TemplateRelease("web", revision: 2).ShouldBe("rendered yaml");

        request().GetProperty("operation").GetString().ShouldBe("templateRelease");
        request().GetProperty("revision").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void GetManifestSerializesRevision()
    {
        var client = CreateClient("{\"data\":\"stored yaml\"}", out var request);

        client.GetManifest("web", revision: 4).ShouldBe("stored yaml");

        request().GetProperty("operation").GetString().ShouldBe("getManifest");
        request().GetProperty("revision").GetInt32().ShouldBe(4);
    }

    [Fact]
    public void InstallSerializesAllOptionsAndMapsRelease()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);
        var release = client.Install("web", "chart", ["values.yaml"], new Dictionary<string, object?> { ["key"] = "value" }, createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(12.5));

        release.Name.ShouldBe("web");
        release.AppVersion.ShouldBe("2.0");
        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("install");
        json.GetProperty("createNamespace").GetBoolean().ShouldBeTrue();
        json.GetProperty("wait").GetBoolean().ShouldBeTrue();
        json.GetProperty("timeoutMilliseconds").GetInt64().ShouldBe(12500);
    }

    [Fact]
    public void UpgradeSerializesInstallIfMissing()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Upgrade("web", "chart", installIfMissing: true);

        request().GetProperty("installIfMissing").GetBoolean().ShouldBeTrue();
        request().GetProperty("operation").GetString().ShouldBe("upgrade");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Exercises the public interface overload.")]
    public void InstallAndUpgradeSerializeVersionAndPreserveOptions(bool upgrade)
    {
        const string response = """
            {"data":{"name":"web","namespace":"apps","revision":"4","status":"deployed","chart":"nginx-1.2.3","chartName":"nginx","chartVersion":"1.2.3"}}
            """;
        IHelmClient client = CreateClient(response, out var request);
        var values = new Dictionary<string, object?> { ["key"] = "value" };
        var timeout = TimeSpan.FromSeconds(12.5);

        var release = upgrade
            ? client.Upgrade("web", "stable/nginx", ["values.yaml"], values, installIfMissing: true, createNamespace: true, wait: true, timeout: timeout, version: "1.2.3")
            : client.Install("web", "stable/nginx", ["values.yaml"], values, createNamespace: true, wait: true, timeout: timeout, version: "1.2.3");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe(upgrade ? "upgrade" : "install");
        json.GetProperty("chart").GetString().ShouldBe("stable/nginx");
        json.GetProperty("version").GetString().ShouldBe("1.2.3");
        json.GetProperty("valuesFiles")[0].GetString().ShouldBe("values.yaml");
        json.GetProperty("values").GetProperty("key").GetString().ShouldBe("value");
        json.GetProperty("createNamespace").GetBoolean().ShouldBeTrue();
        json.GetProperty("wait").GetBoolean().ShouldBeTrue();
        json.GetProperty("timeoutMilliseconds").GetInt64().ShouldBe(12500);
        json.GetProperty("installIfMissing").GetBoolean().ShouldBe(upgrade);
        json.GetProperty("revision").GetInt32().ShouldBe(0);
        release.ChartVersion.ShouldBe("1.2.3");
        release.Revision.ShouldBe("4");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Exercises both class and interface compatibility overloads.")]
    public void InstallAndUpgradeDefaultAndLegacyOverloadsOmitVersion(bool upgrade)
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);
        IHelmClient contract = client;

        if (upgrade)
            contract.Upgrade("web", "chart");
        else
            contract.Install("web", "chart");
        request().GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);

        if (upgrade)
            client.Upgrade("web", "chart", null, null, false, false, false, null);
        else
            client.Install("web", "chart", null, null, false, false, null);
        request().GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);

        if (upgrade)
            contract.Upgrade("web", "chart", null, null, false, false, false, null);
        else
            contract.Install("web", "chart", null, null, false, false, null);
        request().GetProperty("version").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstallAndUpgradePropagateUnavailableVersionError(bool upgrade)
    {
        var client = CreateClient("{\"error\":\"chart version 99.0.0 not found\"}", out var request);

        var exception = Should.Throw<HelmActionException>(() =>
        {
            if (upgrade)
                client.Upgrade("web", "stable/nginx", installIfMissing: true, version: "99.0.0");
            else
                client.Install("web", "stable/nginx", version: "99.0.0");
        });

        exception.Message.ShouldBe("chart version 99.0.0 not found");
        request().GetProperty("version").GetString().ShouldBe("99.0.0");
    }

    [Fact]
    public void StatusMapsRelease()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Status("web").Status.ShouldBe("deployed");

        request().GetProperty("releaseName").GetString().ShouldBe("web");
        request().GetProperty("operation").GetString().ShouldBe("status");
    }

    [Fact]
    public void HistoryMapsReleases()
    {
        var client = CreateClient("{\"data\":[" + ReleaseJson + "]}", out var request);

        client.History("web").ShouldHaveSingleItem().Name.ShouldBe("web");

        request().GetProperty("operation").GetString().ShouldBe("history");
        request().GetProperty("releaseName").GetString().ShouldBe("web");
    }

    [Fact]
    public void RollbackSendsRevision()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.Rollback("web", 2);

        request().GetProperty("operation").GetString().ShouldBe("rollback");
        request().GetProperty("revision").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void UninstallSendsReleaseName()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.Uninstall("web");

        request().GetProperty("operation").GetString().ShouldBe("uninstall");
        request().GetProperty("releaseName").GetString().ShouldBe("web");
    }

    [Fact]
    public void GetValuesMapsJsonValuesAndUsesConfiguredNamespace()
    {
        var client = CreateClient("{\"data\":{\"replicas\":3,\"enabled\":true}}", out var request,
            new HelmClientOptions { Namespace = "apps" });

        var values = client.GetValues("web");

        values["replicas"].GetInt32().ShouldBe(3);
        values["enabled"].GetBoolean().ShouldBeTrue();
        request().GetProperty("namespace").GetString().ShouldBe("apps");
        request().GetProperty("operation").GetString().ShouldBe("values");
    }

    [Fact]
    public void ListRepositoriesMapsRepositoryRecords()
    {
        var client = CreateClient("{\"data\":[{\"name\":\"stable\",\"url\":\"https://example.test\"}]}", out var request);

        client.ListRepositories().ShouldHaveSingleItem().ShouldBe(new HelmRepository("stable", "https://example.test"));

        request().GetProperty("operation").GetString().ShouldBe("repoList");
    }

    [Fact]
    public void SearchChartsMapsQualifiedReferenceAndVersion()
    {
        var client = CreateClient("{\"data\":[{\"reference\":\"stable/nginx\",\"chartName\":\"nginx\",\"version\":\"1.2.3\"}]}", out var request);

        client.SearchCharts("nginx").ShouldHaveSingleItem().ShouldBe(new HelmChartSearchResult("stable/nginx", "nginx", "1.2.3"));
        request().GetProperty("operation").GetString().ShouldBe("chartSearch");
        request().GetProperty("searchTerm").GetString().ShouldBe("nginx");
    }

    [Fact]
    public void ListChartsMapsResultsAndForwardsExactRepositoryName()
    {
        const string response = """
            {"data":[
                {"reference":"stable/nginx","chartName":"nginx","version":"1.2.3"},
                {"reference":"stable/redis","chartName":"redis","version":"7.0.0"}
            ]}
            """;
        var client = CreateClient(response, out var request);

        client.ListCharts("stable").ShouldBe([
            new HelmChartSearchResult("stable/nginx", "nginx", "1.2.3"),
            new HelmChartSearchResult("stable/redis", "redis", "7.0.0")
        ]);

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("chartList");
        json.GetProperty("repositoryName").GetString().ShouldBe("stable");
    }

    [Fact]
    public void ListChartsDefaultsToAllRepositories()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.ListCharts().ShouldBeEmpty();

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("chartList");
        json.GetProperty("repositoryName").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void AddRepositorySendsCredentials()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.AddRepository("private", "https://example.test", "user", "secret");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("repoAdd");
        json.GetProperty("repositoryName").GetString().ShouldBe("private");
        json.GetProperty("repositoryUrl").GetString().ShouldBe("https://example.test");
        json.GetProperty("repositoryUsername").GetString().ShouldBe("user");
        json.GetProperty("repositoryPassword").GetString().ShouldBe("secret");
    }

    [Fact]
    public void RemoveRepositorySendsName()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.RemoveRepository("stable");

        request().GetProperty("repositoryName").GetString().ShouldBe("stable");
        request().GetProperty("operation").GetString().ShouldBe("repoRemove");
    }

    [Fact]
    public void UpdateRepositoriesSendsOperation()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.UpdateRepositories();

        request().GetProperty("operation").GetString().ShouldBe("repoUpdate");
    }

    [Fact]
    public void SharedChartVersionsUseRealNativeResolution()
    {
        HelmClientScenarios.RunChartVersionScenarios(clusterLifecycle: false);
    }

    [Fact]
    public void SharedScenariosUseTheSameOperationsWithScriptedNativeResponses()
    {
        var requests = new List<JsonElement>();
        var repositoryListCount = 0;
        var releaseListCount = 0;
        var valuesCount = 0;
        var mainRelease = Release("aot-smoke", "1");
        var upgradedRelease = Release("aot-smoke", "2");
        var secondaryRelease = Release("aot-smoke-nowait", "1");
        const string manifest = "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: aot-smoke\ndata:\n  setting: \"rendered\"\n";

        string NativeCall(byte[] payload)
        {
            using var document = JsonDocument.Parse(payload);
            var request = document.RootElement.Clone();
            requests.Add(request);
            var operation = request.GetProperty("operation").GetString();
            var data = operation switch
            {
                "template" => JsonSerializer.Serialize(manifest, NativeJsonContext.Default.String),
                "repoAdd" or "repoUpdate" or "repoRemove" or "rollback" or "uninstall" => "{\"succeeded\":true}",
                "repoList" => ++repositoryListCount == 1
                    ? "[{\"name\":\"aot-smoke\",\"url\":\"http://127.0.0.1\"}]"
                    : "[]",
                "chartSearch" => "[{\"reference\":\"aot-smoke/aot-smoke\",\"chartName\":\"aot-smoke\",\"version\":\"1.0.0\"}]",
                "install" => mainRelease,
                "getManifest" or "templateRelease" => JsonSerializer.Serialize(manifest, NativeJsonContext.Default.String),
                "list" => ++releaseListCount switch
                {
                    1 or 2 => "[" + mainRelease + "]",
                    3 => "[" + mainRelease + "," + secondaryRelease + "]",
                    _ => "[]"
                },
                "status" => mainRelease,
                "values" => ++valuesCount switch
                {
                    1 or 3 => "{\"setting\":\"installed\"}",
                    _ => "{\"setting\":\"upgraded\"}"
                },
                "upgrade" => request.GetProperty("releaseName").GetString() == "aot-smoke"
                    ? upgradedRelease
                    : secondaryRelease,
                "history" => "[" + mainRelease + "," + upgradedRelease + "]",
                _ => throw new InvalidOperationException($"Unexpected scenario operation: {operation}")
            };
            return "{\"data\":" + data + "}";
        }

        HelmClient CreateClient(HelmClientOptions options) => new(options, NativeCall);

        var chartDirectory = Path.Combine(Path.GetTempPath(), $"helm-net-scenario-{Guid.NewGuid():N}");
        Directory.CreateDirectory(chartDirectory);
        File.WriteAllText(Path.Combine(chartDirectory, "smoke-values.yaml"), "setting: from-file\n");
        try
        {
            var helm = CreateClient(new HelmClientOptions { Namespace = "helm-net-aot" });
            HelmClientScenarios.Run(helm, CreateClient, chartDirectory, "http://127.0.0.1", clusterLifecycle: true);
        }
        finally
        {
            Directory.Delete(chartDirectory, recursive: true);
        }

        requests.Select(item => item.GetProperty("operation").GetString()).ShouldBe(
        [
            "template", "repoAdd", "repoList", "repoUpdate", "chartSearch", "repoRemove", "repoList",
            "install", "getManifest", "templateRelease", "list", "list", "status", "values", "upgrade",
            "values", "history", "rollback", "values", "rollback", "values", "upgrade", "list", "uninstall",
            "uninstall", "list"
        ]);
        requests[0].GetProperty("version").GetString().ShouldBe("1.0.0");
        requests[0].GetProperty("values").GetProperty("setting").GetString().ShouldBe("rendered");
        requests[0].GetProperty("valuesFiles")[0].GetString().ShouldBe(Path.Combine(chartDirectory, "smoke-values.yaml"));
        requests[1].GetProperty("repositoryName").GetString().ShouldBe("aot-smoke");
        requests[1].GetProperty("repositoryUrl").GetString().ShouldBe("http://127.0.0.1");
        requests[2].GetProperty("repositoryConfig").GetString()!.ShouldContain("repositories.yaml");
        requests[2].GetProperty("repositoryCache").GetString()!.ShouldContain("cache");
        requests[7].GetProperty("createNamespace").GetBoolean().ShouldBeTrue();
        requests[7].GetProperty("version").GetString().ShouldBe("1.0.0");
        requests[14].GetProperty("installIfMissing").GetBoolean().ShouldBeTrue();
        requests[14].GetProperty("version").GetString().ShouldBe("1.0.0");
        requests[21].GetProperty("version").GetString().ShouldBe("1.0.0");
        requests[17].GetProperty("revision").GetInt32().ShouldBe(1);
        requests[10].GetProperty("namespace").GetString().ShouldBe("helm-net-aot");
        requests[11].GetProperty("allNamespaces").GetBoolean().ShouldBeTrue();
        requests[11].GetProperty("filter").GetString().ShouldBe("^aot-smoke$");

        static string Release(string name, string revision) =>
            $$"""{"name":"{{name}}","namespace":"helm-net-aot","revision":"{{revision}}","updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"aot-smoke-1.0.0","app_version":"1.0.0","chartName":"aot-smoke","chartVersion":"1.0.0"}""";
    }

    [Fact]
    public void InvokePropagatesAllConfiguredOptions()
    {
        var options = new HelmClientOptions
        {
            Kubeconfig = "kube.yaml", KubeContext = "dev", Namespace = "apps",
            RepositoryConfig = "repositories.yaml", RepositoryCache = "cache",
            RegistryConfig = "registry.json", HelmDriver = "configmap"
        };
        var client = CreateClient("{\"data\":[]}", out var request, options);

        client.ListReleases();

        var json = request();
        json.GetProperty("kubeconfig").GetString().ShouldBe("kube.yaml");
        json.GetProperty("kubeContext").GetString().ShouldBe("dev");
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("repositoryConfig").GetString().ShouldBe("repositories.yaml");
        json.GetProperty("repositoryCache").GetString().ShouldBe("cache");
        json.GetProperty("registryConfig").GetString().ShouldBe("registry.json");
        json.GetProperty("helmDriver").GetString().ShouldBe("configmap");
    }

    [Fact]
    public void InvokeThrowsActionExceptionWhenNativeResponseHasError()
    {
        var client = CreateClient("{\"error\":\"release not found\"}", out _);

        Should.Throw<HelmActionException>(() => client.Status("missing")).Message.ShouldBe("release not found");
    }

    [Fact]
    public void InvokeThrowsWhenNativeResponseIsInvalidJson()
    {
        var client = CreateClient("not json", out _);

        Should.Throw<JsonException>(() => client.ListReleases());
    }

    [Fact]
    public void OperationsWithOptionalDataReturnEmptyDefaults()
    {
        var client = CreateClient("{}", out _);

        client.ListReleases().ShouldBeEmpty();
        client.History("web").ShouldBeEmpty();
        client.ListRepositories().ShouldBeEmpty();
        client.SearchCharts("chart").ShouldBeEmpty();
        client.ListCharts().ShouldBeEmpty();
        client.GetValues("web").ShouldBeEmpty();
        client.Template("web", "chart").ShouldBeEmpty();
        client.TemplateRelease("web").ShouldBeEmpty();
        client.GetManifest("web").ShouldBeEmpty();
    }

    [Fact]
    public void InstallThrowsWhenNativeResponseHasNoRelease()
    {
        var client = CreateClient("{}", out _);

        Should.Throw<InvalidOperationException>(() => client.Install("web", "chart"));
    }

    [Fact]
    public void NativeCallCopiesRequestAndFreesResponse()
    {
        var native = new FakeNativeApi("{\"data\":[]}");
        var client = new HelmClient(new HelmClientOptions { Namespace = "apps" }, native);

        client.ListReleases().ShouldBeEmpty();

        native.Request.ShouldNotBeNull();
        using var request = JsonDocument.Parse(native.Request);
        request.RootElement.GetProperty("operation").GetString().ShouldBe("list");
        request.RootElement.GetProperty("namespace").GetString().ShouldBe("apps");
        native.WasFreed.ShouldBeTrue();
    }

    [Fact]
    public void NativeLibraryExecutesAndReturnsItsActionError()
    {
        var response = HelmNativeInterop.Invoke(
            Encoding.UTF8.GetBytes("{\"operation\":\"unsupported\"}"),
            HelmNativeInterop.DefaultApi);

        response.ShouldContain("unsupported operation: unsupported");
    }

    [Fact]
    public void NativeCallThrowsWhenNativeReturnsNull()
    {
        var native = new FakeNativeApi(null);
        var client = new HelmClient(new HelmClientOptions(), native);

        Should.Throw<InvalidOperationException>(() => client.ListReleases()).Message.ShouldContain("null response");
        native.WasFreed.ShouldBeFalse();
    }

    [Fact]
    public void NativeCallFreesResponseWhenItCannotBeDecoded()
    {
        var native = new FakeNativeApi("unused") { ReturnUndecodableString = true };
        var client = new HelmClient(new HelmClientOptions(), native);

        Should.Throw<InvalidOperationException>(() => client.ListReleases()).Message.ShouldContain("invalid response");
        native.WasFreed.ShouldBeTrue();
    }

    [Fact]
    public void NativeRequestUsesCamelCaseJsonFields()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new NativeHelmRequest("repoAdd", RepositoryURL: "https://example.test"), NativeJsonContext.Default.NativeHelmRequest);

        var text = Encoding.UTF8.GetString(bytes);
        text.ShouldContain("\"operation\"");
        text.ShouldContain("\"repositoryUrl\"");
        text.Contains("\"Operation\"", StringComparison.Ordinal).ShouldBeFalse();
    }

    private static HelmClient CreateClient(string response, out Func<JsonElement> readRequest, HelmClientOptions? options = null)
    {
        JsonElement? captured = null;
        var client = new HelmClient(options ?? new HelmClientOptions(), payload =>
        {
            using var document = JsonDocument.Parse(payload);
            captured = document.RootElement.Clone();
            return response;
        });
        readRequest = () => captured ?? throw new InvalidOperationException("No native request was captured.");
        return client;
    }

    private sealed class FakeNativeApi(string? response) : IHelmNativeApi
    {
        private nint _responsePointer;

        public byte[]? Request { get; private set; }
        public bool WasFreed { get; private set; }
        public bool ReturnUndecodableString { get; init; }

        public nint Execute(nint input)
        {
            var bytes = new List<byte>();
            for (var offset = 0; Marshal.ReadByte(input, offset) != 0; offset++)
                bytes.Add(Marshal.ReadByte(input, offset));
            Request = bytes.ToArray();
            if (response is null)
                return 0;

            var responseBytes = Encoding.UTF8.GetBytes(response + "\0");
            _responsePointer = Marshal.AllocHGlobal(responseBytes.Length);
            Marshal.Copy(responseBytes, 0, _responsePointer, responseBytes.Length);
            return _responsePointer;
        }

        public string? GetString(nint value) => ReturnUndecodableString ? null : Marshal.PtrToStringUTF8(value);

        public void Free(nint value)
        {
            WasFreed = true;
            Marshal.FreeHGlobal(value);
            _responsePointer = 0;
        }
    }
}
