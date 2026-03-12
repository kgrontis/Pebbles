namespace Pebbles.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Spectre.Console;
using System.Security;

/// <summary>
/// Service for managing context compression and memory extraction.
/// </summary>
internal sealed class ContextManagementService(
    ICompressionService compressionService,
    IMemoryService memoryService,
    PebblesOptions options) : IContextManagementService
{

    /// <inheritdoc />
    public async Task CheckAutoCompressionAsync(ChatSession session)
    {
        if (!options.AutoCompressionEnabled || !session.AutoCompressionEnabled)
            return;

        if (session.IsCompressing)
            return;

        var currentTokens = session.TotalInputTokens + session.TotalOutputTokens;
        var contextWindow = options.GetContextWindowTokens(session.Model);

        if (!compressionService.ShouldCompact(currentTokens, contextWindow, options.CompressionThreshold))
            return;

        if (session.Messages.Count <= options.KeepRecentMessages)
            return;

        session.IsCompressing = true;

        try
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[dim yellow]⚠ Context threshold reached. Auto-compressing...[/]");

            var result = await compressionService.CompactAsync(
                session.Messages,
                options.KeepRecentMessages,
                session.CompressionStats.LastSummary).ConfigureAwait(false);

            if (!result.Success || result.MessagesSummarized == 0)
            {
                AnsiConsole.MarkupLine($"[dim red]Auto-compression skipped: {result.Error ?? "No messages to summarize"}[/]");
                return;
            }

            var summaryMessage = ChatMessage.User(
                $"[Previous conversation summary]\n{result.Summary}",
                result.TokensAfter);

            var keptMessages = session.Messages
                .Skip(session.Messages.Count - options.KeepRecentMessages)
                .ToList();

            session.Messages.Clear();
            session.Messages.Add(summaryMessage);
            foreach (var msg in keptMessages)
            {
                session.Messages.Add(msg);
            }

            session.CompressionStats.Count++;
            session.CompressionStats.TotalTokensSaved += result.TokensBefore - result.TokensAfter;
            session.CompressionStats.LastCompressionTime = DateTime.Now;
            session.CompressionStats.LastSummary = result.Summary;

            AnsiConsole.MarkupLine($"[dim green]✓ Auto-compressed: {result.TokensBefore:N0} → {result.TokensAfter:N0} tokens[/]");
            AnsiConsole.MarkupLine("");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            AnsiConsole.MarkupLine($"[dim red]Auto-compression failed: {ex.Message}[/]");
        }
        finally
        {
            session.IsCompressing = false;
        }
    }

    /// <inheritdoc />
    public async Task CheckMemoryExtractionAsync(ChatSession session)
    {
        const int extractionInterval = 10;

        if (session.Messages.Count < extractionInterval)
            return;

        if (session.Messages.Count % extractionInterval != 0)
            return;

        try
        {
            var extracted = await memoryService.ExtractMemoriesAsync(session.Messages).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(extracted))
            {
                memoryService.SaveMemories(extracted);
                AnsiConsole.MarkupLine("[dim]💡 Extracted new memories from conversation[/]");
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            // Silently fail - memory extraction is not critical
        }
    }
}