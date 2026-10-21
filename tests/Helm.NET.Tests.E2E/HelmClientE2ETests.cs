using Helm.NET.Scenarios;

namespace Helm.NET.Tests.E2E;

public sealed class HelmClientE2ETests
{
    [Fact]
    public void PublicApiScenariosPassAgainstKind()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("HELM_TEST_CLUSTER"), "true", StringComparison.OrdinalIgnoreCase))
            throw Xunit.Sdk.SkipException.ForSkip("Set HELM_TEST_CLUSTER=true to run the Kind-backed end-to-end scenarios.");

        var helm = new HelmClient(new HelmClientOptions
        {
            Namespace = $"helm-net-e2e-{Guid.NewGuid():N}"
        });

        HelmClientScenarios.RunAll(helm, options => new HelmClient(options), clusterLifecycle: true);
    }
}
