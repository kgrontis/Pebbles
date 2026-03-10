namespace Pebbles.Tests.Services.Tools;

using Pebbles.Services.Tools;
using System.Text.Json;

public class ShellToolTests
{
    private readonly ShellTool _tool;

    public ShellToolTests()
    {
        _tool = new ShellTool();
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCommand_ReturnsError()
    {
        // Arrange
        var args = new { };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("'command' is required", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithDangerousCommand_ReturnsError()
    {
        // Arrange
        var args = new { command = "rm -rf /" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Dangerous command", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithInteractiveCommand_ReturnsError()
    {
        // Arrange
        var args = new { command = "sudo apt-get update" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Interactive command", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidWorkingDirectory_ReturnsError()
    {
        // Arrange
        var args = new { command = "dir", workingDirectory = @"C:\NonExistentPath12345" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Error);
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = _tool.GetDefinition();

        // Assert
        Assert.NotNull(definition.Function);
        Assert.Equal("run_command", definition.Function.Name);
        Assert.NotEmpty(definition.Function.Description);
        Assert.Contains("command", definition.Function.Parameters.Properties.Keys);
        Assert.Contains("command", definition.Function.Parameters.Required);
    }
}