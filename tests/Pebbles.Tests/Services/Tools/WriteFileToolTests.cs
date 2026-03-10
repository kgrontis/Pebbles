namespace Pebbles.Tests.Services.Tools;

using Pebbles.Services.Tools;
using System.Text.Json;

public class WriteFileToolTests : IDisposable
{
    private readonly WriteFileTool _tool;
    private readonly string _testDirectory;

    public WriteFileToolTests()
    {
        _tool = new WriteFileTool();
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArguments_CreatesFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var args = new { path = testFile, content = "Hello World" };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            result.Success.Should().BeTrue();
            result.Error.Should().BeNullOrEmpty();
            File.Exists(testFile).Should().BeTrue();
            File.ReadAllText(testFile).Should().Be("Hello World");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingPath_ReturnsError()
    {
        // Arrange
        var args = new { content = "Hello World" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("'path' is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingContent_ReturnsError()
    {
        // Arrange
        var args = new { path = "test.txt" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("'content' is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContent_ReturnsError()
    {
        // Arrange
        var args = new { path = "test.txt", content = (string?)null };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("'content' is required");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesParentDirectories_IfMissing()
    {
        // Arrange
        var nestedPath = Path.Combine(_testDirectory, "sub1", "sub2", "test.txt");
        var args = new { path = nestedPath, content = "Nested content" };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            result.Success.Should().BeTrue();
            File.Exists(nestedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(nestedPath)))
                Directory.Delete(Path.GetDirectoryName(nestedPath)!, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingFile_CreatesBackup()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "existing.txt");
        File.WriteAllText(testFile, "Original content");
        var args = new { path = testFile, content = "New content", createBackup = true };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            result.Success.Should().BeTrue();
            File.ReadAllText(testFile).Should().Be("New content");
            result.Content.Should().Contain("Backup:");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCreateBackupFalse_DoesNotCreateBackup()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "existing.txt");
        File.WriteAllText(testFile, "Original content");
        var args = new { path = testFile, content = "New content", createBackup = false };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            result.Success.Should().BeTrue();
            File.ReadAllText(testFile).Should().Be("New content");
            result.Content.Should().NotContain("Backup:");
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithSystemPath_ReturnsError()
    {
        // Arrange
        var args = new { path = @"C:\Windows\System32\test.txt", content = "Malicious" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("outside allowed");
    }

    [Fact]
    public async Task ExecuteAsync_WithTildePath_ResolvesToHomeDirectory()
    {
        // Arrange
        var homePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "test_pebbles.txt");
        var args = new { path = "~/test_pebbles.txt", content = "Home directory test" };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            result.Success.Should().BeTrue();
            File.Exists(homePath).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(homePath))
                File.Delete(homePath);
        }
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = _tool.GetDefinition();

        // Assert
        definition.Function.Should().NotBeNull();
        definition.Function!.Name.Should().Be("write_file");
        definition.Function.Description.Should().NotBeNullOrEmpty();
        definition.Function.Parameters.Properties.Should().ContainKey("path");
        definition.Function.Parameters.Properties.Should().ContainKey("content");
        definition.Function.Parameters.Required.Should().Contain("path");
        definition.Function.Parameters.Required.Should().Contain("content");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}