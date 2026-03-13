namespace Pebbles.Services.Commands;

using Pebbles.Configuration;
using Pebbles.Models;

/// <summary>
/// Handles compression-related commands: /compress, /autocompress.
/// </summary>
public sealed class CompressionCommands(ICompressionService? compressionService, PebblesOptions options)
{
    public async Task<CommandResult> HandleCompress(ChatSession session)
    {
        if (compressionService is null)
        {
            return CommandResult.Fail("Compression service not available.");
        }

        if (session.Messages.Count <= options.KeepRecentMessages)
        {
            return CommandResult.Ok("Not enough messages to compress. Keep chatting!");
        }

        if (session.IsCompressing)
        {
            return CommandResult.Fail("Compression already in progress.");
        }

        session.IsCompressing = true;

        try
        {
            Spectre.Console.AnsiConsole.MarkupLine("[dim]Compressing conversation history...[/]");

            var result = await compressionService.CompactAsync(
                session.Messages,
                options.KeepRecentMessages,
                session.CompressionStats.LastSummary).ConfigureAwait(false);

            if (!result.Success)
            {
                return CommandResult.Fail(result.Error ?? "Compression failed.");
            }

            if (result.MessagesSummarized == 0)
            {
                return CommandResult.Ok("No compression needed.");
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

            var lines = new List<string>
            {
                "",
                "[bold green]✓[/] Context compressed",
                "",
                $"  Before:     {result.TokensBefore:N0} tokens ({result.MessagesSummarized + result.MessagesKept} messages)",
                $"  After:      {result.TokensAfter:N0} tokens ({result.MessagesKept + 1} messages)",
                $"  Saved:      {result.TokensBefore - result.TokensAfter:N0} tokens",
                "",
                $"  Compressions this session: {session.CompressionStats.Count}",
                ""
            };

            return CommandResult.OkWithMarkup(string.Join("\n", lines));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is OverflowException)
        {
            return CommandResult.Fail($"Compression failed: {ex.Message}");
        }
        finally
        {
            session.IsCompressing = false;
        }
    }

    public static CommandResult HandleAutoCompress(ChatSession session)
    {
        session.AutoCompressionEnabled = !session.AutoCompressionEnabled;
        return CommandResult.Ok(
            $"Auto-compression: {(session.AutoCompressionEnabled ? "ON" : "OFF")}");
    }
}