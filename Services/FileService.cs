namespace Pebbles.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Implementation of file operations for including files in AI context.
/// </summary>
public class FileService : IFileService
{
    private static readonly Regex FileReferenceRegex = new(
        @"@(?<path>[a-zA-Z0-9_\-./\\]+\.[a-zA-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, FileContent> _loadedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _workingDirectory;

    public FileService()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
    }

    public IReadOnlyDictionary<string, FileContent> LoadedFiles => _loadedFiles;

    public IReadOnlyList<FileItem> ListDirectory(string? directory = null, string? filter = null)
    {
        var items = new List<FileItem>();
        var targetPath = string.IsNullOrEmpty(directory)
            ? _workingDirectory
            : ResolvePath(directory);

        try
        {
            if (!Directory.Exists(targetPath))
                return items;

            // Get directories
            foreach (var dir in Directory.GetDirectories(targetPath))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!string.IsNullOrEmpty(filter) && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip hidden directories
                if (name.StartsWith('.'))
                    continue;

                var relativePath = GetRelativePath(dir);
                items.Add(new FileItem
                {
                    Name = name,
                    Path = relativePath,
                    IsDirectory = true
                });
            }

            // Get files
            foreach (var file in Directory.GetFiles(targetPath))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!string.IsNullOrEmpty(filter) && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip hidden files
                if (name.StartsWith('.'))
                    continue;

                var relativePath = GetRelativePath(file);
                items.Add(new FileItem
                {
                    Name = name,
                    Path = relativePath,
                    IsDirectory = false,
                    Extension = Path.GetExtension(name)
                });
            }

            // Sort: directories first, then files, both alphabetically
            return items
                .OrderBy(i => !i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return items;
        }
    }

    private string GetRelativePath(string absolutePath)
    {
        if (absolutePath.StartsWith(_workingDirectory))
        {
            var relative = absolutePath[_workingDirectory.Length..];
            return relative.TrimStart(Path.DirectorySeparatorChar, '/');
        }
        return absolutePath;
    }

    public ParsedInput ParseFileReferences(string input)
    {
        var matches = FileReferenceRegex.Matches(input);
        var references = new List<FileReference>();
        var cleanInput = input;

        // Process matches in reverse order to preserve indices
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var path = match.Groups["path"].Value;

            // Normalize path separators
            path = path.Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);

            references.Add(new FileReference
            {
                Original = match.Value,
                Path = path,
                StartIndex = match.Index,
                Length = match.Length
            });

            // Replace the reference with a placeholder
            cleanInput = cleanInput.Remove(match.Index, match.Length)
                                   .Insert(match.Index, $"[file: {path}]");
        }

        // Reverse to get original order
        references.Reverse();

        return new ParsedInput
        {
            Original = input,
            CleanInput = cleanInput,
            FileReferences = references
        };
    }

    public FileContent ReadFile(string path)
    {
        // Resolve to absolute path
        var absolutePath = ResolvePath(path);

        // Check if already loaded
        if (_loadedFiles.TryGetValue(absolutePath, out var cached))
        {
            return cached;
        }

        // Try to read the file
        try
        {
            if (!File.Exists(absolutePath))
            {
                return new FileContent
                {
                    Path = path,
                    AbsolutePath = absolutePath,
                    Content = string.Empty,
                    Size = 0,
                    Error = $"File not found: {path}"
                };
            }

            var fileInfo = new FileInfo(absolutePath);

            // Check file size (limit to 1MB)
            const long maxSize = 1 * 1024 * 1024;
            if (fileInfo.Length > maxSize)
            {
                return new FileContent
                {
                    Path = path,
                    AbsolutePath = absolutePath,
                    Content = string.Empty,
                    Size = fileInfo.Length,
                    Error = $"File too large ({fileInfo.Length / 1024}KB). Maximum size is 1MB."
                };
            }

            // Check if binary file
            if (IsBinaryFile(absolutePath))
            {
                return new FileContent
                {
                    Path = path,
                    AbsolutePath = absolutePath,
                    Content = string.Empty,
                    Size = fileInfo.Length,
                    Error = "Binary files are not supported. Only text files can be included."
                };
            }

            var content = File.ReadAllText(absolutePath);
            var fileContent = new FileContent
            {
                Path = path,
                AbsolutePath = absolutePath,
                Content = content,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc
            };

            // Cache the file
            _loadedFiles[absolutePath] = fileContent;

            return fileContent;
        }
        catch (UnauthorizedAccessException)
        {
            return new FileContent
            {
                Path = path,
                AbsolutePath = absolutePath,
                Content = string.Empty,
                Size = 0,
                Error = $"Access denied: {path}"
            };
        }
        catch (Exception ex)
        {
            return new FileContent
            {
                Path = path,
                AbsolutePath = absolutePath,
                Content = string.Empty,
                Size = 0,
                Error = $"Error reading file: {ex.Message}"
            };
        }
    }

    public void AddFile(string path, FileContent content)
    {
        var absolutePath = ResolvePath(path);
        _loadedFiles[absolutePath] = content with { Path = path, AbsolutePath = absolutePath };
    }

    public void ClearFiles()
    {
        _loadedFiles.Clear();
    }

    public string FormatFilesForPrompt()
    {
        if (_loadedFiles.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Loaded Files");
        sb.AppendLine("The following files have been loaded and are available for reference:");
        sb.AppendLine();

        foreach (var (path, content) in _loadedFiles)
        {
            if (!content.Success)
                continue;

            var ext = Path.GetExtension(path).TrimStart('.');
            var lang = GetLanguageFromExtension(ext);

            sb.AppendLine($"### `{path}`");
            sb.AppendLine($"```{lang}");
            sb.AppendLine(content.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(_workingDirectory, path));
    }

    private static bool IsBinaryFile(string path)
    {
        // Common text file extensions
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".json", ".xml", ".yaml", ".yml",
            ".cs", ".csproj", ".sln", ".config",
            ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs",
            ".py", ".pyw",
            ".java", ".kt", ".kts",
            ".rb", ".php",
            ".go", ".rs", ".c", ".cpp", ".h", ".hpp",
            ".sh", ".bash", ".zsh", ".ps1", ".bat", ".cmd",
            ".sql", ".html", ".htm", ".css", ".scss", ".sass", ".less",
            ".vue", ".svelte",
            ".toml", ".ini", ".env", ".gitignore", ".dockerignore",
            ".dockerfile", ".makefile", ".cmake",
            ".razor", ".aspx", ".asmx", ".ashx",
            ".resx", ".xaml"
        };

        var ext = Path.GetExtension(path);
        if (textExtensions.Contains(ext))
            return false;

        // Check for null bytes in first 8KB
        try
        {
            var buffer = new byte[8192];
            using var fs = File.OpenRead(path);
            var bytesRead = fs.Read(buffer, 0, buffer.Length);

            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static string GetLanguageFromExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            "cs" => "csharp",
            "csproj" => "xml",
            "sln" => "plaintext",
            "js" => "javascript",
            "ts" => "typescript",
            "jsx" => "jsx",
            "tsx" => "tsx",
            "py" => "python",
            "java" => "java",
            "kt" or "kts" => "kotlin",
            "rb" => "ruby",
            "php" => "php",
            "go" => "go",
            "rs" => "rust",
            "c" => "c",
            "cpp" or "hpp" or "h" => "cpp",
            "sh" or "bash" => "bash",
            "ps1" => "powershell",
            "sql" => "sql",
            "html" or "htm" => "html",
            "css" => "css",
            "scss" or "sass" => "scss",
            "json" => "json",
            "xml" => "xml",
            "yaml" or "yml" => "yaml",
            "md" => "markdown",
            "toml" => "toml",
            _ => ext
        };
    }
}