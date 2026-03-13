namespace Pebbles.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.UI;
using Spectre.Console;
using System;

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
    public async Task RunAsync(SessionResumeOptions? resumeOptions = null)
    {
        resumeOptions ??= new SessionResumeOptions();

        var session = await ResolveSessionAsync(resumeOptions).ConfigureAwait(false);
        renderer.RenderWelcome(session);

        while (true)
        {
            try
            {
                await ProcessUserInputAsync(session).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // General exception handler is intentional to prevent app crashes
            catch (Exception ex)
#pragma warning restore CA1031
            {
                HandleGeneralException(ex, session);
            }
        }
    }

    private async Task<ChatSession> ResolveSessionAsync(SessionResumeOptions resumeOptions)
    {
        // If a specific session ID was provided, try to load it
        if (!string.IsNullOrEmpty(resumeOptions.SessionId))
        {
            var session = await sessionStore.LoadSessionAsync(resumeOptions.SessionId).ConfigureAwait(false);
            if (session is not null)
            {
                AnsiConsole.MarkupLine($"[dim]Loaded session: {resumeOptions.SessionId}[/]");
                return session;
            }

            AnsiConsole.MarkupLine($"[yellow]Session '{resumeOptions.SessionId}' not found. Starting new session.[/]");
            return ChatSession.Create(options.DefaultModel);
        }

        // Handle different resume modes
        return resumeOptions.Mode switch
        {
            SessionResumeMode.New => ChatSession.Create(options.DefaultModel),
            SessionResumeMode.Continue => await LoadLastSessionOrCreateAsync().ConfigureAwait(false),
            SessionResumeMode.Select => await SelectSessionOrCreateAsync().ConfigureAwait(false),
            _ => await LoadLastSessionOrCreateAsync().ConfigureAwait(false)
        };
    }

    private async Task<ChatSession> LoadLastSessionOrCreateAsync()
    {
        var lastSessionId = await sessionStore.GetLastActiveSessionIdAsync().ConfigureAwait(false);

        if (!string.IsNullOrEmpty(lastSessionId))
        {
            var session = await sessionStore.LoadSessionAsync(lastSessionId).ConfigureAwait(false);
            if (session is not null)
            {
                AnsiConsole.MarkupLine($"[dim]Continuing session: {lastSessionId}[/]");
                return session;
            }
        }

        return ChatSession.Create(options.DefaultModel);
    }

    private async Task<ChatSession> SelectSessionOrCreateAsync()
    {
        var summaries = await sessionStore.ListSessionSummariesAsync().ConfigureAwait(false);
        var summaryList = summaries.ToList();

        if (summaryList.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No saved sessions found. Starting new session.[/]");
            return ChatSession.Create(options.DefaultModel);
        }

        // Build selection items
        var lastActiveId = await sessionStore.GetLastActiveSessionIdAsync().ConfigureAwait(false);
        var choices = new List<(string Display, string? Id)>();

        foreach (var summary in summaryList)
        {
            var marker = summary.Id == lastActiveId ? " [green]●[/]" : "";
            var preview = string.IsNullOrEmpty(summary.LastMessagePreview)
                ? "[dim grey](empty)[/]"
                : Markup.Escape(summary.LastMessagePreview);
            choices.Add(($"[dim]{summary.Id}[/] [dim grey]{preview}[/]{marker}", summary.Id));
        }

        // Add option to start new session
        choices.Add(("[yellow]+ New session[/]", null));

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select a session to resume:[/]")
                .PageSize(10)
                .UseConverter(c => c)
                .AddChoices(choices.Select(c => c.Display)));

        // Find the ID for the selected display string
        var selectedChoice = choices.FirstOrDefault(c => c.Display == selected);

        if (selectedChoice.Id is null)
        {
            return ChatSession.Create(options.DefaultModel);
        }

        var session = await sessionStore.LoadSessionAsync(selectedChoice.Id).ConfigureAwait(false);
        if (session is not null)
        {
            AnsiConsole.MarkupLine($"[dim]Loaded session: {selectedChoice.Id}[/]");
            return session;
        }

        return ChatSession.Create(options.DefaultModel);
    }

    private async Task ProcessUserInputAsync(ChatSession session)
    {
        var input = inputHandler.ReadInput(session);

        if (input is null)
            return;

        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input))
            return;

        if (commandHandler.IsCommand(input))
        {
            var result = await commandHandler.ExecuteAsync(input, session).ConfigureAwait(false);
            renderer.RenderCommandResult(result, session);

            if (result.ShouldExit)
                Environment.Exit(0);

            return;
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

    private void HandleGeneralException(Exception ex, ChatSession session)
    {
        AnsiConsole.MarkupLine("\n[red]╔════════════════════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[red]║[/] [bold red]An Error Occurred[/]                                      [red]║[/]");
        AnsiConsole.MarkupLine("[red]╚════════════════════════════════════════════════════════╝[/]\n");

        AnsiConsole.MarkupLine($"[bold red]Error:[/] {ex.GetType().Name}");
        AnsiConsole.MarkupLine($"[bold red]Message:[/] {ex.Message}\n");

        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            AnsiConsole.MarkupLine("[dim]Stack trace:[/]");
            AnsiConsole.MarkupLine($"[dim]{ex.StackTrace}[/]\n");
        }

        AnsiConsole.MarkupLine("[yellow]The application will continue. You can try your request again.[/]\n");
        
        // Save session to preserve any progress
        try
        {
            sessionStore.SaveSessionAsync(session).ConfigureAwait(false).GetAwaiter().GetResult();
        }
#pragma warning disable CA1031 // Intentionally catching all exceptions during error recovery
        catch
#pragma warning restore CA1031
        {
            // Ignore save errors during exception handling
        }
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