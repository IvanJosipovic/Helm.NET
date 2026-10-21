using Helm.NET;
using Helm.NET.Scenarios;
using Xunit;

namespace Helm.NET.Tests.E2E.AOT;

public sealed class HelmClientAotE2ETests
{
    [Fact]
    public void PublicApiScenariosPass()
    {
        var clusterLifecycle = string.Equals(
            Environment.GetEnvironmentVariable("HELM_TEST_CLUSTER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var helm = new HelmClient(new HelmClientOptions { DefaultNamespace = "helm-net-aot" });

        HelmClientScenarios.RunAll(helm, options => new HelmClient(options), clusterLifecycle);
    }
}
