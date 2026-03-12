namespace Pebbles.Configuration;

/// <summary>
/// Constants for AI provider names.
/// </summary>
internal static class ProviderNames
{
    /// <summary>
    /// Mock provider for testing.
    /// </summary>
    public const string Mock = "mock";

    /// <summary>
    /// Alibaba Cloud (DashScope) provider.
    /// </summary>
    public const string AlibabaCloud = "alibabacloud";

    /// <summary>
    /// OpenAI provider.
    /// </summary>
    public const string OpenAI = "openai";

    /// <summary>
    /// Anthropic provider.
    /// </summary>
    public const string Anthropic = "anthropic";

    /// <summary>
    /// Legacy DashScope provider name (maps to AlibabaCloud).
    /// </summary>
    public const string DashScope = "dashscope";
}