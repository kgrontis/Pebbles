namespace Pebbles.Tests.Services;

using Pebbles.Models;
using Pebbles.Services;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class ToolRegistryTests
{
    [Fact]
    public void RegisterTool_AddsTool()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        var tool = new SimpleTestTool();

        // Act
        registry.RegisterTool(tool);

        // Assert
        var retrieved = registry.GetTool("test");
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Name);
    }

    [Fact]
    public void RegisterTool_Throws_WhenDuplicate()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new SimpleTestTool());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => registry.RegisterTool(new SimpleTestTool()));
    }

    [Fact]
    public void RegisterTool_Throws_WhenNull()
    {
        // Arrange
        var registry = new ToolRegistry(null);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.RegisterTool(null!));
    }

    [Fact]
    public void GetTool_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var registry = new ToolRegistry(null);

        // Act
        var result = registry.GetTool("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTool_Throws_WhenNameIsEmpty()
    {
        // Arrange
        var registry = new ToolRegistry(null);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.GetTool(""));
    }

    [Fact]
    public void GetTool_IsCaseInsensitive()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new SimpleTestTool());

        // Act
        var result = registry.GetTool("TEST");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetAllToolDefinitions_ReturnsAllDefinitions()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new SimpleTestTool());
        registry.RegisterTool(new AnotherTestTool());

        // Act
        var definitions = registry.GetAllToolDefinitions();

        // Assert
        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.Function?.Name == "test");
        Assert.Contains(definitions, d => d.Function?.Name == "another");
    }

    [Fact]
    public void GetAllToolDefinitions_ReturnsEmpty_WhenNoTools()
    {
        // Arrange
        var registry = new ToolRegistry(null);

        // Act
        var definitions = registry.GetAllToolDefinitions();

        // Assert
        Assert.Empty(definitions);
    }

    [Fact]
    public async Task ExecuteToolAsync_ReturnsError_WhenToolNotFound()
    {
        // Arrange
        var registry = new ToolRegistry(null);

        // Act
        var result = await registry.ExecuteToolAsync("nonexistent", "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unknown tool", result.Error, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ExecuteToolAsync_ExecutesTool()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new SimpleTestTool());

        // Act
        var result = await registry.ExecuteToolAsync("test", "{\"input\": \"hello\"}");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Executed with: {\"input\": \"hello\"}", result.Content);
    }

    [Fact]
    public async Task ExecuteToolAsync_HandlesToolException()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new ThrowingTestTool());

        // Act
        var result = await registry.ExecuteToolAsync("throwing", "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("failed", result.Error, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ExecuteToolAsync_PassesCancellationToken()
    {
        // Arrange
        var registry = new ToolRegistry(null);
        registry.RegisterTool(new CancellableTestTool());
        using var cts = new CancellationTokenSource();

        // Act
        var result = await registry.ExecuteToolAsync("cancellable", "{}", cts.Token);

        // Assert
        Assert.True(result.Success);
    }
}

internal sealed class SimpleTestTool : ITool
{
    public string Name => "test";
    public string Description => "A simple test tool";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters { Type = "object" }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = true,
            Content = $"Executed with: {arguments}"
        });
    }
}

internal sealed class AnotherTestTool : ITool
{
    public string Name => "another";
    public string Description => "Another test tool";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters { Type = "object" }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult { Success = true });
    }
}

internal sealed class ThrowingTestTool : ITool
{
    public string Name => "throwing";
    public string Description => "A tool that throws";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters { Type = "object" }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Tool failed!");
    }
}

internal sealed class CancellableTestTool : ITool
{
    public string Name => "cancellable";
    public string Description => "A cancellable tool";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters { Type = "object" }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ToolExecutionResult { Success = true });
    }
}