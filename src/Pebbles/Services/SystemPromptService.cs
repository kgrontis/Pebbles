namespace Pebbles.Services;

/// <summary>
/// Manages system prompts for the AI assistant.
/// Supports loading from files, environment variable overrides, and user memory.
/// </summary>
public class SystemPromptService : ISystemPromptService
{
    private const string DefaultPromptDir = ".pebbles/agent";
    private const string DefaultUserMemoryPath = ".pebbles/user_memory.md";

    private readonly string _baseDir;
    private readonly string _promptDir;
    private readonly string _userMemoryPath;

    /// <summary>
    /// Initializes a new instance of the SystemPromptService.
    /// </summary>
    /// <param name="baseDir">The base directory for resolving prompt paths. Defaults to current directory.</param>
    public SystemPromptService(string? baseDir = null)
    {
        // Ensure global agent directory exists with default prompts
        AgentInitializer.EnsureInitialized();

        _baseDir = baseDir ?? Directory.GetCurrentDirectory();
        _promptDir = ResolvePromptDirectory();
        _userMemoryPath = ResolveUserMemoryPath();
    }

    /// <inheritdoc />
    public string GetAgentPrompt()
    {
        var promptPath = GetPromptPath("AGENTS.md");
        var basePrompt = LoadPrompt(promptPath);
        var userMemory = GetUserMemory();

        return AppendUserMemory(basePrompt, userMemory);
    }

    /// <inheritdoc />
    public string GetCompressionPrompt()
    {
        var promptPath = GetPromptPath("COMPRESSION.md");
        return LoadPrompt(promptPath);
    }

    /// <inheritdoc />
    public string GetProjectSummaryPrompt()
    {
        var promptPath = GetPromptPath("PROJECT_SUMMARY.md");
        return LoadPrompt(promptPath);
    }

    /// <inheritdoc />
    public string GetMemoryExtractionPrompt()
    {
        var promptPath = GetPromptPath("MEMORY_EXTRACTION.md");
        return LoadPrompt(promptPath);
    }

    /// <inheritdoc />
    public void SaveUserMemory(string memory)
    {
        var directory = Path.GetDirectoryName(_userMemoryPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_userMemoryPath, memory);
    }

    /// <inheritdoc />
    public string GetUserMemory()
    {
        if (!File.Exists(_userMemoryPath))
        {
            return string.Empty;
        }

        return File.ReadAllText(_userMemoryPath).Trim();
    }

    /// <summary>
    /// Resolves the prompt directory from environment variable or uses default.
    /// </summary>
    private string ResolvePromptDirectory()
    {
        var envPath = Environment.GetEnvironmentVariable("PEBBLES_SYSTEM_MD") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pebbles", "agent");

        // If env var is set to a file path, use its directory
        if (!string.IsNullOrWhiteSpace(envPath) && !IsSwitchValue(envPath))
        {
            var fullPath = Path.IsPathRooted(envPath)
                ? envPath
                : Path.Combine(_baseDir, envPath);

            // If it's a file path, return the directory
            if (Path.HasExtension(fullPath))
            {
                return Path.GetDirectoryName(fullPath) ?? _baseDir;
            }

            return fullPath;
        }

        // Default prompt directory
        return Path.Combine(_baseDir, DefaultPromptDir);
    }

    /// <summary>
    /// Resolves the user memory path.
    /// </summary>
    private string ResolveUserMemoryPath()
    {
        return Path.Combine(_baseDir, DefaultUserMemoryPath);
    }

    /// <summary>
    /// Gets the full path to a prompt file.
    /// </summary>
    private string GetPromptPath(string fileName)
    {
        // Check environment variable override first
        var envPath = Environment.GetEnvironmentVariable("PEBBLES_SYSTEM_MD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pebbles", "agent");

        if (!string.IsNullOrWhiteSpace(envPath) && !IsSwitchValue(envPath))
        {
            var fullPath = Path.IsPathRooted(envPath)
                ? envPath
                : Path.Combine(_baseDir, envPath);

            // If env var points to a specific file, use it for AGENTS.md only
            if (Path.HasExtension(fullPath) && fileName == "AGENTS.md")
            {
                return fullPath;
            }
        }

        return Path.Combine(_promptDir, fileName);
    }

    /// <summary>
    /// Loads a prompt from a file.
    /// </summary>
    private static string LoadPrompt(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"System prompt file not found: {path}");
        }

        return File.ReadAllText(path).Trim();
    }

    /// <summary>
    /// Appends user memory to the base prompt with a separator.
    /// </summary>
    private static string AppendUserMemory(string basePrompt, string userMemory)
    {
        if (string.IsNullOrWhiteSpace(userMemory))
        {
            return basePrompt;
        }

        return $"{basePrompt}\n\n---\n\n{userMemory}";
    }

    /// <summary>
    /// Checks if the environment variable value is a switch (true/false/0/1).
    /// </summary>
    private static bool IsSwitchValue(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        return upper is "TRUE" or "FALSE" or "0" or "1";
    }
}