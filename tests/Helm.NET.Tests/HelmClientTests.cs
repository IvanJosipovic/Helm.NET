using System.Text.Json;

namespace Helm.NET.Tests;

public sealed class HelmClientTests
{
    [Fact]
    public void ConstructorUsesDefaultOptions()
    {
        var client = new HelmClient();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void ExistingInterfaceImplementersReceiveDefaultsForNewOperations()
    {
        IHelmClient client = new LegacyHelmClient();

        client.ListReleases().ShouldBeEmpty();
        Should.Throw<NotSupportedException>(() => client.List());
    }

    [Fact]
    public void ListReleasesUsesDefaultsAndMapsTypedRecords()
    {
        var client = CreateClient(Response(releases: [NativeRelease()]), out var request);

        var releases = client.List();

        request().Operation.ShouldBe("list");
        request().Namespace.ShouldBe("default");
        request().AllNamespaces.ShouldBeFalse();
        request().Filter.ShouldBeNull();
        releases.ShouldHaveSingleItem().ShouldBe(Release());
    }

    [Fact]
    public void ListReleasesForwardsNamespaceAndFilterOptions()
    {
        var client = CreateClient(Response(), out var request);

        client.List(allNamespaces: true, filter: "^web$");

        request().AllNamespaces.ShouldBeTrue();
        request().Filter.ShouldBe("^web$");
    }

    [Fact]
    public void TemplateForwardsValuesFilesAndDynamicJsonValuesAndReturnsText()
    {
        using var valuesDocument = JsonDocument.Parse("""{"replicas":2,"configuration":{"nested":{"enabled":true,"items":[2,3]}}}""");
        var client = CreateClient(Response(text: "rendered yaml"), out var request);

        client.Template("web", "./chart", ["prod.yaml"], valuesDocument.RootElement).ShouldBe("rendered yaml");

        var captured = request();
        captured.Operation.ShouldBe("template");
        captured.ReleaseName.ShouldBe("web");
        captured.Chart.ShouldBe("./chart");
        captured.ValuesFiles.ShouldBe(["prod.yaml"]);
        var values = captured.Values!.Value;
        values.GetProperty("replicas").GetInt32().ShouldBe(2);
        var configuration = values.GetProperty("configuration");
        configuration.GetProperty("nested").GetProperty("enabled").GetBoolean().ShouldBeTrue();
        configuration.GetProperty("nested").GetProperty("items")[1].GetInt32().ShouldBe(3);
    }

    [Fact]
    public void TemplateDefaultsToNoValuesFilesOrValues()
    {
        var client = CreateClient(Response(text: "manifest"), out var request);

        client.Template("web", "chart").ShouldBe("manifest");

        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();
    }

    [Fact]
    public void TemplateWithHelmOptionsForwardsTypedOptions()
    {
        using var valuesDocument = JsonDocument.Parse("""{"replicas":2}""");
        var client = CreateClient(Response(text: "rendered"), out var readRequest);

        client.Template(
            "web", "chart", namespaceName: "apps", values: ["prod.yaml"], setValues: valuesDocument.RootElement,
            version: "1.2.3", apiVersions: ["v1"], kubeVersion: "1.31", includeCrds: true,
            skipTests: true, dependencyUpdate: true, isUpgrade: true, dryRun: "server", hideSecret: true,
            disableHooks: true, postRenderer: "renderer", postRendererArgs: ["--flag"], repo: "https://charts.test",
            username: "user", password: "password", caFile: "ca.pem", certFile: "client.pem", keyFile: "client.key",
            insecureSkipTlsVerify: true, passCredentials: true, plainHttp: true).ShouldBe("rendered");

        var request = readRequest();
        request.Namespace.ShouldBe("apps");
        request.ValuesFiles.ShouldBe(["prod.yaml"]);
        request.Values!.Value.GetProperty("replicas").GetInt32().ShouldBe(2);
        request.Version.ShouldBe("1.2.3");
        request.ApiVersions.ShouldBe(["v1"]);
        request.KubeVersion.ShouldBe("1.31");
        request.IncludeCrds.ShouldBeTrue();
        request.SkipTests.ShouldBeTrue();
        request.DependencyUpdate.ShouldBeTrue();
        request.IsUpgrade.ShouldBeTrue();
        request.DryRun.ShouldBe("server");
        request.HideSecret.ShouldBeTrue();
        request.DisableHooks.ShouldBeTrue();
        request.PostRenderer.ShouldBe("renderer");
        request.PostRendererArgs.ShouldBe(["--flag"]);
        request.Repo.ShouldBe("https://charts.test");
        request.RepositoryUsername.ShouldBe("user");
        request.RepositoryPassword.ShouldBe("password");
        request.CaFile.ShouldBe("ca.pem");
        request.CertFile.ShouldBe("client.pem");
        request.KeyFile.ShouldBe("client.key");
        request.InsecureSkipTlsVerify.ShouldBeTrue();
        request.PassCredentials.ShouldBeTrue();
        request.PlainHttp.ShouldBeTrue();
    }

    [Fact]
    public void InstallForwardsOptionsAndMapsRelease()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);
        using var valuesDocument = JsonDocument.Parse("""{"key":"value"}""");

        client.Install("web", "chart", ["values.yaml"], valuesDocument.RootElement, createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(12.5))
            .ShouldBe(Release());

        var captured = request();
        captured.Operation.ShouldBe("install");
        captured.CreateNamespace.ShouldBeTrue();
        captured.Wait.ShouldBeTrue();
        captured.TimeoutMilliseconds.ShouldBe(12500);
        captured.ValuesFiles.ShouldBe(["values.yaml"]);
        captured.Values!.Value.GetProperty("key").GetString().ShouldBe("value");
    }

    [Fact]
    public void InstallWithHelmOptionsForwardsFlagsAndNamespace()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var readRequest);

        client.Install(
            "web", "chart", namespaceName: "apps", values: ["values.yaml"], createNamespace: true,
            wait: true, timeout: TimeSpan.FromSeconds(9), version: "2.0.0", dryRun: "client",
            hideSecret: true, disableHooks: true, postRenderer: "renderer", postRendererArgs: ["--a"],
            waitForJobs: true, rollbackOnFailure: true, dependencyUpdate: true, skipCrds: true,
            devel: true, skipSchemaValidation: true, disableOpenApiValidation: true,
            description: "install test", waitStrategy: "watcher", replace: true, forceReplace: true,
            forceConflicts: true, takeOwnership: true, repo: "https://charts.test", username: "user",
            password: "password", caFile: "ca.pem", certFile: "client.pem", keyFile: "client.key",
            insecureSkipTlsVerify: true, passCredentials: true, plainHttp: true);

        var request = readRequest();
        request.Namespace.ShouldBe("apps");
        request.ValuesFiles.ShouldBe(["values.yaml"]);
        request.CreateNamespace.ShouldBeTrue();
        request.Wait.ShouldBeTrue();
        request.TimeoutMilliseconds.ShouldBe(9000);
        request.Version.ShouldBe("2.0.0");
        request.DryRun.ShouldBe("client");
        request.WaitForJobs.ShouldBeTrue();
        request.RollbackOnFailure.ShouldBeTrue();
        request.DependencyUpdate.ShouldBeTrue();
        request.SkipCrds.ShouldBeTrue();
        request.Devel.ShouldBeTrue();
        request.SkipSchemaValidation.ShouldBeTrue();
        request.DisableOpenApiValidation.ShouldBeTrue();
        request.Description.ShouldBe("install test");
        request.WaitStrategy.ShouldBe("watcher");
        request.Replace.ShouldBeTrue();
        request.ForceReplace.ShouldBeTrue();
        request.ForceConflicts.ShouldBeTrue();
        request.TakeOwnership.ShouldBeTrue();
        request.InsecureSkipTlsVerify.ShouldBeTrue();
        request.PassCredentials.ShouldBeTrue();
        request.PlainHttp.ShouldBeTrue();
    }

    [Fact]
    public void UpgradeForwardsInstallIfMissingAndWaitOptions()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Upgrade("web", "chart", installIfMissing: true, createNamespace: true, wait: true, timeout: TimeSpan.FromSeconds(7))
            .ShouldBe(Release());

        var captured = request();
        captured.Operation.ShouldBe("upgrade");
        captured.InstallIfMissing.ShouldBeTrue();
        captured.CreateNamespace.ShouldBeTrue();
        captured.Wait.ShouldBeTrue();
        captured.TimeoutMilliseconds.ShouldBe(7000);
    }

    [Fact]
    public void InstallAndUpgradeUseFalseAndZeroOptionDefaults()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Install("web", "chart");
        request().CreateNamespace.ShouldBeFalse();
        request().Wait.ShouldBeFalse();
        request().TimeoutMilliseconds.ShouldBe(0);
        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();

        client.Upgrade("web", "chart");
        request().InstallIfMissing.ShouldBeFalse();
        request().CreateNamespace.ShouldBeFalse();
        request().Wait.ShouldBeFalse();
        request().TimeoutMilliseconds.ShouldBe(0);
        request().ValuesFiles.ShouldBeNull();
        request().Values.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListAndHistoryMapHookExecutionAndDeletePolicies(bool history)
    {
        var release = NativeRelease(hooks:
        [
            new NativeHelmHook
            {
                Name = "smoke",
                Kind = "Pod",
                Events = ["test"],
                LastRun = new NativeHelmHookExecution
                {
                    StartedAt = DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
                    CompletedAt = DateTimeOffset.Parse("2026-01-02T03:04:06Z", System.Globalization.CultureInfo.InvariantCulture),
                    Phase = "Succeeded"
                },
                DeletePolicies = ["before-hook-creation"]
            }
        ]);
        var client = CreateClient(Response(releases: [release]), out _);

        var result = history ? client.History("web").ShouldHaveSingleItem() : client.List().ShouldHaveSingleItem();

        var hook = result.Hooks.ShouldHaveSingleItem();
        hook.StartedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.CompletedAt.ShouldBe(DateTimeOffset.Parse("2026-01-02T03:04:06Z", System.Globalization.CultureInfo.InvariantCulture));
        hook.Phase.ShouldBe("Succeeded");
        hook.DeletePolicies.ShouldHaveSingleItem().ShouldBe("before-hook-creation");
    }

    [Fact]
    public void HistoryForwardsMax()
    {
        var client = CreateClient(Response(), out var request);

        client.History("web", max: 5);

        request().Max.ShouldBe(5);
    }

    [Fact]
    public void StatusMapsTypedRelease()
    {
        var client = CreateClient(Response(release: NativeRelease()), out var request);

        client.Status("web").ShouldBe(Release());

        request().ReleaseName.ShouldBe("web");
        request().Operation.ShouldBe("status");
    }

    [Fact]
    public void HistoryMapsTypedReleaseArray()
    {
        var previous = NativeRelease(revision: 2);
        var client = CreateClient(Response(releases: [NativeRelease(), previous]), out var request);

        client.History("web").ShouldBe([Release(), Release(2)]);

        request().Operation.ShouldBe("history");
        request().ReleaseName.ShouldBe("web");
    }

    [Fact]
    public void RollbackForwardsRevisionAndWait()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web", 2, wait: true);

        var captured = request();
        captured.Operation.ShouldBe("rollback");
        captured.ReleaseName.ShouldBe("web");
        captured.Revision.ShouldBe(2);
        captured.Wait.ShouldBeTrue();
    }

    [Fact]
    public void RollbackDefaultsToPreviousRevisionWithoutWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web");

        request().Revision.ShouldBe(0);
        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void RollbackWithRevisionDefaultsToNotWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Rollback("web", 4);

        request().Revision.ShouldBe(4);
        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void UninstallForwardsWaitOption()
    {
        var client = CreateClient(Response(), out var request);

        client.Uninstall("web", wait: true);

        request().Operation.ShouldBe("uninstall");
        request().ReleaseName.ShouldBe("web");
        request().Wait.ShouldBeTrue();
    }

    [Fact]
    public void UninstallDefaultsToNotWaiting()
    {
        var client = CreateClient(Response(), out var request);

        client.Uninstall("web");

        request().Wait.ShouldBeFalse();
    }

    [Fact]
    public void GetValuesMapsDynamicJsonAndUsesConfiguredNamespace()
    {
        var client = CreateClient(Response(values: ParseJsonElement("""{"replicas":3,"enabled":true,"nested":{"zone":"west"}}""")), out var request,
            new HelmClientOptions { DefaultNamespace = "apps" });

        var values = client.GetValues("web");

        values.GetProperty("replicas").GetInt32().ShouldBe(3);
        values.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        values.GetProperty("nested").GetProperty("zone").GetString().ShouldBe("west");
        request().Namespace.ShouldBe("apps");
        request().Operation.ShouldBe("values");
    }

    [Fact]
    public void ListRepositoriesMapsTypedRepositoryArray()
    {
        var expected = new HelmRepository("stable", "https://example.test");
        var client = CreateClient(Response(repositories: [expected]), out var request);

        client.ListRepositories().ShouldHaveSingleItem().ShouldBe(expected);

        request().Operation.ShouldBe("repoList");
    }

    [Fact]
    public void SearchRepoMapsTypedResultsAndOptions()
    {
        var expected = new HelmChartSearchResult("stable/nginx", "nginx", "1.2.3", "Nginx chart", "2.0");
        var client = CreateClient(Response(chartSearchResults: [expected]), out var readRequest);

        client.SearchRepo("nginx", versions: true, devel: true, regexp: true, version: ">=1.0", failOnNoResult: true)
            .ShouldHaveSingleItem().ShouldBe(expected);

        var request = readRequest();
        request.Operation.ShouldBe("chartSearch");
        request.SearchTerm.ShouldBe("nginx");
        request.Versions.ShouldBeTrue();
        request.Devel.ShouldBeTrue();
        request.Regexp.ShouldBeTrue();
        request.VersionConstraint.ShouldBe(">=1.0");
        request.FailOnNoResult.ShouldBeTrue();
    }

    [Fact]
    public void ShowValuesAndGetManifestReturnTextAndForwardOptions()
    {
        var client = CreateClient(Response(text: "values: {}"), out var readRequest);

        client.ShowValues("stable/nginx", version: "1.2.3", repo: "https://charts.test", username: "user", password: "password")
            .ShouldBe("values: {}");
        var showValuesRequest = readRequest();
        showValuesRequest.Operation.ShouldBe("showValues");
        showValuesRequest.Chart.ShouldBe("stable/nginx");
        showValuesRequest.Version.ShouldBe("1.2.3");
        showValuesRequest.Repo.ShouldBe("https://charts.test");
        showValuesRequest.RepositoryUsername.ShouldBe("user");
        showValuesRequest.RepositoryPassword.ShouldBe("password");

        client.GetManifest("web", namespaceName: "apps", revision: 2).ShouldBe("values: {}");
        var manifestRequest = readRequest();
        manifestRequest.Operation.ShouldBe("getManifest");
        manifestRequest.Namespace.ShouldBe("apps");
        manifestRequest.Revision.ShouldBe(2);
    }

    [Fact]
    public void AddRepositoryForwardsCredentials()
    {
        var client = CreateClient(Response(), out var request);

        client.RepoAdd("private", "https://example.test", "user", "secret");

        var captured = request();
        captured.Operation.ShouldBe("repoAdd");
        captured.RepositoryName.ShouldBe("private");
        captured.RepositoryURL.ShouldBe("https://example.test");
        captured.RepositoryUsername.ShouldBe("user");
        captured.RepositoryPassword.ShouldBe("secret");
    }

    [Fact]
    public void AddRepositoryDefaultsCredentialsToNull()
    {
        var client = CreateClient(Response(), out var request);

        client.AddRepository("public", "https://example.test");

        request().RepositoryUsername.ShouldBeNull();
        request().RepositoryPassword.ShouldBeNull();
    }

    [Fact]
    public void RepoAddForwardsForceUpdate()
    {
        var client = CreateClient(Response(), out var request);

        client.RepoAdd("stable", "https://example.test", forceUpdate: true);

        request().ForceUpdate.ShouldBeTrue();
    }

    [Fact]
    public void RepoRemoveSendsName()
    {
        var client = CreateClient(Response(), out var request);

        client.RepoRemove("stable");

        request().RepositoryName.ShouldBe("stable");
        request().Operation.ShouldBe("repoRemove");
    }

    [Fact]
    public void UpdateRepositoriesDispatchesUpdateOperation()
    {
        var client = CreateClient(Response(), out var request);

        client.RepoUpdate();

        request().Operation.ShouldBe("repoUpdate");
    }

    [Fact]
    public void InvokeMergesAllConfiguredOptionsIntoRequest()
    {
        var options = new HelmClientOptions
        {
            Kubeconfig = "kube.yaml", KubeContext = "dev", DefaultNamespace = "apps",
            RepositoryConfig = "repositories.yaml", RepositoryCache = "cache",
            RegistryConfig = "registry.json", HelmDriver = "configmap"
        };
        var client = CreateClient(Response(), out var request, options);

        client.List();

        var captured = request();
        captured.Kubeconfig.ShouldBe("kube.yaml");
        captured.KubeContext.ShouldBe("dev");
        captured.Namespace.ShouldBe("apps");
        captured.RepositoryConfig.ShouldBe("repositories.yaml");
        captured.RepositoryCache.ShouldBe("cache");
        captured.RegistryConfig.ShouldBe("registry.json");
        captured.HelmDriver.ShouldBe("configmap");
    }

    [Fact]
    public void InvokeThrowsActionExceptionWhenResponseHasError()
    {
        var client = CreateClient(Response(error: "release not found", release: NativeRelease()), out _);

        Should.Throw<HelmActionException>(() => client.Status("missing")).Message.ShouldBe("release not found");
    }

    [Fact]
    public void OperationsWithMissingOptionalResultsReturnExistingEmptyDefaults()
    {
        var client = CreateClient(Response(), out _);

        client.List().ShouldBeEmpty();
        client.History("web").ShouldBeEmpty();
        client.ListRepositories().ShouldBeEmpty();
        var values = client.GetValues("web");
        values.ValueKind.ShouldBe(JsonValueKind.Object);
        values.EnumerateObject().Any().ShouldBeFalse();
        client.Template("web", "chart").ShouldBeEmpty();
        client.GetManifest("web").ShouldBeEmpty();
    }

    [Fact]
    public void InstallThrowsWhenResponseHasNoRelease()
    {
        var client = CreateClient(Response(), out _);

        Should.Throw<InvalidOperationException>(() => client.Install("web", "chart"));
    }

    [Fact]
    public void StatusThrowsWhenResponseHasNoRelease()
    {
        var client = CreateClient(Response(), out _);

        Should.Throw<InvalidOperationException>(() => client.Status("web"));
    }

    private static HelmClient CreateClient(
        NativeHelmResponse response,
        out Func<NativeHelmRequest> readRequest,
        HelmClientOptions? options = null)
    {
        NativeHelmRequest? captured = null;
        var nativeApi = new FakeNativeApi(request =>
        {
            captured = request;
            return response;
        });
        var client = new HelmClient(options ?? new HelmClientOptions(), nativeApi);
        readRequest = () => captured ?? throw new InvalidOperationException("No native request was captured.");
        return client;
    }

    private sealed class LegacyHelmClient : IHelmClient
    {
        public IReadOnlyList<HelmRelease> ListReleases() => [];
        public IReadOnlyList<HelmRelease> ListReleases(bool allNamespaces, string? filter = null) => [];
        public string Template(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null) => string.Empty;
        public HelmRelease Install(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null) => throw new NotSupportedException();
        public HelmRelease Upgrade(string releaseName, string chart, IEnumerable<string>? valuesFiles = null, JsonElement? values = null, bool installIfMissing = false, bool createNamespace = false, bool wait = false, TimeSpan? timeout = null) => throw new NotSupportedException();
        public HelmRelease Status(string releaseName) => throw new NotSupportedException();
        public IReadOnlyList<HelmRelease> History(string releaseName) => [];
        public void Rollback(string releaseName, int revision = 0) { }
        public void Rollback(string releaseName, int revision, bool wait) { }
        public void Uninstall(string releaseName) { }
        public void Uninstall(string releaseName, bool wait) { }
        public JsonElement GetValues(string releaseName) => ParseJsonElement("{}");
        public IReadOnlyList<HelmRepository> ListRepositories() => [];
        public void AddRepository(string name, string url, string? username = null, string? password = null) { }
        public void RemoveRepository(string name) { }
        public void UpdateRepositories() { }
    }

    private sealed class FakeNativeApi(Func<NativeHelmRequest, NativeHelmResponse> execute) : IHelmNativeApi
    {
        public NativeHelmResponse Execute(NativeHelmRequest request) => execute(request);
    }

    private static NativeHelmResponse Response(
        string? error = null,
        string? text = null,
        JsonElement? values = null,
        NativeHelmRelease? release = null,
        NativeHelmRelease[]? releases = null,
        HelmRepository[]? repositories = null,
        HelmChartSearchResult[]? chartSearchResults = null) => new()
        {
            Error = error,
            Text = text,
            Values = values,
            Release = release,
            Releases = releases,
            Repositories = repositories,
            ChartSearchResults = chartSearchResults
        };

    private static JsonElement ParseJsonElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static NativeHelmRelease NativeRelease(int revision = 3, NativeHelmHook[]? hooks = null) => new()
    {
        Name = "web",
        Namespace = "apps",
        Revision = revision,
        Updated = DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture),
        Status = "deployed",
        Chart = "nginx-1.2.3",
        AppVersion = "2.0",
        Hooks = hooks ?? []
    };

    private static HelmRelease Release(int revision = 3) => new(
        "web", "apps", revision, DateTimeOffset.Parse("2026-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture), "deployed", "nginx-1.2.3", "2.0");
}
