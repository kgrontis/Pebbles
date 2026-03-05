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
    public string DefaultModel { get; set; } = "pebbles-3.5-sonnet";

    /// <summary>
    /// Available models for selection.
    /// </summary>
    public string[] AvailableModels { get; set; } = [
        "pebbles-3.5-sonnet",
        "pebbles-3.5-opus",
        "pebbles-4-haiku",
        "pebbles-4-sonnet"
    ];

    /// <summary>
    /// Cost per 1K input tokens (in dollars).
    /// </summary>
    public decimal InputCostPer1K { get; set; } = 0.003m;

    /// <summary>
    /// Cost per 1K output tokens (in dollars).
    /// </summary>
    public decimal OutputCostPer1K { get; set; } = 0.015m;

    /// <summary>
    /// Token estimation multiplier (words * multiplier = estimated tokens).
    /// </summary>
    public double TokenEstimationMultiplier { get; set; } = 1.3;
}