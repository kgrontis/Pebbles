namespace Pebbles.Services;

/// <summary>
/// Initializes the global agent directory with default prompt files.
/// Creates ~/.pebbles/agent if it doesn't exist and populates it with default content.
/// </summary>
internal static class AgentInitializer
{
    private static readonly string AgentDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".pebbles",
        "agent");

    /// <summary>
    /// Ensures the global agent directory exists with default prompt files.
    /// Creates missing files but never overwrites existing ones.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (!Directory.Exists(AgentDirectory))
        {
            Directory.CreateDirectory(AgentDirectory);
        }

        // AGENTS.md is user-visible and editable
        var agentsPath = Path.Combine(AgentDirectory, "AGENTS.md");
        if (!File.Exists(agentsPath))
        {
            ExtractEmbeddedResource("AGENTS.md", agentsPath);
        }

        // COMPRESSION.md is generated from code (internal prompt)
        var compressionPath = Path.Combine(AgentDirectory, "COMPRESSION.md");
        if (!File.Exists(compressionPath))
        {
            File.WriteAllText(compressionPath, GetCompressionPrompt());
        }

        // MEMORY_EXTRACTION.md is generated from code (internal prompt)
        var memoryPath = Path.Combine(AgentDirectory, "MEMORY_EXTRACTION.md");
        if (!File.Exists(memoryPath))
        {
            File.WriteAllText(memoryPath, GetMemoryExtractionPrompt());
        }
    }

    /// <summary>
    /// Extracts an embedded resource to the agent directory.
    /// </summary>
    private static void ExtractEmbeddedResource(string fileName, string targetPath)
    {
        var resourcePath = $"Pebbles.AgentDefaults.{fileName}";

        using var stream = typeof(AgentInitializer).Assembly.GetManifestResourceStream(resourcePath) ?? throw new InvalidOperationException($"Embedded resource not found: {resourcePath}");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        File.WriteAllText(targetPath, content);
    }

    /// <summary>
    /// Returns the compression prompt content.
    /// This is an internal prompt not meant to be user-editable.
    /// </summary>
    private static string GetCompressionPrompt() =>
        """"
        # Compression Prompt

        You are the component that summarizes internal chat history into a structured format.

        ## Your Task

        When the conversation history grows too large, you will be invoked to distill the entire history into a concise, structured XML snapshot. This snapshot is CRITICAL, as it will become the agent's *only* memory of the past. The agent will resume its work based solely on this snapshot. All crucial details, plans, errors, and user directives MUST be preserved.

        **CRITICAL: Preserve exact text verbatim.** File paths, error messages, line numbers, and code snippets must be preserved exactly as they appear. Do not paraphrase or summarize these — the agent needs them for debugging and navigation.

        First, think through the entire history in a private `<scratchpad>`. Review the user's overall goal, the agent's actions, tool outputs, file modifications, and any unresolved questions. Identify every piece of information that is essential for future actions.

        After your reasoning is complete, generate the final `<state_snapshot>` XML object. Be incredibly dense with information. Omit any irrelevant conversational filler.

        The structure MUST be as follows:

        ```xml
        <state_snapshot>
            <overall_goal>
                <!-- A single, concise sentence describing the user's high-level objective. -->
                <!-- Example: "Refactor the authentication service to use a new JWT library." -->
            </overall_goal>

            <constraints>
                <!-- User preferences, requirements, and limitations that must be respected. -->
                <!-- Example:
                 - Use Entity Framework (not Dapper) for data access
                 - No external API calls without user approval
                 - Must maintain backward compatibility with existing clients
                 - User prefers minimal comments in code
                -->
            </constraints>

            <key_knowledge>
                <!-- Crucial facts, conventions, and constraints the agent must remember. -->
                <!-- Example:
                 - Build Command: `dotnet build`
                 - Testing: Tests are run with `dotnet test`. Test files must end in `.Tests.cs`.
                 - API Endpoint: The primary API endpoint is `https://api.example.com/v2`
                -->
            </key_knowledge>

            <file_system_state>
                <!-- List files that have been created, read, modified, or deleted. -->
                <!-- Example:
                 - CWD: `/home/user/project/src`
                 - READ: `Pebbles.csproj` - Confirmed 'Spectre.Console' is a dependency.
                 - MODIFIED: `Services/ChatService.cs` - Added streaming support.
                 - CREATED: `Services/ICacheService.cs` - New caching interface.
                -->
            </file_system_state>

            <recent_actions>
                <!-- A summary of the last few significant agent actions and their outcomes. -->
                <!-- Example:
                 - Ran `grep 'old_function'` which returned 3 results in 2 files.
                 - Ran `dotnet test`, which failed due to missing mock setup.
                 - Ran `ls -F` and discovered configuration files are stored as `.json`.
                -->
            </recent_actions>

            <failed_approaches>
                <!-- What was tried and didn't work. Preserve exact error messages. -->
                <!-- This prevents the agent from repeating failed attempts. -->
                <!-- Example:
                 - Tried `Newtonsoft.Json` but caused conflicts with `System.Text.Json` in Program.cs:47
                 - `dotnet test` failed: "Mock&lt;IUserService&gt; not setup for method GetUserAsync"
                 - Adding caching to UserService caused circular dependency with OrderService
                -->
            </failed_approaches>

            <avoid>
                <!-- Patterns, files, or approaches to NOT repeat. -->
                <!-- Example:
                 - Do NOT use `dynamic` keyword - causes runtime errors in this codebase
                 - Do NOT modify `LegacyAuth.cs` - deprecated, will be removed next sprint
                 - Do NOT run `dotnet restore` - breaks the local NuGet cache
                -->
            </avoid>

            <current_plan>
                <!-- The agent's step-by-step plan. Mark completed steps. -->
                <!-- Example:
                 1. [DONE] Identify all files using the deprecated 'UserAPI'.
                 2. [IN PROGRESS] Refactor `Services/UserService.cs` to use the new 'ProfileAPI'.
                 3. [TODO] Refactor the remaining files.
                 4. [TODO] Update tests to reflect the API change.
                -->
            </current_plan>
        </state_snapshot>
        ```
        """";

    /// <summary>
    /// Returns the memory extraction prompt content.
    /// This is an internal prompt not meant to be user-editable.
    /// </summary>
    private static string GetMemoryExtractionPrompt() =>
        """"
        # Memory Extraction Prompt

        You are the component that extracts user preferences and important facts from conversations.

        ## Your Task

        Analyze the conversation and extract memories that should be persisted for future sessions. Focus on:

        1. **User preferences** — Coding style, formatting preferences, language choices
        2. **Project conventions** — Build commands, test frameworks, naming conventions
        3. **Personal context** — Timezone, preferred language, work context
        4. **Important decisions** — Why certain choices were made

        ## Rules

        - Only extract facts that would be useful in FUTURE conversations
        - Do NOT extract task-specific details (those go in compression summaries)
        - Keep memories concise and actionable
        - Format as bullet points under categories

        ## Output Format

        Generate a `<memories>` XML block:

        ```xml
        <memories>
            <preferences>
                - User prefers minimal comments in code
                - Use British English spelling
            </preferences>

            <conventions>
                - Build: `dotnet build`
                - Tests: `dotnet test` (xUnit, files end in `.Tests.cs`)
            </conventions>

            <context>
                - User works in UTC+2 timezone
                - Project uses .NET 10 with Spectre.Console
            </context>
        </memories>
        ```

        If no new memories should be extracted, output:

        ```xml
        <memories>
            <!-- No new memories to extract -->
        </memories>
        ```

        ## Important

        - Do NOT duplicate existing memories
        - Do NOT extract temporary context (current task, recent errors)
        - Focus on PERSISTENT preferences and facts
        """";

    /// <summary>
    /// Checks if the agent directory exists.
    /// </summary>
    public static bool IsInitialized => Directory.Exists(AgentDirectory);

    /// <summary>
    /// Gets the path to the agent directory.
    /// </summary>
    public static string AgentPath => AgentDirectory;
}