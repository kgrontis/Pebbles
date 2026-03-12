namespace Pebbles.IntegrationTests;

using Pebbles.Services;
using Pebbles.Services.Tools;
using System.Text.Json;

public class ToolExecutionIntegrationTests : IDisposable
{
    private readonly ToolRegistry _toolRegistry;
    private readonly string _testDirectory;
    private bool isDisposed;

    public ToolExecutionIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
        
        // Create real instances for integration testing
        var fileService = new FileService();
        _toolRegistry = new ToolRegistry(null);
        _toolRegistry.RegisterTool(new ReadFileTool(fileService));
        _toolRegistry.RegisterTool(new WriteFileTool());
        _toolRegistry.RegisterTool(new ListDirectoryTool(fileService));
        _toolRegistry.RegisterTool(new ShellTool());
    }

    [Fact]
    public async Task ReadFileTool_Integration_ReadsActualFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Hello Integration Test");
        var tool = _toolRegistry.GetTool("read_file");
        var args = JsonSerializer.Serialize(new { path = testFile });

        // Act
        var result = await tool!.ExecuteAsync(args);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Hello Integration Test", result.Content, StringComparison.CurrentCultureIgnoreCase);
    }

    [Fact]
    public async Task WriteFileTool_Integration_CreatesActualFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "output.txt");
        var tool = _toolRegistry.GetTool("write_file");
        var args = JsonSerializer.Serialize(new { 
            path = testFile, 
            content = "Created by integration test" 
        });

        // Act
        var result = await tool!.ExecuteAsync(args);

        // Assert
        Assert.True(result.Success);
        Assert.True(File.Exists(testFile));
        Assert.Equal("Created by integration test", await File.ReadAllTextAsync(testFile));
    }

    [Fact]
    public async Task ListDirectoryTool_Integration_ListsActualFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.txt");
        var file2 = Path.Combine(_testDirectory, "file2.cs");
        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");
        var tool = _toolRegistry.GetTool("list_directory");
        var args = JsonSerializer.Serialize(new { path = _testDirectory });

        // Act
        var result = await tool!.ExecuteAsync(args);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("file1.txt", result.Content, StringComparison.Ordinal);
        Assert.Contains("file2.cs", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShellTool_Integration_ExecutesActualCommand()
    {
        // Arrange
        var tool = _toolRegistry.GetTool("run_command");
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var command = isWindows ? "echo Hello" : "echo Hello";
        var args = JsonSerializer.Serialize(new { command });

        // Act
        var result = await tool!.ExecuteAsync(args);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Hello", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolExecution_MultipleTools_ExecuteInSequence()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "sequence.txt");
        var writeTool = _toolRegistry.GetTool("write_file");
        var readTool = _toolRegistry.GetTool("read_file");
        
        // Act - Write then Read
        var writeArgs = JsonSerializer.Serialize(new { 
            path = testFile, 
            content = "Sequential content" 
        });
        var writeResult = await writeTool!.ExecuteAsync(writeArgs);
        
        var readArgs = JsonSerializer.Serialize(new { path = testFile });
        var readResult = await readTool!.ExecuteAsync(readArgs);

        // Assert
        Assert.True(writeResult.Success);
        Assert.True(readResult.Success);
        Assert.Contains("Sequential content", readResult.Content, StringComparison.Ordinal);
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
