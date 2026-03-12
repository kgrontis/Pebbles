namespace Pebbles.Tests.Services.Tools;

using Pebbles.Services;
using Pebbles.Services.Tools;
using System.Text.Json;

public class ListDirectoryToolTests : IDisposable
{
    private readonly ListDirectoryTool _tool;
    private readonly IFileService _fileService;
    private readonly string _testDirectory;
    private bool isDisposed;

    public ListDirectoryToolTests()
    {
        _fileService = new FileService();
        _tool = new ListDirectoryTool(_fileService);
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDirectory_ReturnsEmptyMessage()
    {
        // Arrange
        var args = new { path = _testDirectory };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("empty", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithFiles_ReturnsFileList()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.txt");
        var file2 = Path.Combine(_testDirectory, "file2.cs");
        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");
        var args = new { path = _testDirectory };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("file1.txt", result.Content, StringComparison.Ordinal);
            Assert.Contains("file2.cs", result.Content, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithDirectories_ShowsDirectoryIndicator()
    {
        // Arrange
        var subDir = Path.Combine(_testDirectory, "SubDirectory");
        Directory.CreateDirectory(subDir);
        var args = new { path = _testDirectory };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("SubDirectory/", result.Content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(subDir))
                Directory.Delete(subDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithFilter_FiltersByName()
    {
        // Arrange
        var csFile = Path.Combine(_testDirectory, "Program.cs");
        var txtFile = Path.Combine(_testDirectory, "README.txt");
        await File.WriteAllTextAsync(csFile, "code");
        await File.WriteAllTextAsync(txtFile, "readme");
        var args = new { path = _testDirectory, filter = "Program" };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Program.cs", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("README.txt", result.Content, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(csFile)) File.Delete(csFile);
            if (File.Exists(txtFile)) File.Delete(txtFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentPath_ReturnsError()
    {
        // Arrange
        var args = new { path = @"C:\NonExistentPath12345" };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("empty or does not exist", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_UsesWorkingDirectory()
    {
        // Arrange - pass empty args to use default path
        var args = new { };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert - should succeed (directory likely has files from project)
        // If empty, it returns the empty message; if not empty, it lists contents
        Assert.True(result.Success);
        // Result should contain either the directory path or the empty message
        Assert.True(
            result.Content.Contains("Directory is empty", StringComparison.Ordinal) ||
            result.Content.Contains("📂", StringComparison.Ordinal),
            $"Expected directory listing or empty message, got: {result.Content}");
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = _tool.GetDefinition();

        // Assert
        Assert.NotNull(definition.Function);
        Assert.Equal("list_directory", definition.Function.Name);
        Assert.NotEmpty(definition.Function.Description);
        Assert.True(definition.Function.Parameters.Properties.ContainsKey("path"), "Expected 'path' in Properties");
        Assert.True(definition.Function.Parameters.Properties.ContainsKey("filter"), "Expected 'filter' in Properties");
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;
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
        isDisposed = true;
    }
}