namespace Pebbles.Configuration;

using Pebbles.Models;

/// <summary>
/// Hard-coded model configurations for Alibaba Cloud (Qwen OAuth equivalent).
/// These models are always available and cannot be overridden.
/// </summary>
internal static class AlibabaCloudModels
{
    public static readonly List<ModelProviderConfig> Models =
    [
        new ModelProviderConfig
        {
            Id = "qwen3.5-plus",
            Name = "Qwen 3.5 Plus",
            Description = "Efficient hybrid model with leading coding performance",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        },
        new ModelProviderConfig
        {
            Id = "qwen3-coder-plus",
            Name = "Qwen 3 Coder Plus",
            Description = "Specialized coding model with advanced reasoning",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        },
        new ModelProviderConfig
        {
            Id = "qwen3-coder-next",
            Name = "Qwen 3 Coder Next",
            Description = "Fast coding model for quick iterations",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = false }
        },
        new ModelProviderConfig
        {
            Id = "glm-4.7",
            Name = "GLM 4.7",
            Description = "Zhipu AI's general purpose model",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        },
        new ModelProviderConfig
        {
            Id = "glm-5",
            Name = "GLM 5",
            Description = "Zhipu AI's latest general purpose model",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        },
        new ModelProviderConfig
        {
            Id = "MiniMax-M2.5",
            Name = "MiniMax M2.5",
            Description = "MiniMax multimodal model",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        },
        new ModelProviderConfig
        {
            Id = "kimi-k2.5",
            Name = "Kimi K2.5",
            Description = "Moonshot AI's Kimi assistant model",
            EnvKey = "ALIBABA_CLOUD_API_KEY",
            BaseUrl = new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            Capabilities = new ModelCapabilities { Vision = true }
        }
    ];
}
