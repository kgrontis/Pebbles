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
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("empty");
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
            result.Success.Should().BeTrue();
            result.Content.Should().Contain("file1.txt");
            result.Content.Should().Contain("file2.cs");
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
            result.Success.Should().BeTrue();
            result.Content.Should().Contain("SubDirectory/");
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
            result.Success.Should().BeTrue();
            result.Content.Should().Contain("Program.cs");
            result.Content.Should().NotContain("README.txt");
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
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("empty or does not exist");
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
        result.Success.Should().BeTrue();
        result.Content.Should().Contain(Directory.GetCurrentDirectory());
    }

    [Fact]
    public void GetDefinition_ReturnsValidToolDefinition()
    {
        // Act
        var definition = _tool.GetDefinition();

        // Assert
        definition.Function.Should().NotBeNull();
        definition.Function!.Name.Should().Be("list_directory");
        definition.Function.Description.Should().NotBeNullOrEmpty();
        definition.Function.Parameters.Properties.Should().ContainKey("path");
        definition.Function.Parameters.Properties.Should().ContainKey("filter");
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