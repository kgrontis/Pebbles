namespace Pebbles.Configuration;

/// <summary>
/// Configuration options for Pebbles.
/// </summary>
public sealed class PebblesOptions
{
    public const string SectionName = "Pebbles";

    /// <summary>
    /// The default AI model to use.
    /// </summary>
    public string DefaultModel { get; set; } = "qwen3.5-plus";

    /// <summary>
    /// Available models for selection.
    /// </summary>
    public string[] AvailableModels { get; set; } =
    [
        "qwen3.5-plus",
        "qwen3-coder-plus",
        "qwen3-coder-next",
        "qwen3-max-2026-01-23",
        "glm-4.7",
        "glm-5",
        "MiniMax-M2.5",
        "kimi-k2.5"
    ];

    /// <summary>
    /// Cost per 1K input tokens (in dollars).
    /// </summary>
    public decimal InputCostPer1K { get; set; } = 0.0004m;

    /// <summary>
    /// Cost per 1K output tokens (in dollars).
    /// </summary>
    public decimal OutputCostPer1K { get; set; } = 0.0024m;

    /// <summary>
    /// Token estimation multiplier (words * multiplier = estimated tokens).
    /// </summary>
    public double TokenEstimationMultiplier { get; set; } = 1.3;

    /// <summary>
    /// AI provider to use ("mock" for testing, "dashscope" for Alibaba Cloud).
    /// </summary>
    public string Provider { get; set; } = "mock";

    /// <summary>
    /// DashScope API key (or set BAILIAN_CODING_PLAN_API_KEY environment variable).
    /// </summary>
    public string? DashScopeApiKey { get; set; }

    /// <summary>
    /// DashScope base URL (regional endpoints).
    /// </summary>
    public string DashScopeBaseUrl { get; set; } = "https://coding-intl.dashscope.aliyuncs.com/v1";

    /// <summary>
    /// System prompt for the AI.
    /// </summary>
    public string SystemPrompt { get; set; } = 
        "You are Pebbles, a helpful AI coding assistant. " +
        "You provide clear, concise answers with code examples when appropriate. " +
        "You communicate in a friendly but professional manner.";
}