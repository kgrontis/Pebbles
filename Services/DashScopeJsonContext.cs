using System.Text.Json.Serialization;

namespace Pebbles.Services;

/// <summary>
/// Source-generated JSON context for Native AOT compatibility.
/// </summary>
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatMessageItem))]
[JsonSerializable(typeof(StreamChunk))]
internal partial class DashScopeJsonContext : JsonSerializerContext
{
}