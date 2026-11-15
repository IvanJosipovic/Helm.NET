using System.Text.Json;
using System.Text.Json.Serialization;

namespace Helm.NET;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JsonElement))]
internal partial class NativeJsonContext : JsonSerializerContext;
