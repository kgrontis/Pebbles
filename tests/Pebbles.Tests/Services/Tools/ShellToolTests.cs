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
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("'command' is required");
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
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Dangerous command");
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
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Interactive command");
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
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = _tool.GetDefinition();

        // Assert
        definition.Function.Should().NotBeNull();
        definition.Function!.Name.Should().Be("run_command");
        definition.Function.Description.Should().NotBeNullOrEmpty();
        definition.Function.Parameters.Properties.Should().ContainKey("command");
        definition.Function.Parameters.Required.Should().Contain("command");
    }
}