namespace Helm.NET.Tests;

public sealed class HelmExecutableResolverTests
{
    [Fact]
    public void ExplicitPathTakesPrecedence()
    {
        var result = HelmExecutableResolver.Resolve(new HelmClientOptions { ExecutablePath = "custom-helm" }, new HelmPlatform("linux-x64", "helm"), "does-not-exist");
        result.ShouldBe("custom-helm");
    }

    [Fact]
    public void BundledAssetIsResolvedFromRidDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "runtimes", "linux-x64", "native", "helm");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        try
        {
            HelmExecutableResolver.Resolve(new HelmClientOptions(), new HelmPlatform("linux-x64", "helm"), root).ShouldBe(path);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DisabledBundlingFallsBackToPathLookup()
    {
        HelmExecutableResolver.Resolve(new HelmClientOptions { UseBundledExecutable = false }, new HelmPlatform("linux-x64", "helm"), "missing").ShouldBe("helm");
    }

    [Fact]
    public void RootBundledAssetIsResolved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "helm");
        File.WriteAllText(path, "test");
        try { HelmExecutableResolver.Resolve(new HelmClientOptions(), new HelmPlatform("linux-x64", "helm"), root).ShouldBe(path); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EmbeddedAssetIsExtractedWhenOutputAssetIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = HelmExecutableResolver.Resolve(
                new HelmClientOptions { ExecutableCacheDirectory = root },
                new HelmPlatform("linux-x64", "helm"),
                Path.Combine(root, "missing-output"));

            path.ShouldBe(Path.Combine(root, "v4.3.0", "linux-x64", "helm"));
            File.Exists(path).ShouldBeTrue();
            new FileInfo(path).Length.ShouldBeGreaterThan(0);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void EmbeddedAssetReusesValidCachedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new HelmClientOptions { ExecutableCacheDirectory = root };
            var platform = new HelmPlatform("linux-x64", "helm");
            var firstPath = HelmExecutableResolver.Resolve(options, platform, Path.Combine(root, "missing-output"));
            var firstWriteTime = File.GetLastWriteTimeUtc(firstPath);

            var secondPath = HelmExecutableResolver.Resolve(options, platform, Path.Combine(root, "missing-output"));

            secondPath.ShouldBe(firstPath);
            File.GetLastWriteTimeUtc(secondPath).ShouldBe(firstWriteTime);
        }
        finally { Directory.Delete(root, true); }
    }
}
