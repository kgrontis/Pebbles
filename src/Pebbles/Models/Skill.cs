namespace Pebbles.Models;

/// <summary>
/// Represents a skill that can be invoked to provide specialized guidance to the AI.
/// Skills are markdown files with YAML frontmatter stored in ~/.pebbles/skills/ or .pebbles/skills/.
/// </summary>
public class Skill
{
    /// <summary>
    /// The unique name of the skill (from filename or frontmatter).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// A brief description of what the skill does and when to use it.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The full content of the skill (instructions for the AI).
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// The file path where the skill was loaded from.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Whether this skill is currently active.
    /// </summary>
    public bool IsActive { get; set; }
}