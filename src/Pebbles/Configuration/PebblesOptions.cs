namespace Pebbles.Configuration;

/// <summary>
/// Configuration options for Pebbles.
/// </summary>
#pragma warning disable CA1515 // Consider making public types internal
public sealed class PebblesOptions
#pragma warning restore CA1515 // Consider making public types internal
{
    /// <summary>
    /// The Section Name in appsettings.json for Pebbles configuration.
    /// </summary>
    public const string SectionName = "Pebbles";

    /// <summary>
    /// HTTP client timeout in seconds. Default is 120 seconds.
    /// </summary>
    public int HttpClientTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// The default AI model to use.
    /// </summary>
    public string DefaultModel { get; set; } = "qwen3.5-plus";

    /// <summary>
    /// Available models for selection.
    /// </summary>
    public IReadOnlyList<string> AvailableModels { get; } =
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
    /// AI provider to use ("mock" for testing, "alibabacloud" for Alibaba Cloud).
    /// </summary>
    public string Provider { get; set; } = "mock";

    /// <summary>
    /// Alibaba Cloud API key (or set ALIBABA_CLOUD_API_KEY environment variable).
    /// </summary>
    public string? AlibabaCloudApiKey { get; set; }

    /// <summary>
    /// Alibaba Cloud base URL (Coding Plan endpoint).
    /// </summary>
    public Uri AlibabaCloudBaseUrl { get; set; } = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1");

    /// <summary>
    /// OpenAI API key (or set OPENAI_API_KEY environment variable).
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// OpenAI base URL.
    /// </summary>
    public Uri OpenAiBaseUrl { get; set; } = new Uri("https://api.openai.com/v1");

    /// <summary>
    /// Anthropic API key (or set ANTHROPIC_API_KEY environment variable).
    /// </summary>
    public string? AnthropicApiKey { get; set; }

    /// <summary>
    /// Anthropic base URL.
    /// </summary>
    public Uri AnthropicBaseUrl { get; set; } = new Uri("https://api.anthropic.com");

    /// <summary>
    /// Enable automatic context compression when threshold is reached.
    /// </summary>
    public bool AutoCompressionEnabled { get; set; } = true;

    /// <summary>
    /// Threshold for triggering auto-compression (0.0-1.0 of context window).
    /// Default 0.7 means compress when 70% of context window is used.
    /// </summary>
    public double CompressionThreshold { get; set; } = 0.7;

    /// <summary>
    /// Number of recent messages to keep verbatim during compaction.
    /// Older messages will be summarized.
    /// </summary>
    public int KeepRecentMessages { get; set; } = 6;

    /// <summary>
    /// Context window token limits per model (in thousands).
    /// </summary>
    public Dictionary<string, int> ModelContextWindows { get; } = new()
    {
        ["qwen3.5-plus"] = 1_000_000,
        ["qwen3-coder-plus"] = 1_000_000,
        ["qwen3-coder-next"] = 262_144,
        ["qwen3-max-2026-01-23"] = 262_144,
        ["glm-4.7"] = 202_752,
        ["glm-5"] = 202_752,
        ["MiniMax-M2.5"] = 1_000_000,
        ["kimi-k2.5"] = 262_144
    };

    /// <summary>
    /// Gets the context window size for a specific model.
    /// </summary>
    public int GetContextWindowTokens(string model)
    {
        return ModelContextWindows.TryGetValue(model, out var tokens) ? tokens : 128_000;
    }
}