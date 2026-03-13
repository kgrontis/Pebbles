namespace Pebbles.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.UI;
using Spectre.Console;

/// <summary>
/// Main chat application service - orchestrates the conversation loop.
/// </summary>
public sealed class ChatService(
    IAIProvider aiProvider,
    ICommandHandler commandHandler,
    IChatRenderer renderer,
    IInputHandler inputHandler,
    IFileService fileService,
    IToolExecutionService toolExecutionService,
    IContextManagementService contextManagementService,
    ISessionStore sessionStore,
    PebblesOptions options) : IChatService
{
    public async Task RunAsync()
    {
        // Try to load last active session
        var lastSessionId = await sessionStore.GetLastActiveSessionIdAsync().ConfigureAwait(false);
        ChatSession? session = null;
        
        if (!string.IsNullOrEmpty(lastSessionId))
        {
            session = await sessionStore.LoadSessionAsync(lastSessionId).ConfigureAwait(false);
            if (session is not null)
            {
                AnsiConsole.MarkupLine($"[dim]Loaded previous session: {lastSessionId}[/]");
            }
        }
        
        session ??= ChatSession.Create(options.DefaultModel);
        renderer.RenderWelcome(session);

        while (true)
        {
            var input = inputHandler.ReadInput(session);

            if (input is null)
                break;

            input = input.Trim();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (commandHandler.IsCommand(input))
            {
                var result = await commandHandler.ExecuteAsync(input, session).ConfigureAwait(false);
                renderer.RenderCommandResult(result, session);

                if (result.ShouldExit)
                    break;

                continue;
            }

            var parsed = fileService.ParseFileReferences(input);
            if (parsed.HasFiles)
            {
                await LoadFilesAsync(parsed).ConfigureAwait(false);
                input = parsed.CleanInput;
            }

            var inputTokens = EstimateTokens(input);
            var userMsg = ChatMessage.User(parsed.Original, inputTokens);
            session.Messages.Add(userMsg);

            aiProvider.AddToHistory(userMsg);
            renderer.RenderUserMessage(parsed.Original);

            var assistantMsg = await toolExecutionService.ExecuteToolLoopAsync(input).ConfigureAwait(false);

            session.Messages.Add(assistantMsg);
            aiProvider.AddToHistory(assistantMsg);

            renderer.RenderAssistantMessage(assistantMsg.Content, assistantMsg.Thinking);

            session.TotalInputTokens += inputTokens;
            session.TotalOutputTokens += assistantMsg.TokenCount;
            renderer.RenderTokenInfo(inputTokens, assistantMsg.TokenCount, session);

            await contextManagementService.CheckAutoCompressionAsync(session).ConfigureAwait(false);
            await contextManagementService.CheckMemoryExtractionAsync(session).ConfigureAwait(false);
            
            // Auto-save session after each message
            await sessionStore.SaveSessionAsync(session).ConfigureAwait(false);
        }
        
        // Final save on exit
        await sessionStore.SaveSessionAsync(session).ConfigureAwait(false);
        await sessionStore.SetLastActiveSessionIdAsync(session.Id).ConfigureAwait(false);
    }

    private async Task LoadFilesAsync(ParsedInput parsed)
    {
        AnsiConsole.MarkupLine("[dim]Loading files...[/]");
        var loaded = 0;
        var failed = 0;

        foreach (var fileRef in parsed.FileReferences)
        {
            var content = fileService.ReadFile(fileRef.Path);

            if (content.Success)
            {
                loaded++;
                AnsiConsole.MarkupLine($"[green]✓[/] [dim]{fileRef.Path}[/] ({FormatSize(content.Size)})");
            }
            else
            {
                failed++;
                AnsiConsole.MarkupLine($"[red]✗[/] [dim]{fileRef.Path}[/]: {content.Error}");
            }
        }

        if (loaded > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Loaded {loaded} file(s) into context[/]");
        }

        if (failed > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: {failed} file(s) could not be loaded[/]");
        }

        AnsiConsole.WriteLine();
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes / (1024 * 1024):F1} MB"
        };

    private int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * options.TokenEstimationMultiplier);
}