namespace Pebbles.Tests.Services.Tools;

using Pebbles.Services;
using Pebbles.Services.Tools;
using System.Text.Json;

public class ListDirectoryToolTests : IDisposable
{
    private readonly ListDirectoryTool _tool;
    private readonly IFileService _fileService;
    private readonly string _testDirectory;

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
        Assert.Contains("empty", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithFiles_ReturnsFileList()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.txt");
        var file2 = Path.Combine(_testDirectory, "file2.cs");
        File.WriteAllText(file1, "content1");
        File.WriteAllText(file2, "content2");
        var args = new { path = _testDirectory };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("file1.txt", result.Content);
            Assert.Contains("file2.cs", result.Content);
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
            Assert.Contains("SubDirectory/", result.Content);
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
        File.WriteAllText(csFile, "code");
        File.WriteAllText(txtFile, "readme");
        var args = new { path = _testDirectory, filter = "Program" };
        var arguments = JsonSerializer.Serialize(args);

        try
        {
            // Act
            var result = await _tool.ExecuteAsync(arguments);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Program.cs", result.Content);
            Assert.DoesNotContain("README.txt", result.Content);
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
        Assert.Contains("empty or does not exist", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullPath_UsesCurrentDirectory()
    {
        // Arrange
        var args = new { };
        var arguments = JsonSerializer.Serialize(args);

        // Act
        var result = await _tool.ExecuteAsync(arguments);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(Directory.GetCurrentDirectory(), result.Content);
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
        Assert.Contains("path", definition.Function.Parameters.Properties.Keys);
        Assert.Contains("filter", definition.Function.Parameters.Properties.Keys);
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