namespace Pebbles.Tests.Services;

using Pebbles.Services;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class FileServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _originalDirectory;
    private readonly FileService _fileService;
    private bool _disposed;

    public FileServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pebbles_file_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDirectory);
        _fileService = new FileService();
    }

    [Fact]
    public void ParseFileReferences_ReturnsEmpty_WhenNoReferences()
    {
        // Arrange
        var input = "Hello, how are you?";

        // Act
        var result = _fileService.ParseFileReferences(input);

        // Assert
        Assert.False(result.HasFiles);
        Assert.Empty(result.FileReferences);
        Assert.Equal(input, result.CleanInput);
    }

    [Fact]
    public void ParseFileReferences_DetectsSingleFile()
    {
        // Arrange
        var input = "Read @Program.cs and explain it";

        // Act
        var result = _fileService.ParseFileReferences(input);

        // Assert
        Assert.True(result.HasFiles);
        Assert.Single(result.FileReferences);
        Assert.Equal("Program.cs", result.FileReferences[0].Path);
    }

    [Fact]
    public void ParseFileReferences_DetectsMultipleFiles()
    {
        // Arrange
        var input = "Compare @Program.cs and @Startup.cs";

        // Act
        var result = _fileService.ParseFileReferences(input);

        // Assert
        Assert.True(result.HasFiles);
        Assert.Equal(2, result.FileReferences.Count);
    }

    [Fact]
    public void ParseFileReferences_DetectsPathWithSubdirectory()
    {
        // Arrange
        var input = "Read @src/Services/FileService.cs";

        // Act
        var result = _fileService.ParseFileReferences(input);

        // Assert
        Assert.Single(result.FileReferences);
        Assert.Contains("FileService.cs", result.FileReferences[0].Path, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void ParseFileReferences_ReplacesWithPlaceholder()
    {
        // Arrange
        var input = "Read @Program.cs";

        // Act
        var result = _fileService.ParseFileReferences(input);

        // Assert
        Assert.Contains("[file:", result.CleanInput, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Program.cs", result.CleanInput, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void ReadFile_ReturnsError_WhenFileNotFound()
    {
        // Arrange & Act
        var result = _fileService.ReadFile("nonexistent.txt");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadFile_ReturnsContent_WhenFileExists()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "Hello, World!");

        // Act
        var result = _fileService.ReadFile("test.txt");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Hello, World!", result.Content);
    }

    [Fact]
    public void ReadFile_ReturnsCorrectSize()
    {
        // Arrange
        var content = "Hello, World!";
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), content);

        // Act
        var result = _fileService.ReadFile("test.txt");

        // Assert
        Assert.Equal(content.Length, result.Size);
    }

    [Fact]
    public void ReadFile_CachesResult()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(filePath, "Original content");

        // Act - First read
        var first = _fileService.ReadFile("test.txt");

        // Modify file
        File.WriteAllText(filePath, "Modified content");

        // Second read should return cached
        var second = _fileService.ReadFile("test.txt");

        // Assert
        Assert.Equal("Original content", first.Content);
        Assert.Equal("Original content", second.Content); // Cached
    }

    [Fact]
    public void ReadFile_DetectsImageFile()
    {
        // Arrange - Create a minimal PNG file
        var filePath = Path.Combine(_testDirectory, "test.png");
        // Minimal valid PNG: 8-byte signature + IHDR + IDAT + IEND
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
            0x01, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82
        };
        File.WriteAllBytes(filePath, pngBytes);

        // Act
        var result = _fileService.ReadFile("test.png");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(FileContentType.Image, result.ContentType);
        Assert.Equal("image/png", result.MimeType);
    }

    [Fact]
    public void ReadFile_ReturnsDirectoryStructure_WhenPathIsDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(_testDirectory, "myfolder");
        Directory.CreateDirectory(dirPath);
        File.WriteAllText(Path.Combine(dirPath, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(dirPath, "file2.txt"), "content2");

        // Act
        var result = _fileService.ReadFile("myfolder");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(FileContentType.Directory, result.ContentType);
        Assert.Contains("myfolder", result.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void ListDirectory_ReturnsEmpty_WhenDirectoryNotFound()
    {
        // Act
        var result = _fileService.ListDirectory("nonexistent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ListDirectory_ReturnsFilesAndDirectories()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testDirectory, "subdir"));
        File.WriteAllText(Path.Combine(_testDirectory, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.cs"), "code");

        // Act
        var result = _fileService.ListDirectory();

        // Assert
        Assert.True(result.Count >= 3); // At least subdir, file1.txt, file2.cs
        Assert.Contains(result, i => i.Name == "subdir" && i.IsDirectory);
        Assert.Contains(result, i => i.Name == "file1.txt" && !i.IsDirectory);
    }

    [Fact]
    public void ListDirectory_SortsDirectoriesFirst()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testDirectory, "zdir"));
        File.WriteAllText(Path.Combine(_testDirectory, "afile.txt"), "content");

        // Act
        var result = _fileService.ListDirectory();

        // Assert
        var firstNonHidden = result[0];
        Assert.NotNull(firstNonHidden);
        Assert.True(firstNonHidden.IsDirectory);
    }

    [Fact]
    public void ListDirectory_FiltersByName()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "test1.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "other.txt"), "content");

        // Act
        var result = _fileService.ListDirectory(filter: "test");

        // Assert
        Assert.All(result, i => Assert.StartsWith("test", i.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListDirectory_SkipsHiddenFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "visible.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, ".hidden.txt"), "hidden");

        // Act
        var result = _fileService.ListDirectory();

        // Assert
        Assert.DoesNotContain(result, i => i.Name.StartsWith('.'));
    }

    [Fact]
    public void AddFile_AddsToLoadedFiles()
    {
        // Arrange
        var content = new FileContent
        {
            Path = "test.txt",
            AbsolutePath = Path.Combine(_testDirectory, "test.txt"),
            Content = "Added content"
        };

        // Act
        _fileService.AddFile("test.txt", content);

        // Assert
        Assert.Single(_fileService.LoadedFiles);
    }

    [Fact]
    public void ClearFiles_RemovesAllLoadedFiles()
    {
        // Arrange
        var content = new FileContent
        {
            Path = "test.txt",
            AbsolutePath = Path.Combine(_testDirectory, "test.txt"),
            Content = "Content"
        };
        _fileService.AddFile("test.txt", content);

        // Act
        _fileService.ClearFiles();

        // Assert
        Assert.Empty(_fileService.LoadedFiles);
    }

    [Fact]
    public void FormatFilesForPrompt_ReturnsEmpty_WhenNoFiles()
    {
        // Act
        var result = _fileService.FormatFilesForPrompt();

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatFilesForPrompt_IncludesFileContent()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "test.cs"), "public class Test { }");
        _fileService.ReadFile("test.cs");

        // Act
        var result = _fileService.FormatFilesForPrompt();

        // Assert
        Assert.Contains("test.cs", result, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("public class Test", result, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("```csharp", result, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void FormatFilesForPrompt_ReturnsEmpty_WhenAllFilesFailed()
    {
        // Arrange - ReadFile returns error but doesn't cache failed files
        _fileService.ReadFile("nonexistent.txt");

        // Act
        var result = _fileService.FormatFilesForPrompt();

        // Assert - Empty because failed files are not cached
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatFilesForPrompt_IncludesErrorMessages_WhenFileAddedManually()
    {
        // Arrange - Add a file with an error manually
        var errorContent = new FileContent
        {
            Path = "error.txt",
            AbsolutePath = Path.Combine(_testDirectory, "error.txt"),
            Content = string.Empty,
            Error = "File not found"
        };
        _fileService.AddFile("error.txt", errorContent);

        // Act
        var result = _fileService.FormatFilesForPrompt();

        // Assert
        Assert.Contains("Error:", result, StringComparison.InvariantCultureIgnoreCase);
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

        if (disposing)
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        _disposed = true;
    }
}