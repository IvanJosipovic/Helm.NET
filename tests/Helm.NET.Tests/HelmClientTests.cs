using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
namespace Helm.NET.Tests;

public sealed class HelmClientTests
{
    private const string ReleaseJson = """
        {"name":"web","namespace":"apps","revision":3,"updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"nginx-1.2.3","app_version":"2.0"}
        """;

    [Fact]
    public void ConstructorUsesDefaultOptions()
    {
        var client = new HelmClient();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void ListUsesDefaultsAndMapsRecords()
    {
        var client = CreateClient("{\"data\":[" + ReleaseJson + "]}", out var request);

        var releases = client.List();

        request().GetProperty("operation").GetString().ShouldBe("list");
        request().GetProperty("namespace").GetString().ShouldBe("default");
        var release = releases.ShouldHaveSingleItem();
        release.Name.ShouldBe("web");
        release.Namespace.ShouldBe("apps");
        release.Revision.ShouldBe(3);
        release.Updated.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture));
        release.Status.ShouldBe("deployed");
        release.Chart.ShouldBe("nginx-1.2.3");
        release.AppVersion.ShouldBe("2.0");
    }

    [Fact]
    public void ListCanSelectAllNamespacesAndFilter()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.List(allNamespaces: true, filter: "^web$");

        var json = request();
        json.GetProperty("allNamespaces").GetBoolean().ShouldBeTrue();
        json.GetProperty("filter").GetString().ShouldBe("^web$");
    }

    [Fact]
    public void ListAllowsNamespaceOverrideWhileAllNamespacesIsExplicit()
    {
        var client = CreateClient("{\"data\":[]}", out var request,
            new HelmClientOptions { DefaultNamespace = "default" });

        client.List("apps");
        request().GetProperty("namespace").GetString().ShouldBe("apps");

        client.List(allNamespaces: true, namespaceName: "apps");
        var json = request();
        json.GetProperty("allNamespaces").GetBoolean().ShouldBeTrue();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
    }

    [Fact]
    public void ListSerializesPaginationSelectorStatusesAndSort()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.List(allNamespaces: true, filter: "^web$", max: 20, offset: 10, selector: "team=apps", deployed: true, failed: true, dateSort: true, reverse: true);

        var json = request();
        json.GetProperty("max").GetInt32().ShouldBe(20);
        json.GetProperty("offset").GetInt32().ShouldBe(10);
        json.GetProperty("selector").GetString().ShouldBe("team=apps");
        json.GetProperty("deployed").GetBoolean().ShouldBeTrue();
        json.GetProperty("failed").GetBoolean().ShouldBeTrue();
        json.GetProperty("date").GetBoolean().ShouldBeTrue();
        json.GetProperty("reverse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void TemplateSerializesValuesAndReturnsManifest()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);
        var values = ParseJsonElement("""{"replicas":2,"image":{"tag":"1.2.3"}}""");

        client.Template("web", "./chart", values: ["prod.yaml"], setValues: values).ShouldBe("rendered yaml");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("template");
        json.GetProperty("releaseName").GetString().ShouldBe("web");
        json.GetProperty("chart").GetString().ShouldBe("./chart");
        json.GetProperty("valuesFiles")[0].GetString().ShouldBe("prod.yaml");
        json.GetProperty("values").GetProperty("replicas").GetInt32().ShouldBe(2);
        json.GetProperty("values").GetProperty("image").GetProperty("tag").GetString().ShouldBe("1.2.3");
    }

    [Fact]
    public void SetValuesRequiresJsonObject()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out _);
        var values = ParseJsonElement("3");

        Should.Throw<ArgumentException>(() => client.Template("web", "chart", setValues: values))
            .ParamName.ShouldBe("setValues");
    }

    [Fact]
    public void TemplateSerializesRequestedVersion()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);

        client.Template("web", "stable/nginx", version: "1.2.3");

        request().GetProperty("version").GetString().ShouldBe("1.2.3");
    }

    [Fact]
    public void ShowValuesSerializesChartVersionAndDownloadOptions()
    {
        var client = CreateClient("{\"data\":\"replicas: 2\\n\"}", out var request);

        client.ShowValues("oci://registry.example/charts/web", version: "1.2.3", username: "user", password: "password", plainHttp: true)
            .ShouldBe("replicas: 2\n");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("showValues");
        json.GetProperty("chart").GetString().ShouldBe("oci://registry.example/charts/web");
        json.GetProperty("version").GetString().ShouldBe("1.2.3");
        json.GetProperty("repositoryUsername").GetString().ShouldBe("user");
        json.GetProperty("repositoryPassword").GetString().ShouldBe("password");
        json.GetProperty("plainHttp").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void TemplateSerializesRenderingAndChartDownloadOptions()
    {
        var client = CreateClient("{\"data\":\"rendered yaml\"}", out var request);

        client.Template("web", "stable/nginx", "apps", apiVersions: ["example.io/v1"], kubeVersion: "1.30.0", includeCrds: true, skipTests: true, dependencyUpdate: true, isUpgrade: true, dryRun: "server", hideSecret: true, disableHooks: true, postRenderer: "renderer", postRendererArgs: ["--strict"], repo: "https://charts.example", username: "user", password: "password", caFile: "ca.pem", certFile: "client.pem", keyFile: "client.key", insecureSkipTlsVerify: true, passCredentials: true, plainHttp: true);

        var json = request();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("apiVersions")[0].GetString().ShouldBe("example.io/v1");
        json.GetProperty("kubeVersion").GetString().ShouldBe("1.30.0");
        json.GetProperty("includeCrds").GetBoolean().ShouldBeTrue();
        json.GetProperty("skipTests").GetBoolean().ShouldBeTrue();
        json.GetProperty("dependencyUpdate").GetBoolean().ShouldBeTrue();
        json.GetProperty("isUpgrade").GetBoolean().ShouldBeTrue();
        json.GetProperty("dryRun").GetString().ShouldBe("server");
        json.GetProperty("disableHooks").GetBoolean().ShouldBeTrue();
        json.GetProperty("repo").GetString().ShouldBe("https://charts.example");
        json.GetProperty("repositoryUsername").GetString().ShouldBe("user");
        json.GetProperty("repositoryPassword").GetString().ShouldBe("password");
        json.GetProperty("caFile").GetString().ShouldBe("ca.pem");
        json.GetProperty("certFile").GetString().ShouldBe("client.pem");
        json.GetProperty("keyFile").GetString().ShouldBe("client.key");
        json.GetProperty("insecureSkipTlsVerify").GetBoolean().ShouldBeTrue();
        json.GetProperty("passCredentials").GetBoolean().ShouldBeTrue();
        json.GetProperty("plainHttp").GetBoolean().ShouldBeTrue();
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
    public void ReleaseLookupMethodsAllowNamespaceOverrides()
    {
        var manifestClient = CreateClient("{\"data\":\"manifest\"}", out var manifestRequest,
            new HelmClientOptions { DefaultNamespace = "default" });
        manifestClient.GetManifest("web", "apps");
        manifestRequest().GetProperty("namespace").GetString().ShouldBe("apps");

        var statusClient = CreateClient("{\"data\":" + ReleaseJson + "}", out var statusRequest);
        statusClient.Status("web", "apps");
        statusRequest().GetProperty("namespace").GetString().ShouldBe("apps");

        var historyClient = CreateClient("{\"data\":[]}", out var historyRequest);
        historyClient.History("web", "apps");
        historyRequest().GetProperty("namespace").GetString().ShouldBe("apps");

        var valuesClient = CreateClient("{\"data\":{}}", out var valuesRequest);
        valuesClient.GetValues("web", "apps");
        valuesRequest().GetProperty("namespace").GetString().ShouldBe("apps");
    }

    [Fact]
    public void InstallSerializesAllOptionsAndMapsRelease()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);
        var release = client.Install("web", "chart", "apps", values: ["values.yaml"], setValues: ParseJsonElement("""{"key":"value"}"""), createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(12.5));

        release.Name.ShouldBe("web");
        release.AppVersion.ShouldBe("2.0");
        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("install");
        json.GetProperty("createNamespace").GetBoolean().ShouldBeTrue();
        json.GetProperty("wait").GetBoolean().ShouldBeTrue();
        json.GetProperty("timeoutMilliseconds").GetInt64().ShouldBe(12500);
    }

    [Fact]
    public void InstallSerializesCliDryRunHooksRendererAndRollbackOptions()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Install("web", "chart", dryRun: "server", hideSecret: true, disableHooks: true, postRenderer: "renderer", postRendererArgs: ["--mode", "strict"], waitForJobs: true, rollbackOnFailure: true, dependencyUpdate: true, skipCrds: true, devel: true, skipSchemaValidation: true, disableOpenApiValidation: true, description: "test install");

        var json = request();
        json.GetProperty("dryRun").GetString().ShouldBe("server");
        json.GetProperty("hideSecret").GetBoolean().ShouldBeTrue();
        json.GetProperty("disableHooks").GetBoolean().ShouldBeTrue();
        json.GetProperty("postRenderer").GetString().ShouldBe("renderer");
        json.GetProperty("postRendererArgs")[1].GetString().ShouldBe("strict");
        json.GetProperty("waitForJobs").GetBoolean().ShouldBeTrue();
        json.GetProperty("rollbackOnFailure").GetBoolean().ShouldBeTrue();
        json.GetProperty("dependencyUpdate").GetBoolean().ShouldBeTrue();
        json.GetProperty("skipCrds").GetBoolean().ShouldBeTrue();
        json.GetProperty("devel").GetBoolean().ShouldBeTrue();
        json.GetProperty("skipSchemaValidation").GetBoolean().ShouldBeTrue();
        json.GetProperty("disableOpenApiValidation").GetBoolean().ShouldBeTrue();
        json.GetProperty("description").GetString().ShouldBe("test install");
    }

    [Fact]
    public void InstallSerializesNamespaceWaitOwnershipAndChartDownloadOptions()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Install("web", "chart", namespaceName: "apps", waitStrategy: "legacy", replace: true, forceReplace: true, forceConflicts: true, takeOwnership: true, repo: "https://charts.example", username: "user", password: "password", caFile: "ca.pem", certFile: "client.pem", keyFile: "client.key", insecureSkipTlsVerify: true, passCredentials: true, plainHttp: true);

        var json = request();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("waitStrategy").GetString().ShouldBe("legacy");
        json.GetProperty("replace").GetBoolean().ShouldBeTrue();
        json.GetProperty("forceReplace").GetBoolean().ShouldBeTrue();
        json.GetProperty("forceConflicts").GetBoolean().ShouldBeTrue();
        json.GetProperty("takeOwnership").GetBoolean().ShouldBeTrue();
        json.GetProperty("repo").GetString().ShouldBe("https://charts.example");
        json.GetProperty("passCredentials").GetBoolean().ShouldBeTrue();
        json.GetProperty("plainHttp").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void UpgradeSerializesInstallIfMissing()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Upgrade("web", "chart", install: true);

        request().GetProperty("installIfMissing").GetBoolean().ShouldBeTrue();
        request().GetProperty("operation").GetString().ShouldBe("upgrade");
    }

    [Fact]
    public void UpgradeSerializesDryRunValueStrategyAndPostRendererOptions()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Upgrade("web", "chart", dryRun: "client", hideSecret: true, disableHooks: true, postRenderer: "renderer", postRendererArgs: ["arg"], waitForJobs: true, rollbackOnFailure: true, resetValues: true, reuseValues: true, resetThenReuseValues: true, skipCrds: true, devel: true, skipSchemaValidation: true, disableOpenApiValidation: true, description: "test upgrade", dependencyUpdate: true);

        var json = request();
        json.GetProperty("dryRun").GetString().ShouldBe("client");
        json.GetProperty("resetValues").GetBoolean().ShouldBeTrue();
        json.GetProperty("reuseValues").GetBoolean().ShouldBeTrue();
        json.GetProperty("resetThenReuseValues").GetBoolean().ShouldBeTrue();
        json.GetProperty("rollbackOnFailure").GetBoolean().ShouldBeTrue();
        json.GetProperty("dependencyUpdate").GetBoolean().ShouldBeTrue();
        json.GetProperty("postRendererArgs")[0].GetString().ShouldBe("arg");
    }

    [Fact]
    public void UpgradeSerializesNamespaceWaitForceOwnershipAndCleanupOptions()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Upgrade("web", "chart", namespaceName: "apps", waitStrategy: "watcher", forceReplace: true, forceConflicts: true, takeOwnership: true, cleanupOnFail: true, repo: "https://charts.example", username: "user", password: "password", caFile: "ca.pem", certFile: "client.pem", keyFile: "client.key", insecureSkipTlsVerify: true, passCredentials: true, plainHttp: true);

        var json = request();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("waitStrategy").GetString().ShouldBe("watcher");
        json.GetProperty("forceReplace").GetBoolean().ShouldBeTrue();
        json.GetProperty("forceConflicts").GetBoolean().ShouldBeTrue();
        json.GetProperty("takeOwnership").GetBoolean().ShouldBeTrue();
        json.GetProperty("cleanupOnFail").GetBoolean().ShouldBeTrue();
        json.GetProperty("repo").GetString().ShouldBe("https://charts.example");
        json.GetProperty("passCredentials").GetBoolean().ShouldBeTrue();
        json.GetProperty("plainHttp").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Exercises the public interface overload.")]
    public void InstallAndUpgradeSerializeVersionAndPreserveOptions(bool upgrade)
    {
        const string response = """
            {"data":{"name":"web","namespace":"apps","revision":4,"updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"nginx-1.2.3","chartName":"nginx","chartVersion":"1.2.3"}}
            """;
        IHelmClient client = CreateClient(response, out var request);
        var values = ParseJsonElement("""{"key":"value"}""");
        var timeout = TimeSpan.FromSeconds(12.5);

        var release = upgrade
            ? client.Upgrade("web", "stable/nginx", values: ["values.yaml"], setValues: values, install: true, createNamespace: true, wait: true, timeout: timeout, version: "1.2.3")
            : client.Install("web", "stable/nginx", values: ["values.yaml"], setValues: values, createNamespace: true, wait: true, timeout: timeout, version: "1.2.3");

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
        release.Revision.ShouldBe(4);
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
                client.Upgrade("web", "stable/nginx", install: true, version: "99.0.0");
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
    public void StatusSerializesRevision()
    {
        var client = CreateClient("{\"data\":" + ReleaseJson + "}", out var request);

        client.Status("web", revision: 4);

        request().GetProperty("revision").GetInt32().ShouldBe(4);
    }

    [Fact]
    public void StatusMapsReleaseDescriptionNotesResourcesAndTestHooks()
    {
        const string response = """
            {"data":{"name":"web","namespace":"apps","revision":3,"updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"nginx-1.2.3","description":"ready","notes":"visit service","resources":{"v1/Pod":[{"metadata":{"name":"web"}}]},"hooks":[{"name":"smoke","kind":"Pod","events":["test"],"last_run":{"started_at":"2026-01-02T03:04:05Z","completed_at":"2026-01-02T03:04:06Z","phase":"Succeeded"},"delete_policies":["before-hook-creation"]}]}}
            """;
        var client = CreateClient(response, out _);

        var release = client.Status("web");

        release.Description.ShouldBe("ready");
        release.Notes.ShouldBe("visit service");
        release.Resources!.Value.GetProperty("v1/Pod")[0].GetProperty("metadata").GetProperty("name").GetString().ShouldBe("web");
        var hook = release.Hooks.ShouldHaveSingleItem();
        hook.Name.ShouldBe("smoke");
        hook.StartedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.CompletedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:06Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.Phase.ShouldBe("Succeeded");
        hook.DeletePolicies.ShouldHaveSingleItem().ShouldBe("before-hook-creation");
        release.Hooks[0].Events.ShouldContain("test");
    }

    [Fact]
    public void StatusRejectsNativeReleaseWithoutUpdatedTimestamp()
    {
        var client = CreateClient("{\"data\":{\"name\":\"web\",\"revision\":3,\"status\":\"deployed\",\"chart\":\"nginx-1.2.3\"}}", out _);

        Should.Throw<JsonException>(() => client.Status("web"));
    }

    [Fact]
    public void HistoryMapsReleases()
    {
        var client = CreateClient("{\"data\":[" + ReleaseJson + "]}", out var request);

        client.History("web").ShouldHaveSingleItem().Name.ShouldBe("web");

        request().GetProperty("operation").GetString().ShouldBe("history");
        request().GetProperty("releaseName").GetString().ShouldBe("web");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListAndHistoryMapHookExecutionAndDeletePolicies(bool history)
    {
        const string response = """
            {"data":[{"name":"web","namespace":"apps","revision":3,"updated":"2026-01-02T03:04:05Z","status":"deployed","chart":"nginx-1.2.3","hooks":[{"name":"smoke","kind":"Pod","events":["test"],"last_run":{"started_at":"2026-01-02T03:04:05Z","completed_at":"2026-01-02T03:04:06Z","phase":"Succeeded"},"delete_policies":["before-hook-creation"]}]}]}
            """;
        var client = CreateClient(response, out _);

        var release = history ? client.History("web").ShouldHaveSingleItem() : client.List().ShouldHaveSingleItem();

        var hook = release.Hooks.ShouldHaveSingleItem();
        hook.StartedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.CompletedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:06Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.Phase.ShouldBe("Succeeded");
        hook.DeletePolicies.ShouldHaveSingleItem().ShouldBe("before-hook-creation");
    }

    [Fact]
    public void HistorySerializesMax()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.History("web", max: 5);

        request().GetProperty("max").GetInt32().ShouldBe(5);
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
    public void RollbackSerializesCliOptions()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.Rollback("web", 3, namespaceName: "apps", waitStrategy: "legacy", timeout: TimeSpan.FromSeconds(20), waitForJobs: true, disableHooks: true, dryRun: "server", forceReplace: true, forceConflicts: true, cleanupOnFail: true, historyMax: 4, description: "rollback test");

        var json = request();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("waitStrategy").GetString().ShouldBe("legacy");
        json.GetProperty("timeoutMilliseconds").GetInt64().ShouldBe(20000);
        json.GetProperty("waitForJobs").GetBoolean().ShouldBeTrue();
        json.GetProperty("disableHooks").GetBoolean().ShouldBeTrue();
        json.GetProperty("dryRun").GetString().ShouldBe("server");
        json.GetProperty("forceReplace").GetBoolean().ShouldBeTrue();
        json.GetProperty("forceConflicts").GetBoolean().ShouldBeTrue();
        json.GetProperty("cleanupOnFail").GetBoolean().ShouldBeTrue();
        json.GetProperty("historyMax").GetInt32().ShouldBe(4);
        json.GetProperty("description").GetString().ShouldBe("rollback test");
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
    public void UninstallSerializesCliOptions()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.Uninstall("web", namespaceName: "apps", waitStrategy: "watcher", timeout: TimeSpan.FromSeconds(15), disableHooks: true, dryRun: true, ignoreNotFound: true, keepHistory: true, cascade: "foreground", description: "remove test");

        var json = request();
        json.GetProperty("namespace").GetString().ShouldBe("apps");
        json.GetProperty("waitStrategy").GetString().ShouldBe("watcher");
        json.GetProperty("timeoutMilliseconds").GetInt64().ShouldBe(15000);
        json.GetProperty("disableHooks").GetBoolean().ShouldBeTrue();
        json.GetProperty("dryRunUninstall").GetBoolean().ShouldBeTrue();
        json.GetProperty("ignoreNotFound").GetBoolean().ShouldBeTrue();
        json.GetProperty("keepHistory").GetBoolean().ShouldBeTrue();
        json.GetProperty("cascade").GetString().ShouldBe("foreground");
        json.GetProperty("description").GetString().ShouldBe("remove test");
    }

    [Fact]
    public void GetValuesMapsJsonValuesAndUsesConfiguredNamespace()
    {
        var client = CreateClient("{\"data\":{\"replicas\":3,\"enabled\":true}}", out var request,
            new HelmClientOptions { DefaultNamespace = "apps" });

        var values = client.GetValues("web");

        values.ValueKind.ShouldBe(JsonValueKind.Object);
        values.GetProperty("replicas").GetInt32().ShouldBe(3);
        values.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        request().GetProperty("namespace").GetString().ShouldBe("apps");
        request().GetProperty("operation").GetString().ShouldBe("values");
    }

    [Fact]
    public void GetValuesSerializesRevisionAndAllValues()
    {
        var client = CreateClient("{\"data\":{}}", out var request);

        client.GetValues("web", revision: 3, all: true);

        var json = request();
        json.GetProperty("revision").GetInt32().ShouldBe(3);
        json.GetProperty("allValues").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void RepoListMapsRepositoryRecords()
    {
        var client = CreateClient("{\"data\":[{\"name\":\"stable\",\"url\":\"https://example.test\"}]}", out var request);

        client.RepoList().ShouldHaveSingleItem().ShouldBe(new HelmRepository("stable", "https://example.test"));

        request().GetProperty("operation").GetString().ShouldBe("repoList");
    }

    [Fact]
    public void SearchRepoMapsQualifiedReferenceAndVersion()
    {
        var client = CreateClient("{\"data\":[{\"reference\":\"stable/nginx\",\"chartName\":\"nginx\",\"version\":\"1.2.3\",\"app_version\":\"2.0\"}]}", out var request);

        client.SearchRepo("nginx").ShouldHaveSingleItem().ShouldBe(new HelmChartSearchResult("stable/nginx", "nginx", "1.2.3", AppVersion: "2.0"));
        request().GetProperty("operation").GetString().ShouldBe("chartSearch");
        request().GetProperty("searchTerm").GetString().ShouldBe("nginx");
    }

    [Fact]
    public void SearchRepoAllowsOmittedKeyword()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.SearchRepo();

        request().GetProperty("searchTerm").GetString().ShouldBe(string.Empty);
    }

    [Fact]
    public void SearchRepoSerializesCliSearchOptions()
    {
        var client = CreateClient("{\"data\":[]}", out var request);

        client.SearchRepo("nginx.*", versions: true, devel: true, regexp: true, version: ">=1.0.0 <2.0.0", failOnNoResult: true);

        var json = request();
        json.GetProperty("versions").GetBoolean().ShouldBeTrue();
        json.GetProperty("devel").GetBoolean().ShouldBeTrue();
        json.GetProperty("regexp").GetBoolean().ShouldBeTrue();
        json.GetProperty("versionConstraint").GetString().ShouldBe(">=1.0.0 <2.0.0");
        json.GetProperty("failOnNoResult").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void RepoAddSendsCredentials()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.RepoAdd("private", "https://example.test", "user", "secret");

        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("repoAdd");
        json.GetProperty("repositoryName").GetString().ShouldBe("private");
        json.GetProperty("repositoryUrl").GetString().ShouldBe("https://example.test");
        json.GetProperty("repositoryUsername").GetString().ShouldBe("user");
        json.GetProperty("repositoryPassword").GetString().ShouldBe("secret");
    }

    [Fact]
    public void RepoAddSerializesForceUpdate()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.RepoAdd("stable", "https://example.test", forceUpdate: true);

        request().GetProperty("forceUpdate").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void RepoRemoveSendsName()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.RepoRemove("stable");

        request().GetProperty("repositoryName").GetString().ShouldBe("stable");
        request().GetProperty("operation").GetString().ShouldBe("repoRemove");
    }

    [Fact]
    public void RepoUpdateSendsOperation()
    {
        var client = CreateClient("{\"data\":{\"succeeded\":true}}", out var request);

        client.RepoUpdate();

        request().GetProperty("operation").GetString().ShouldBe("repoUpdate");
    }

    [Fact]
    public void InvokePropagatesAllConfiguredOptions()
    {
        var options = new HelmClientOptions
        {
            Kubeconfig = "kube.yaml", KubeContext = "dev", DefaultNamespace = "apps",
            RepositoryConfig = "repositories.yaml", RepositoryCache = "cache",
            RegistryConfig = "registry.json", HelmDriver = "configmap"
        };
        var client = CreateClient("{\"data\":[]}", out var request, options);

        client.List();

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

        Should.Throw<JsonException>(() => client.List());
    }

    [Fact]
    public void OperationsWithOptionalDataReturnEmptyDefaults()
    {
        var client = CreateClient("{}", out _);

        client.List().ShouldBeEmpty();
        client.History("web").ShouldBeEmpty();
        client.RepoList().ShouldBeEmpty();
        client.SearchRepo("chart").ShouldBeEmpty();
        var values = client.GetValues("web");
        values.ValueKind.ShouldBe(JsonValueKind.Object);
        values.EnumerateObject().Any().ShouldBeFalse();
        client.Template("web", "chart").ShouldBeEmpty();
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
        var client = new HelmClient(new HelmClientOptions { DefaultNamespace = "apps" }, native);

        client.List().ShouldBeEmpty();

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

        Should.Throw<InvalidOperationException>(() => client.List()).Message.ShouldContain("null response");
        native.WasFreed.ShouldBeFalse();
    }

    [Fact]
    public void NativeCallFreesResponseWhenItCannotBeDecoded()
    {
        var native = new FakeNativeApi("unused") { ReturnUndecodableString = true };
        var client = new HelmClient(new HelmClientOptions(), native);

        Should.Throw<InvalidOperationException>(() => client.List()).Message.ShouldContain("invalid response");
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

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
