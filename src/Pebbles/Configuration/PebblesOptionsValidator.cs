using Microsoft.Extensions.Options;

namespace Pebbles.Configuration;

/// <summary>
/// Validator for PebblesOptions configuration.
/// </summary>
public sealed class PebblesOptionsValidator : IValidateOptions<PebblesOptions>
{
    private static readonly HashSet<string> ValidProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ProviderNames.Mock,
        ProviderNames.AlibabaCloud,
        ProviderNames.OpenAI,
        ProviderNames.Anthropic,
        ProviderNames.DashScope // Legacy support
    };

    public ValidateOptionsResult Validate(string? name, PebblesOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            return ValidateOptionsResult.Fail("DefaultModel is required.");
        }

        if (options.AvailableModels is null || options.AvailableModels.Count == 0)
        {
            return ValidateOptionsResult.Fail("AvailableModels must contain at least one model.");
        }

        // Validate DefaultModel is in AvailableModels
        if (!options.AvailableModels.Contains(options.DefaultModel, StringComparer.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"DefaultModel '{options.DefaultModel}' is not in AvailableModels. " +
                $"Valid options: {string.Join(", ", options.AvailableModels)}");
        }

        // Validate Provider
        if (!string.IsNullOrWhiteSpace(options.Provider) && !ValidProviders.Contains(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"Provider must be one of: {string.Join(", ", ValidProviders)}. Got '{options.Provider}'.");
        }

        if (options.InputCostPer1K < 0)
        {
            return ValidateOptionsResult.Fail("InputCostPer1K cannot be negative.");
        }

        if (options.OutputCostPer1K < 0)
        {
            return ValidateOptionsResult.Fail("OutputCostPer1K cannot be negative.");
        }

        if (options.TokenEstimationMultiplier <= 0)
        {
            return ValidateOptionsResult.Fail("TokenEstimationMultiplier must be positive.");
        }

        if (options.CompressionThreshold is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("CompressionThreshold must be between 0 and 1.");
        }

        if (options.KeepRecentMessages < 1)
        {
            return ValidateOptionsResult.Fail("KeepRecentMessages must be at least 1.");
        }

        if (options.HttpClientTimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("HttpClientTimeoutSeconds must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}