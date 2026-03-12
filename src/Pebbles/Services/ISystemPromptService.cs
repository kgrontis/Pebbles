namespace Pebbles.Services;

/// <summary>
/// Provides system prompts for the AI assistant.
/// </summary>
internal interface ISystemPromptService
{
    /// <summary>
    /// Gets the main agent system prompt.
    /// </summary>
    /// <returns>The complete system prompt with user memory appended.</returns>
    string GetAgentPrompt();

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