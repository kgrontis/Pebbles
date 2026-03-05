namespace Pebbles.Services;

/// <summary>
/// Manages project context and guidelines for the AI.
/// </summary>
public class ContextManager
{
    private readonly string _globalContextPath;
    private readonly string _projectContextPath;
    private string? _globalContext;
    private string? _projectContext;

    public ContextManager()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalContextPath = Path.Combine(userProfile, ".pebbles", "agent", "AGENTS.md");
        _projectContextPath = Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "agent", "AGENTS.md");
    }

    /// <summary>
    /// Gets the global context guidelines.
    /// </summary>
    public string? GetGlobalContext()
    {
        if (_globalContext is not null)
            return _globalContext;

        if (File.Exists(_globalContextPath))
        {
            _globalContext = File.ReadAllText(_globalContextPath);
            return _globalContext;
        }

        return null;
    }

    /// <summary>
    /// Gets the project-specific context guidelines.
    /// </summary>
    public string? GetProjectContext()
    {
        if (_projectContext is not null)
            return _projectContext;

        if (File.Exists(_projectContextPath))
        {
            _projectContext = File.ReadAllText(_projectContextPath);
            return _projectContext;
        }

        return null;
    }

    /// <summary>
    /// Gets combined context for AI prompts.
    /// </summary>
    public string GetContextForPrompt()
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

    /// <summary>
    /// Checks if context files exist.
    /// </summary>
    public (bool Global, bool Project) CheckContextFiles()
    {
        return (File.Exists(_globalContextPath), File.Exists(_projectContextPath));
    }
}