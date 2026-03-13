namespace Pebbles.Services.Commands;

using Pebbles.Models;
using Spectre.Console;

/// <summary>
/// Handles skill-related commands: /skill, /skill &lt;name&gt;, /skill off.
/// </summary>
public sealed class SkillCommands(ISkillLoader skillLoader)
{
    private readonly List<Skill> _loadedSkills = [];

    /// <summary>
    /// Gets the currently loaded skills.
    /// </summary>
    public IReadOnlyList<Skill> LoadedSkills => _loadedSkills;

    /// <summary>
    /// Gets the currently active skill, if any.
    /// </summary>
    public Skill? ActiveSkill { get; private set; }

    /// <summary>
    /// Loads skills from disk. Should be called on startup.
    /// </summary>
    public void LoadSkills()
    {
        _loadedSkills.Clear();
        _loadedSkills.AddRange(skillLoader.LoadSkills());
    }

    public CommandResult HandleSkill(string[] args)
    {
        LoadSkills();

        if (args.Length == 0)
        {
            return ListSkills();
        }

        var subCommand = args[0].ToUpperInvariant();

        if (subCommand == "OFF" || subCommand == "CLEAR")
        {
            return DeactivateSkill();
        }

        return ActivateSkill(args[0]);
    }

    private CommandResult ListSkills()
    {
        if (_loadedSkills.Count == 0)
        {
            return CommandResult.OkWithMarkup("""
                [dim]No skills found.[/]

                Skills are markdown files in:
                [dim]~/.pebbles/skills/[/] (global)
                [dim].pebbles/skills/[/] (project)

                Create a skill file with YAML frontmatter:
                [dim]---[/]
                [dim]name: my-skill[/]
                [dim]description: What this skill does[/]
                [dim]---[/]
                [dim]# Instructions for the AI...[/]
                """);
        }

        var lines = new List<string>
        {
            "",
            "[bold]Available Skills[/]",
            ""
        };

        foreach (var skill in _loadedSkills)
        {
            var marker = skill == ActiveSkill ? "[green]●[/]" : " ";
            var desc = string.IsNullOrEmpty(skill.Description) ? "" : $"[dim grey]{Markup.Escape(skill.Description)}[/]";
            lines.Add($"  {marker} [bold]{Markup.Escape(skill.Name)}[/] {desc}");
        }

        lines.Add("");
        if (ActiveSkill is not null)
        {
            lines.Add($"[dim]Active: [bold]{Markup.Escape(ActiveSkill.Name)}[/]. Use /skill off to deactivate.[/]");
        }
        else
        {
            lines.Add("[dim]Use /skill <name> to activate a skill.[/]");
        }

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    private CommandResult ActivateSkill(string name)
    {
        var skill = _loadedSkills.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
        {
            return CommandResult.Fail($"Skill '{name}' not found. Use /skill to list available skills.");
        }

        // Deactivate previous skill
        if (ActiveSkill is not null)
        {
            ActiveSkill.IsActive = false;
        }

        skill.IsActive = true;
        ActiveSkill = skill;

        return CommandResult.OkWithMarkup($$"""
            [bold green]✓[/] Skill activated: [bold]{{Markup.Escape(skill.Name)}}[/]

            [dim]{{Markup.Escape(skill.Description)}}[/]
            """);
    }

    private CommandResult DeactivateSkill()
    {
        if (ActiveSkill is null)
        {
            return CommandResult.OkWithMarkup("[dim]No skill is currently active.[/]");
        }

        var previousName = ActiveSkill.Name;
        ActiveSkill.IsActive = false;
        ActiveSkill = null;

        return CommandResult.OkWithMarkup($"[dim]Skill '{Markup.Escape(previousName)}' deactivated.[/]");
    }
}