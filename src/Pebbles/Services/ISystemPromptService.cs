namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Provides system prompts for the AI assistant.
/// </summary>
public interface ISystemPromptService
{
    /// <summary>
    /// Gets the main agent system prompt.
    /// </summary>
    /// <param name="activeSkill">Optional active skill to include in the prompt.</param>
    /// <returns>The complete system prompt with user memory appended.</returns>
    string GetAgentPrompt(Skill? activeSkill = null);

    /// <summary>
    /// Gets the compression system prompt for summarizing conversation history.
    /// </summary>
    string GetCompressionPrompt();

    /// <summary>
    /// Gets the project summary prompt for generating markdown summaries.
    /// </summary>
    string GetProjectSummaryPrompt();

    /// <summary>
    /// Gets the memory extraction prompt for extracting user preferences.
    /// </summary>
    string GetMemoryExtractionPrompt();

    /// <summary>
    /// Saves user memory to persistent storage.
    /// </summary>
    /// <param name="memory">The user memory content to save.</param>
    void SaveUserMemory(string memory);

    /// <summary>
    /// Gets the current user memory.
    /// </summary>
    /// <returns>The user memory content, or empty string if not set.</returns>
    string GetUserMemory();
}