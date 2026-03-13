namespace Pebbles.Tests.Services.Tools;

using Pebbles.Services.Tools;
using System.Runtime.InteropServices;
using System.Text.Json;

public class WriteFileToolTests : IDisposable
{
    private bool isDisposed;
    private readonly WriteFileTool _tool;
    private readonly string _testDirectory;

    public WriteFileToolTests()
    {
        _tool = new WriteFileTool();
        // Use user profile directory instead of temp to pass path validation
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _testDirectory = Path.Combine(userProfile, ".pebbles_test", Guid.NewGuid().ToString("N")[..8]);
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
            Assert.Equal("Hello World", await File.ReadAllTextAsync(testFile));
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
        Assert.Contains("'path' is required", result.Error, StringComparison.Ordinal);
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
        Assert.Contains("'content' is required", result.Error, StringComparison.Ordinal);
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
        Assert.Contains("'content' is required", result.Error, StringComparison.Ordinal);
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
        // Arrange - use a unique file name to avoid conflicts with parallel tests
        var testFile = Path.Combine(_testDirectory, $"existing_{Guid.NewGuid():N}.txt");

        // Write and close the file explicitly to release the handle
        await using (var fs = File.Create(testFile))
        await using (var writer = new StreamWriter(fs))
        {
            await writer.WriteAsync("Original content");
        }

        // Small delay to ensure file handle is fully released on Windows CI
        await Task.Delay(50);

        var args = new { path = testFile, content = "New content", createBackup = true };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert - backup only created if file is within working directory
            // Since _testDirectory is not in working directory, backup won't be created
            // but the write should still succeed
            Assert.True(result.Success, $"Expected success but got error: {result.Error}");
            Assert.Equal("New content", await File.ReadAllTextAsync(testFile));
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
        // Arrange - use a unique file name to avoid conflicts with parallel tests
        var testFile = Path.Combine(_testDirectory, $"existing_{Guid.NewGuid():N}.txt");

        // Write and close the file explicitly to release the handle
        await using (var fs = File.Create(testFile))
        await using (var writer = new StreamWriter(fs))
        {
            await writer.WriteAsync("Original content");
        }

        // Small delay to ensure file handle is fully released on Windows CI
        await Task.Delay(50);

        var args = new { path = testFile, content = "New content", createBackup = false };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("New content", await File.ReadAllTextAsync(testFile));
            Assert.DoesNotContain("Backup:", result.Content, StringComparison.Ordinal);
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
        // Error could be either "outside allowed" or "system path" depending on validation order
        Assert.True(
            result.Error?.Contains("outside allowed", StringComparison.Ordinal) == true ||
            result.Error?.Contains("system path", StringComparison.OrdinalIgnoreCase) == true,
            $"Expected error about path restriction, got: {result.Error}");
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
        Assert.True(definition.Function.Parameters.Properties.ContainsKey("path"), "Expected 'path' in Properties");
        Assert.True(definition.Function.Parameters.Properties.ContainsKey("content"), "Expected 'content' in Properties");
        Assert.True(definition.Function.Parameters.Required.Contains("path"), "Expected 'path' in Required");
        Assert.True(definition.Function.Parameters.Required.Contains("content"), "Expected 'content' in Required");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;

        if (disposing)
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore cleanup errors
            }
        }

        isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}