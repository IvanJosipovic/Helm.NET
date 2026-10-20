using System.Text.Json.Serialization;

namespace Helm.NET;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HelmRelease[]))]
internal partial class HelmJsonContext : JsonSerializerContext
{
}
