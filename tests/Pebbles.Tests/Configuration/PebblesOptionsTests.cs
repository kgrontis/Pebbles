namespace Pebbles.Tests.Configuration;

using Pebbles.Configuration;

public class PebblesOptionsValidatorTests
{
    private readonly PebblesOptionsValidator _validator;

    public PebblesOptionsValidatorTests()
    {
        _validator = new PebblesOptionsValidator();
    }

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus", "qwen3-coder-plus"],
            InputCostPer1K = 0.0004m,
            OutputCostPer1K = 0.0024m,
            TokenEstimationMultiplier = 1.3,
            CompressionThreshold = 0.7,
            KeepRecentMessages = 6
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithMissingDefaultModel_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "",
            AvailableModels = ["qwen3.5-plus"]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("DefaultModel", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNullDefaultModel_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = null!,
            AvailableModels = ["qwen3.5-plus"]
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("DefaultModel", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithEmptyAvailableModels_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = []
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("AvailableModels", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNegativeInputCost_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            InputCostPer1K = -0.0004m
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("InputCostPer1K", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNegativeOutputCost_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            OutputCostPer1K = -0.0024m
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("OutputCostPer1K", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithZeroTokenMultiplier_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            TokenEstimationMultiplier = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("TokenEstimationMultiplier", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNegativeTokenMultiplier_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            TokenEstimationMultiplier = -1.3
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("TokenEstimationMultiplier", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithCompressionThresholdBelowZero_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            CompressionThreshold = -0.1
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("CompressionThreshold", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithCompressionThresholdAboveOne_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            CompressionThreshold = 1.5
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("CompressionThreshold", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithKeepRecentMessagesBelowOne_ReturnsFailure()
    {
        // Arrange
        var options = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            AvailableModels = ["qwen3.5-plus"],
            KeepRecentMessages = 0
        };

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("KeepRecentMessages", result.FailureMessage);
    }

    [Fact]
    public void GetContextWindowTokens_ReturnsCorrectValue_ForKnownModel()
    {
        // Arrange
        var options = new PebblesOptions();

        // Act
        var tokens = options.GetContextWindowTokens("qwen3.5-plus");

        // Assert
        Assert.Equal(1_000_000, tokens);
    }

    [Fact]
    public void GetContextWindowTokens_ReturnsDefault_ForUnknownModel()
    {
        // Arrange
        var options = new PebblesOptions();

        // Act
        var tokens = options.GetContextWindowTokens("unknown-model");

        // Assert
        Assert.Equal(128_000, tokens);
    }
}