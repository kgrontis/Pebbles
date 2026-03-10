namespace Pebbles.Services;

using System.Text;
using Pebbles.Configuration;
using Pebbles.Models;

/// <summary>
/// Implementation of context compaction services.
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly IAIProvider _aiProvider;
    private readonly ISystemPromptService _promptService;
    private readonly PebblesOptions _options;

    public CompressionService(
        IAIProvider aiProvider,
        ISystemPromptService promptService,
        PebblesOptions options)
    {
        _aiProvider = aiProvider;
        _promptService = promptService;
        _options = options;
    }

    /// <inheritdoc />
    public bool ShouldCompact(int currentTokens, int contextWindow, double threshold = 0.7)
    {
        if (threshold <= 0 || threshold >= 1)
            return false;

        return currentTokens >= contextWindow * threshold;
    }

    /// <inheritdoc />
    public int EstimateTotalTokens(IEnumerable<ChatMessage> messages)
    {
        return messages.Sum(m => m.TokenCount);
    }

    /// <inheritdoc />
    public async Task<CompressionResult> CompactAsync(
        List<ChatMessage> messages,
        int keepRecentCount = 6,
        string? previousSummary = null,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count <= keepRecentCount)
        {
            return CompressionResult.NotNeeded();
        }

        try
        {
            var tokensBefore = EstimateTotalTokens(messages);
            var messagesToSummarize = messages.Take(messages.Count - keepRecentCount).ToList();
            var messagesToKeep = messages.Skip(messages.Count - keepRecentCount).ToList();

            // Build the conversation text for summarization
            var conversationText = BuildConversationText(messagesToSummarize, previousSummary);

            // Get the compression prompt
            var compressionPrompt = _promptService.GetCompressionPrompt();

            // Create the summarization request
            var summarizationInput = BuildSummarizationInput(conversationText);

            // Call the AI to generate the summary
            var summary = await GenerateSummaryAsync(summarizationInput, compressionPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(summary))
            {
                return CompressionResult.Failed("Generated summary was empty");
            }

            // Calculate new token count
            var summaryTokens = EstimateTokens(summary);
            var keptTokens = EstimateTotalTokens(messagesToKeep);
            var tokensAfter = summaryTokens + keptTokens;

            return CompressionResult.Succeeded(
                summary: summary,
                tokensBefore: tokensBefore,
                tokensAfter: tokensAfter,
                summarized: messagesToSummarize.Count,
                kept: keepRecentCount
            );
        }
        catch (Exception ex)
        {
            return CompressionResult.Failed($"Compaction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the conversation text from messages to be summarized.
    /// </summary>
    private static string BuildConversationText(List<ChatMessage> messages, string? previousSummary)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(previousSummary))
        {
            sb.AppendLine("<previous_summary>");
            sb.AppendLine(previousSummary);
            sb.AppendLine("</previous_summary>");
            sb.AppendLine();
        }

        sb.AppendLine("<conversation>");
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                ChatRole.User => "User",
                ChatRole.Assistant => "Assistant",
                ChatRole.System => "System",
                _ => "Unknown"
            };

            sb.AppendLine($"[{role}]: {msg.Content}");

            if (msg.Thinking is not null && !string.IsNullOrEmpty(msg.Thinking.Content))
            {
                sb.AppendLine($"[Thinking]: {msg.Thinking.Content}");
            }
        }
        sb.AppendLine("</conversation>");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the input for the summarization request.
    /// </summary>
    private static string BuildSummarizationInput(string conversationText)
    {
        return $"Summarize the following conversation into a structured state snapshot:\n\n{conversationText}";
    }

    /// <summary>
    /// Generates a summary using the AI provider.
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string input,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var responseBuilder = new StringBuilder();

        await foreach (var chunk in _aiProvider.StreamResponseAsync(input, cancellationToken))
        {
            responseBuilder.Append(chunk);
        }

        return responseBuilder.ToString();
    }

    /// <summary>
    /// Estimates tokens for a string using the configured multiplier.
    /// </summary>
    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)Math.Ceiling(wordCount * _options.TokenEstimationMultiplier);
    }
}