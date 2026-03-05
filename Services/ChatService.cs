namespace Pebbles.Services;

using Spectre.Console;
using Pebbles.Models;
using Pebbles.UI;
using Pebbles.Configuration;

/// <summary>
/// Main chat application service.
/// </summary>
public class ChatService : IChatService
{
    private readonly IAIProvider _aiProvider;
    private readonly ICommandHandler _commandHandler;
    private readonly IChatRenderer _renderer;
    private readonly IInputHandler _inputHandler;
    private readonly IFileService _fileService;
    private readonly PebblesOptions _options;

    public ChatService(
        IAIProvider aiProvider,
        ICommandHandler commandHandler,
        IChatRenderer renderer,
        IInputHandler inputHandler,
        IFileService fileService,
        PebblesOptions options)
    {
        _aiProvider = aiProvider;
        _commandHandler = commandHandler;
        _renderer = renderer;
        _inputHandler = inputHandler;
        _fileService = fileService;
        _options = options;
    }

    public async Task RunAsync()
    {
        var session = ChatSession.Create(_options.DefaultModel);
        _renderer.RenderWelcome(session);

        while (true)
        {
            var input = _inputHandler.ReadInput(session);

            if (input is null)
                break;

            input = input.Trim();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Handle slash commands
            if (_commandHandler.IsCommand(input))
            {
                var result = await _commandHandler.ExecuteAsync(input, session);
                _renderer.RenderCommandResult(result, session);

                if (result.ShouldExit)
                    break;

                continue;
            }

            // Process file references (@file.cs syntax)
            var parsed = _fileService.ParseFileReferences(input);
            if (parsed.HasFiles)
            {
                await LoadFilesAsync(parsed);
                input = parsed.CleanInput;
            }

            // User message
            var inputTokens = EstimateTokens(input);
            var userMsg = ChatMessage.User(parsed.Original, inputTokens);
            session.Messages.Add(userMsg);
            _aiProvider.AddToHistory(userMsg);
            _renderer.RenderUserMessage(parsed.Original);

            // Stream assistant response directly from API
            var (content, thinking, thinkingDuration, outputTokens) =
                await _renderer.RenderAssistantLiveStreamAsync(_aiProvider, input, session.CompactMode);

            // Record assistant message
            var assistantMsg = ChatMessage.Assistant(
                content,
                outputTokens,
                new ThinkingBlock
                {
                    Content = thinking,
                    Duration = thinkingDuration
                });
            session.Messages.Add(assistantMsg);
            _aiProvider.AddToHistory(assistantMsg);

            // Update session totals and show token info
            session.TotalInputTokens += inputTokens;
            session.TotalOutputTokens += outputTokens;
            _renderer.RenderTokenInfo(inputTokens, outputTokens, session);
        }
    }

    private async Task LoadFilesAsync(ParsedInput parsed)
    {
        AnsiConsole.MarkupLine("[dim]Loading files...[/]");
        var loaded = 0;
        var failed = 0;

        foreach (var fileRef in parsed.FileReferences)
        {
            var content = _fileService.ReadFile(fileRef.Path);

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
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * _options.TokenEstimationMultiplier);
}