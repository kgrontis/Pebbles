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
        result.Succeeded.Should().BeTrue();
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("DefaultModel");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("DefaultModel");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("AvailableModels");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("InputCostPer1K");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("OutputCostPer1K");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TokenEstimationMultiplier");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("TokenEstimationMultiplier");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("CompressionThreshold");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("CompressionThreshold");
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
        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("KeepRecentMessages");
    }

    [Fact]
    public void GetContextWindowTokens_ReturnsCorrectValue_ForKnownModel()
    {
        // Arrange
        var options = new PebblesOptions();

        // Act
        var tokens = options.GetContextWindowTokens("qwen3.5-plus");

        // Assert
        tokens.Should().Be(1_000_000);
    }

    [Fact]
    public void GetContextWindowTokens_ReturnsDefault_ForUnknownModel()
    {
        // Arrange
        var options = new PebblesOptions();

        // Act
        var tokens = options.GetContextWindowTokens("unknown-model");

        // Assert
        tokens.Should().Be(128_000);
    }
}