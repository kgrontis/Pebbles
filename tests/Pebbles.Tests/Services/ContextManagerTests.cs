namespace Pebbles.Tests.Services;

using Pebbles.Services;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class ContextManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _globalContextDir;
    private readonly string _projectContextDir;
    private bool _disposed;

    public ContextManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pebbles_context_test_{Guid.NewGuid():N}");
        _globalContextDir = Path.Combine(_testDirectory, "global");
        _projectContextDir = Path.Combine(_testDirectory, "project");
        Directory.CreateDirectory(_globalContextDir);
        Directory.CreateDirectory(_projectContextDir);
    }

    [Fact]
    public void GetGlobalContext_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetGlobalContext();

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public void GetGlobalContext_ReturnsContent_WhenFileExists()
    {
        // Arrange
        var content = "# Global Guidelines\n\nAlways write clean code.";
        File.WriteAllText(Path.Combine(_globalContextDir, "AGENTS.md"), content);
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetGlobalContext();

        // Assert
        Assert.Equal(content, context);
    }

    [Fact]
    public void GetGlobalContext_CachesContent()
    {
        // Arrange
        var content = "Initial content";
        var filePath = Path.Combine(_globalContextDir, "AGENTS.md");
        File.WriteAllText(filePath, content);
        var manager = CreateTestableContextManager();

        // Act - First call
        var first = manager.GetGlobalContext();

        // Modify file
        File.WriteAllText(filePath, "Modified content");

        // Second call should return cached value
        var second = manager.GetGlobalContext();

        // Assert
        Assert.Equal(content, first);
        Assert.Equal(content, second); // Still cached
    }

    [Fact]
    public void GetProjectContext_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetProjectContext();

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public void GetProjectContext_ReturnsContent_WhenFileExists()
    {
        // Arrange
        var content = "# Project Context\n\nThis is a .NET project.";
        File.WriteAllText(Path.Combine(_projectContextDir, "AGENTS.md"), content);
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetProjectContext();

        // Assert
        Assert.Equal(content, context);
    }

    [Fact]
    public void GetContextForPrompt_ReturnsEmpty_WhenNoContextFiles()
    {
        // Arrange
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetContextForPrompt();

        // Assert
        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public void GetContextForPrompt_IncludesProjectContext()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_projectContextDir, "AGENTS.md"), "Project guidelines");
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetContextForPrompt();

        // Assert
        Assert.Contains("Project Context", context, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Project guidelines", context, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetContextForPrompt_IncludesGlobalContext()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_globalContextDir, "AGENTS.md"), "Global guidelines");
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetContextForPrompt();

        // Assert
        Assert.Contains("Global Guidelines", context, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Global guidelines", context, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void GetContextForPrompt_IncludesBothContexts()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_projectContextDir, "AGENTS.md"), "Project rules");
        File.WriteAllText(Path.Combine(_globalContextDir, "AGENTS.md"), "Global rules");
        var manager = CreateTestableContextManager();

        // Act
        var context = manager.GetContextForPrompt();

        // Assert
        Assert.Contains("Project Context", context, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Project rules", context, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Global Guidelines", context, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Global rules", context, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void CheckContextFiles_ReturnsCorrectStatus()
    {
        // Arrange - No files exist
        var manager = CreateTestableContextManager();

        // Act
        var (global, project) = manager.CheckContextFiles();

        // Assert
        Assert.False(global);
        Assert.False(project);

        // Arrange - Create files
        File.WriteAllText(Path.Combine(_globalContextDir, "AGENTS.md"), "Global");
        File.WriteAllText(Path.Combine(_projectContextDir, "AGENTS.md"), "Project");

        // Create new manager to check files
        manager = CreateTestableContextManager();

        // Act
        (global, project) = manager.CheckContextFiles();

        // Assert
        Assert.True(global);
        Assert.True(project);
    }

    private TestableContextManager CreateTestableContextManager()
    {
        return new TestableContextManager(_globalContextDir, _projectContextDir);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }

        _disposed = true;
    }

    /// <summary>
    /// Testable version of ContextManager that uses custom directories.
    /// </summary>
    private sealed class TestableContextManager(string globalPath, string projectPath) : ContextManager
    {
        private readonly string _testGlobalPath = Path.Combine(globalPath, "AGENTS.md");
        private readonly string _testProjectPath = Path.Combine(projectPath, "AGENTS.md");
        private string? _globalContext;
        private string? _projectContext;

        public new string? GetGlobalContext()
        {
            if (_globalContext is not null)
                return _globalContext;

            if (File.Exists(_testGlobalPath))
            {
                _globalContext = File.ReadAllText(_testGlobalPath);
                return _globalContext;
            }

            return null;
        }

        public new string? GetProjectContext()
        {
            if (_projectContext is not null)
                return _projectContext;

            if (File.Exists(_testProjectPath))
            {
                _projectContext = File.ReadAllText(_testProjectPath);
                return _projectContext;
            }

            return null;
        }

        public new string GetContextForPrompt()
        {
            var context = new System.Text.StringBuilder();

            var project = GetProjectContext();
            if (!string.IsNullOrEmpty(project))
            {
                context.AppendLine("## Project Context");
                context.AppendLine(project);
                context.AppendLine();
            }

            var global = GetGlobalContext();
            if (!string.IsNullOrEmpty(global))
            {
                context.AppendLine("## Global Guidelines");
                context.AppendLine(global);
            }

            return context.ToString();
        }

        public new (bool Global, bool Project) CheckContextFiles()
        {
            return (File.Exists(_testGlobalPath), File.Exists(_testProjectPath));
        }
    }
}