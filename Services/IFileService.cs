namespace Pebbles.Services;

/// <summary>
/// Manages file operations for including files in AI context.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Parses @file syntax from input and returns resolved file references.
    /// </summary>
    /// <param name="input">User input containing @file references</param>
    /// <returns>Parsed input with files extracted</returns>
    ParsedInput ParseFileReferences(string input);

    /// <summary>
    /// Reads a file and returns its content.
    /// </summary>
    /// <param name="path">File path (relative or absolute)</param>
    /// <returns>File content or error message</returns>
    FileContent ReadFile(string path);

    /// <summary>
    /// Lists files and folders in a directory.
    /// </summary>
    /// <param name="directory">Directory path (relative or absolute, null for current)</param>
    /// <param name="filter">Optional filter prefix</param>
    /// <returns>List of file system items</returns>
    IReadOnlyList<FileItem> ListDirectory(string? directory = null, string? filter = null);

    /// <summary>
    /// Gets all currently loaded files.
    /// </summary>
    IReadOnlyDictionary<string, FileContent> LoadedFiles { get; }

    /// <summary>
    /// Clears all loaded files.
    /// </summary>
    void ClearFiles();

    /// <summary>
    /// Adds a file to the loaded files cache.
    /// </summary>
    void AddFile(string path, FileContent content);

    /// <summary>
    /// Formats loaded files for inclusion in AI prompt.
    /// </summary>
    string FormatFilesForPrompt();
}

/// <summary>
/// Represents a file or folder item for autocomplete.
/// </summary>
public record FileItem
{
    /// <summary>
    /// Item name (filename or folder name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full relative path from working directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether this is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File extension (empty for directories).
    /// </summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// File size in bytes (0 for directories).
    /// </summary>
    public long Size { get; init; }
}

/// <summary>
/// Result of parsing file references from input.
/// </summary>
public record ParsedInput
{
    /// <summary>
    /// Original user input.
    /// </summary>
    public required string Original { get; init; }

    /// <summary>
    /// Input with @file references replaced with placeholders.
    /// </summary>
    public required string CleanInput { get; init; }

    /// <summary>
    /// File paths referenced in the input.
    /// </summary>
    public required IReadOnlyList<FileReference> FileReferences { get; init; }

    /// <summary>
    /// Whether any file references were found.
    /// </summary>
    public bool HasFiles => FileReferences.Count > 0;
}

/// <summary>
/// A file reference parsed from @file syntax.
/// </summary>
public record FileReference
{
    /// <summary>
    /// Original reference string (e.g., "@Program.cs").
    /// </summary>
    public required string Original { get; init; }

    /// <summary>
    /// Resolved file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Position in the original input.
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Length of the reference in the original input.
    /// </summary>
    public int Length { get; init; }
}

/// <summary>
/// Content of a loaded file.
/// </summary>
public record FileContent
{
    /// <summary>
    /// File path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Absolute file path.
    /// </summary>
    public required string AbsolutePath { get; init; }

    /// <summary>
    /// File content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// When the file was last modified.
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    /// Error message if file could not be read.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether the file was read successfully.
    /// </summary>
    public bool Success => Error is null;
}