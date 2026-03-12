using System.Text.Json.Serialization;

namespace Pebbles.Services;

/// <summary>
/// Source-generated JSON context for optimized serialization.
/// </summary>
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatMessageItem))]
[JsonSerializable(typeof(StreamChunk))]
internal sealed partial class AlibabaCloudJsonContext : JsonSerializerContext
{
}