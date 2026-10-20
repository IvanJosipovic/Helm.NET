using System.Reflection;

namespace Helm.NET;

internal static class HelmExecutableResolver
{
    private static readonly string BundledHelmVersion = typeof(HelmExecutableResolver).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(metadata => metadata.Key == "HelmVersion")
        .Value!;

    public static string Resolve(HelmClientOptions options, HelmPlatform platform, string? baseDirectory = null, string? cacheDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(options.ExecutablePath)) return options.ExecutablePath!;
        if (options.UseBundledExecutable)
        {
            var root = baseDirectory ?? AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(root, platform.FileName),
                Path.Combine(root, "runtimes", platform.Rid, "native", platform.FileName)
            };
            var path = candidates.FirstOrDefault(File.Exists);
            if (path is not null)
            {
                EnsureUnixExecutable(path, platform);
                return path;
            }

            var embeddedPath = ResolveEmbeddedExecutable(platform, cacheDirectory ?? options.ExecutableCacheDirectory);
            if (embeddedPath is not null) return embeddedPath;
        }

        return platform.FileName;
    }

    private static string? ResolveEmbeddedExecutable(HelmPlatform platform, string? cacheDirectory)
    {
        var resourceName = $"Helm.NET.Resources.{platform.Rid}.{platform.FileName}";
        var assembly = typeof(HelmExecutableResolver).Assembly;
        using var resource = assembly.GetManifestResourceStream(resourceName);
        if (resource is null) return null;

        var expectedHash = ComputeHash(resource);
        var root = cacheDirectory ?? GetDefaultCacheDirectory();
        var directory = Path.Combine(root, BundledHelmVersion, platform.Rid);
        var path = Path.Combine(directory, platform.FileName);
        Directory.CreateDirectory(directory);

        if (!IsValidFile(path, expectedHash))
        {
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using var source = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded Helm executable resource '{resourceName}' was not found.");
                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(destination);
                    destination.Flush();
                }

                try
                {
                    File.Move(temporaryPath, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    File.Delete(temporaryPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        EnsureUnixExecutable(path, platform);
        return path;
    }

    private static string GetDefaultCacheDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.Combine(Path.GetTempPath(), "Helm.NET")
            : Path.Combine(localApplicationData, "Helm.NET");
    }

    private static byte[] ComputeHash(Stream stream)
    {
        using var algorithm = System.Security.Cryptography.SHA256.Create();
        return algorithm.ComputeHash(stream);
    }

    private static bool IsValidFile(string path, byte[] expectedHash)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var file = File.OpenRead(path);
            return ComputeHash(file).SequenceEqual(expectedHash);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void EnsureUnixExecutable(string path, HelmPlatform platform)
    {
        if (platform.FileName == "helm.exe") return;
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path.Replace("\\\"", "\\\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The command will provide the actionable error if chmod is unavailable.
        }
    }
}
