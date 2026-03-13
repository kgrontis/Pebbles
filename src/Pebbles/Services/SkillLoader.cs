namespace Pebbles.Services;

using Pebbles.Models;
using System.Text;

/// <summary>
/// Discovers and loads skills from ~/.pebbles/skills/ and .pebbles/skills/ directories.
/// Skills are markdown files with optional YAML frontmatter.
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// Loads all available skills from skill directories.
    /// </summary>
    IEnumerable<Skill> LoadSkills();

    /// <summary>
    /// Gets the paths where skills are searched.
    /// </summary>
    IReadOnlyList<string> SkillPaths { get; }
}

/// <summary>
/// Implementation of skill loading from markdown files with YAML frontmatter.
/// </summary>
public class SkillLoader : ISkillLoader
{
    private readonly List<string> _skillPaths;

    public SkillLoader()
    {
        _skillPaths =
        [
            // Global skills directory
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pebbles", "skills"),
            // Project-level skills directory
            Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "skills")
        ];
    }

    public IReadOnlyList<string> SkillPaths => _skillPaths;

    public IEnumerable<Skill> LoadSkills()
    {
        var skills = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);

        foreach (var basePath in _skillPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            // Load skills from subdirectories (e.g., my-skill/SKILL.md)
            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var skill = LoadSkillFromDirectory(dir);
                if (skill is not null && !string.IsNullOrEmpty(skill.Name))
                {
                    skills[skill.Name] = skill;
                }
            }

            // Also load standalone .md files in the root (legacy/simple skills)
            foreach (var file in Directory.GetFiles(basePath, "*.md", SearchOption.TopDirectoryOnly))
            {
                var skill = ParseSkillFile(file);
                if (skill is not null && !string.IsNullOrEmpty(skill.Name))
                {
                    skills[skill.Name] = skill;
                }
            }
        }

        return skills.Values.OrderBy(s => s.Name);
    }

    private static Skill? LoadSkillFromDirectory(string directoryPath)
    {
        var skillFile = Path.Combine(directoryPath, "SKILL.md");
        if (!File.Exists(skillFile))
            return null;

        try
        {
            var content = File.ReadAllText(skillFile);
            var (frontmatter, body) = ParseFrontmatter(content);

            // Use directory name as skill name if not specified in frontmatter
            var name = frontmatter.GetValueOrDefault("name", new DirectoryInfo(directoryPath).Name);
            var description = frontmatter.GetValueOrDefault("description", string.Empty);

            // Include reference files if they exist
            var referencesDir = Path.Combine(directoryPath, "references");
            var fullContent = new StringBuilder(body.Trim());

            if (Directory.Exists(referencesDir))
            {
                foreach (var refFile in Directory.GetFiles(referencesDir, "*.md", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var refContent = File.ReadAllText(refFile);
                        var refName = Path.GetFileNameWithoutExtension(refFile);
                        fullContent.AppendLine();
                        fullContent.AppendLine();
                        fullContent.AppendLine("## Reference: " + refName);
                        fullContent.AppendLine();
                        fullContent.Append(refContent.Trim());
                    }
                    catch (IOException)
                    {
                        // Skip files that can't be read
                    }
                }
            }

            return new Skill
            {
                Name = name,
                Description = description,
                Content = fullContent.ToString(),
                SourcePath = skillFile
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static Skill? ParseSkillFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var (frontmatter, body) = ParseFrontmatter(content);

            var name = frontmatter.GetValueOrDefault("name", Path.GetFileNameWithoutExtension(filePath));
            var description = frontmatter.GetValueOrDefault("description", string.Empty);

            return new Skill
            {
                Name = name,
                Description = description,
                Content = body.Trim(),
                SourcePath = filePath
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static (Dictionary<string, string> Frontmatter, string Body) ParseFrontmatter(string content)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Check for YAML frontmatter (between --- markers)
        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return (frontmatter, content);
        }

        var endMarker = content.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (endMarker == -1)
        {
            return (frontmatter, content);
        }

        var frontmatterText = content[3..endMarker];
        var body = content[(endMarker + 4)..].TrimStart('\r', '\n');

        // Parse simple key: value pairs
        foreach (var line in frontmatterText.Split('\n'))
        {
            var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();

                // Remove quotes if present
                if (value.StartsWith('"') && value.EndsWith('"') && value.Length > 1)
                {
                    value = value[1..^1];
                }
                else if (value.StartsWith('\'') && value.EndsWith('\'') && value.Length > 1)
                {
                    value = value[1..^1];
                }

                frontmatter[key] = value;
            }
        }

        return (frontmatter, body);
    }
}