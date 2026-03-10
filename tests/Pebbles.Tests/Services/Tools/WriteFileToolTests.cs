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
            Assert.True(result.Success);
            Assert.Null(result.Error);
            Assert.True(File.Exists(testFile));
            Assert.Equal("Hello World", File.ReadAllText(testFile));
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
        Assert.False(result.Success);
        Assert.Contains("'path' is required", result.Error);
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
        Assert.False(result.Success);
        Assert.Contains("'content' is required", result.Error);
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
        Assert.False(result.Success);
        Assert.Contains("'content' is required", result.Error);
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
            Assert.True(result.Success);
            Assert.True(File.Exists(nestedPath));
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
            Assert.True(result.Success);
            Assert.Equal("New content", File.ReadAllText(testFile));
            Assert.Contains("Backup:", result.Content);
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
            Assert.True(result.Success);
            Assert.Equal("New content", File.ReadAllText(testFile));
            Assert.DoesNotContain("Backup:", result.Content);
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
        Assert.False(result.Success);
        Assert.Contains("outside allowed", result.Error);
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
            Assert.True(result.Success);
            Assert.True(File.Exists(homePath));
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
        Assert.NotNull(definition.Function);
        Assert.Equal("write_file", definition.Function.Name);
        Assert.NotEmpty(definition.Function.Description);
        Assert.Contains("path", definition.Function.Parameters.Properties.Keys);
        Assert.Contains("content", definition.Function.Parameters.Properties.Keys);
        Assert.Contains("path", definition.Function.Parameters.Required);
        Assert.Contains("content", definition.Function.Parameters.Required);
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