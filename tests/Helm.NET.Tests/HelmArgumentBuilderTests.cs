namespace Helm.NET.Tests;

public sealed class HelmArgumentBuilderTests
{
    private static string[] Args(params string[] values) => values;
    private static readonly string[] _s_valuesFiles = ["base.yaml", "prod.yaml"];
    private static readonly string[] _s_setValues = ["replicas=2", "image.tag=1.2"];

    [Fact]
    public void CreateChartUsesHelmCreateSyntax()
    {
        HelmArgumentBuilder.CreateChart("my-chart").ShouldBe(Args("create", "my-chart"));
    }

    [Fact]
    public void InstallIncludesReleaseOptionsAndRepeatedValues()
    {
        var actual = HelmArgumentBuilder.Install(new HelmInstallOptions(
            "my-release",
            "./my-chart",
            "apps",
            _s_valuesFiles,
            _s_setValues,
            CreateNamespace: true,
            Wait: true,
            Timeout: TimeSpan.FromMinutes(2)));

        actual.ShouldBe(Args(
            "install", "my-release", "./my-chart",
            "--namespace", "apps",
            "--values", "base.yaml",
            "--values", "prod.yaml",
            "--set", "replicas=2",
            "--set", "image.tag=1.2",
            "--create-namespace", "--wait", "--timeout", "2m"));
    }

    [Fact]
    public void InstallOmitsUnspecifiedOptions()
    {
        HelmArgumentBuilder.Install(new HelmInstallOptions("release", "chart"))
            .ShouldBe(Args("install", "release", "chart"));
    }

    [Theory]
    [InlineData(0.5, "30s")]
    [InlineData(2, "2m")]
    [InlineData(90, "1.5h")]
    public void InstallFormatsTimeoutAsGoDuration(double totalMinutes, string expected)
    {
        var options = new HelmInstallOptions("release", "chart", Timeout: TimeSpan.FromMinutes(totalMinutes));

        HelmArgumentBuilder.Install(options).TakeLast(2).ShouldBe(Args("--timeout", expected));
    }

    [Fact]
    public void UpgradeAddsInstallFlagOnlyWhenRequested()
    {
        var actual = HelmArgumentBuilder.Upgrade(new HelmUpgradeOptions("release", "repo/chart", InstallIfMissing: true));
        actual.ShouldBe(Args("upgrade", "release", "repo/chart", "--install"));
    }

    [Fact]
    public void UpgradeOmitsInstallFlagByDefault()
    {
        HelmArgumentBuilder.Upgrade(new HelmUpgradeOptions("release", "chart"))
            .ShouldBe(Args("upgrade", "release", "chart"));
    }

    [Fact]
    public void ListAddsNamespaceFilterAndOutput()
    {
        HelmArgumentBuilder.List(new HelmListOptions("apps", Filter: "^web", Output: "json"))
            .ShouldBe(Args("list", "--namespace", "apps", "--filter", "^web", "--output", "json"));
    }
}
