using System.Text.Json.Serialization;

namespace Pebbles.Services;

/// <summary>
/// Source-generated JSON context for optimized serialization.
/// </summary>
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatMessageItem))]
[JsonSerializable(typeof(ContentPart))]
[JsonSerializable(typeof(ImageUrl))]
[JsonSerializable(typeof(StreamOptions))]
[JsonSerializable(typeof(StreamChunk))]
[JsonSerializable(typeof(ChatResponseUsage))]
[JsonSerializable(typeof(PromptTokensDetails))]
[JsonSerializable(typeof(CompletionTokensDetails))]
internal sealed partial class AlibabaCloudJsonContext : JsonSerializerContext
{
}