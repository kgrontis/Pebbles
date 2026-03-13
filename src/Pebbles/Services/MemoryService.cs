namespace Pebbles.Services;

using Pebbles.Models;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Implementation of memory management services.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly ISystemPromptService _promptService;
    private readonly IAIProvider _aiProvider;
    private readonly string _memoryPath;

    public MemoryService(
        ISystemPromptService promptService,
        IAIProvider aiProvider,
        string? baseDir = null)
    {
        _promptService = promptService;
        _aiProvider = aiProvider;
        _memoryPath = Path.Combine(baseDir ?? Directory.GetCurrentDirectory(), ".pebbles", "user_memory.md");
    }

    /// <inheritdoc />
    public string GetMemories()
    {
        return _promptService.GetUserMemory();
    }

    /// <inheritdoc />
    public bool SaveMemories(string newMemories)
    {
        try
        {
            var existing = GetMemories();
            var updated = MergeMemories(existing, newMemories);
            _promptService.SaveUserMemory(updated);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is IOException
        || ex is NotSupportedException || ex is SecurityException || ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool Remember(string memory)
    {
        try
        {
            var existing = GetMemories();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var newEntry = $"\n- {memory} (added {timestamp})";
            
            // Find where to insert (before the closing comment if exists, or at end)
            var content = existing.TrimEnd();
            if (content.Contains("<!-- Store your preferences", StringComparison.InvariantCultureIgnoreCase))
            {
                // Insert after the header comment
                var lines = content.Split('\n').ToList();
                var insertIndex = lines.FindIndex(l => l.TrimStart().StartsWith("<!--", StringComparison.InvariantCultureIgnoreCase));
                if (insertIndex >= 0)
                {
                    // Find end of comment
                    for (int i = insertIndex; i < lines.Count; i++)
                    {
                        if (lines[i].Trim().EndsWith("-->", StringComparison.InvariantCultureIgnoreCase))
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }
                    lines.Insert(insertIndex, newEntry);
                }
                else
                {
                    lines.Add(newEntry);
                }
                content = string.Join("\n", lines);
            }
            else
            {
                content += newEntry;
            }

            _promptService.SaveUserMemory(content);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is IOException
        || ex is NotSupportedException || ex is SecurityException || ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public bool ClearMemories()
    {
        try
        {
            var defaultContent = @"# User Memory

<!-- Store your preferences and context here. This content will be appended to the system prompt. -->
";
            _promptService.SaveUserMemory(defaultContent);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is IOException
        || ex is NotSupportedException || ex is SecurityException || ex is DirectoryNotFoundException || ex is FileNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ExtractMemoriesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversationText = BuildConversationText(messages);
            if (string.IsNullOrEmpty(conversationText))
                return null;

            var extractionPrompt = await LoadExtractionPrompt().ConfigureAwait(false);
            var input = $"{extractionPrompt}\n\n<conversation>\n{conversationText}\n</conversation>\n\n<existing_memories>\n{GetMemories()}\n</existing_memories>\n\nExtract new memories that should be persisted.";

            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _aiProvider.StreamResponseAsync(input, cancellationToken).ConfigureAwait(false))
            {
                responseBuilder.Append(chunk);
            }

            var response = responseBuilder.ToString();
            var extracted = ParseMemoriesFromResponse(response);

            if (string.IsNullOrEmpty(extracted) || extracted.Contains("No new memories", StringComparison.InvariantCultureIgnoreCase))
                return null;

            return extracted;
        }
        catch(Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is ArgumentOutOfRangeException || ex is PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds conversation text from messages.
    /// </summary>
    private static string BuildConversationText(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var msg in messages.TakeLast(10)) // Only last 10 messages for memory extraction
        {
            var role = msg.Role switch
            {
                ChatRole.User => "User",
                ChatRole.Assistant => "Assistant",
                _ => "System"
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{role}]: {msg.Content}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Loads the memory extraction prompt.
    /// </summary>
    private async Task<string> LoadExtractionPrompt()
    {
        var promptPath = Path.Combine(
            Path.GetDirectoryName(_memoryPath) ?? ".",
            "agent",
            "MEMORY_EXTRACTION.md");

        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath).ConfigureAwait(false);
        }

        // Fallback prompt
        return @"Extract user preferences and important facts from the conversation that should be remembered for future sessions. Focus on:
- User preferences (coding style, formatting, language)
- Project conventions (build commands, test frameworks)
- Personal context (timezone, work context)
Output in <memories> XML format.";
    }

    /// <summary>
    /// Parses extracted memories from the AI response.
    /// </summary>
    private static string ParseMemoriesFromResponse(string response)
    {
        // Extract content from <memories> tags
        var match = Regex.Match(response, @"<memories>(.*?)</memories>", RegexOptions.Singleline);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return response;
    }

    /// <summary>
    /// Merges new memories with existing ones.
    /// </summary>
    private static string MergeMemories(string existing, string newMemories)
    {
        if (string.IsNullOrWhiteSpace(existing) || existing.Contains("Store your preferences", StringComparison.InvariantCultureIgnoreCase))
        {
            return $"# User Memory\n\n{newMemories}";
        }

        // Simple merge - append new memories
        return $"{existing.TrimEnd()}\n\n{newMemories}";
    }
}