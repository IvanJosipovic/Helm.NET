using System.Runtime.InteropServices;

namespace Helm.NET.Tests;

public sealed class HelmPlatformTests
{
    [Theory]
    [InlineData("Windows", Architecture.X64, "win-x64", "helm.exe")]
    [InlineData("Windows", Architecture.Arm64, "win-arm64", "helm.exe")]
    [InlineData("Linux", Architecture.X64, "linux-x64", "helm")]
    [InlineData("Linux", Architecture.Arm64, "linux-arm64", "helm")]
    [InlineData("Darwin", Architecture.X64, "osx-x64", "helm")]
    [InlineData("Darwin", Architecture.Arm64, "osx-arm64", "helm")]
    public void FromMapsSupportedPlatforms(string os, Architecture architecture, string rid, string fileName)
    {
        var platform = HelmPlatform.From(os, architecture);
        platform.Rid.ShouldBe(rid);
        platform.FileName.ShouldBe(fileName);
    }

    [Fact]
    public void FromRejectsUnsupportedArchitecture() => Should.Throw<PlatformNotSupportedException>(() => HelmPlatform.From("Linux", Architecture.X86));

    [Fact]
    public void FromRejectsUnsupportedOperatingSystem() => Should.Throw<PlatformNotSupportedException>(() => HelmPlatform.From("FreeBSD", Architecture.X64));
}
