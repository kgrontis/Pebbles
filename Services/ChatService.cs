namespace Pebbles.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.UI;
using Spectre.Console;

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
    private readonly ICompressionService _compressionService;
    private readonly IMemoryService _memoryService;
    private readonly PebblesOptions _options;
    private readonly ToolRegistry _toolRegistry;

    public ChatService(
        IAIProvider aiProvider,
        ICommandHandler commandHandler,
        IChatRenderer renderer,
        IInputHandler inputHandler,
        IFileService fileService,
        ICompressionService compressionService,
        IMemoryService memoryService,
        PebblesOptions options,
        ToolRegistry toolRegistry)
    {
        _aiProvider = aiProvider;
        _commandHandler = commandHandler;
        _renderer = renderer;
        _inputHandler = inputHandler;
        _fileService = fileService;
        _compressionService = compressionService;
        _memoryService = memoryService;
        _options = options;
        _toolRegistry = toolRegistry;
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

            // Execute tool calling loop (handles AI response + any tool calls)
            var assistantMsg = await AddToolExecutionLoopAsync(input, session);

            // Record assistant message
            session.Messages.Add(assistantMsg);
            _aiProvider.AddToHistory(assistantMsg);

            // Render the assistant message (with thinking if present)
            _renderer.RenderAssistantMessage(assistantMsg.Content, assistantMsg.Thinking);

            // Update session totals and show token info
            session.TotalInputTokens += inputTokens;
            session.TotalOutputTokens += assistantMsg.TokenCount;
            _renderer.RenderTokenInfo(inputTokens, assistantMsg.TokenCount, session);

            // Check for auto-compression
            await CheckAutoCompressionAsync(session);

            // Check for automatic memory extraction
            await CheckMemoryExtractionAsync(session);
        }
    }

    private async Task<ChatMessage> AddToolExecutionLoopAsync(string input, ChatSession session, CancellationToken cancellationToken = default)
    {
        var maxToolIterations = 5; // Prevent infinite tool call loops
        var iteration = 0;
        string? finalContent = null;
        int outputTokens = 0;
        string thinking = string.Empty;
        TimeSpan thinkingDuration = TimeSpan.Zero;

        while (iteration < maxToolIterations)
        {
            iteration++;

            // Get response from AI (with tool support)
            var toolDefinitions = _toolRegistry.GetAllToolDefinitions();
            
            // Debug: Show available tools
            AnsiConsole.MarkupLine($"[dim]Available tools: {string.Join(", ", toolDefinitions.Select(t => t.Function?.Name ?? t.Type))}[/]");
            
            var response = await _aiProvider.GetResponseWithToolsAsync(
                input,
                toolDefinitions,
                cancellationToken: cancellationToken);

            // Capture thinking if present (from AI provider's internal state)
            thinking = _aiProvider.GetLastThinking();
            thinkingDuration = _aiProvider.GetLastThinkingDuration();
            outputTokens = response.OutputTokens;

            // If no tool calls, we have the final response
            if (response.ToolCalls.Count == 0)
            {
                finalContent = response.Content;
                break;
            }

            // Execute tool calls
            var toolResults = new List<ToolResult>();
            foreach (var toolCall in response.ToolCalls)
            {
                AnsiConsole.MarkupLine($"[dim]🔧 Executing: {toolCall.Function.Name}[/]");
                AnsiConsole.MarkupLine($"[dim]   Arguments: {toolCall.Function.Arguments}[/]");

                var result = await _toolRegistry.ExecuteToolAsync(
                    toolCall.Function.Name,
                    toolCall.Function.Arguments,
                    cancellationToken);

                toolResults.Add(new ToolResult
                {
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function.Name,
                    Content = result.Success ? result.Content : result.Error ?? "Unknown error",
                    IsError = !result.Success
                });

                AnsiConsole.MarkupLine(
                    result.Success
                        ? $"[green]✓ {toolCall.Function.Name} completed[/]"
                        : $"[red]✗ {toolCall.Function.Name} failed: {result.Error}[/]");
            }

            // Send tool results back to AI for continuation
            input = $"Tool results: {System.Text.Json.JsonSerializer.Serialize(toolResults)}";
        }

        // Return the final assistant message
        return ChatMessage.Assistant(
            finalContent ?? "Tool execution completed.",
            outputTokens,
            new ThinkingBlock { Content = thinking, Duration = thinkingDuration });
    }

    /// <summary>
    /// Checks if auto-compression should be triggered and performs it if needed.
    /// </summary>
    private async Task CheckAutoCompressionAsync(ChatSession session)
    {
        if (!_options.AutoCompressionEnabled || !session.AutoCompressionEnabled)
            return;

        if (session.IsCompressing)
            return;

        var currentTokens = session.TotalInputTokens + session.TotalOutputTokens;
        var contextWindow = _options.GetContextWindowTokens(session.Model);

        if (!_compressionService.ShouldCompact(currentTokens, contextWindow, _options.CompressionThreshold))
            return;

        if (session.Messages.Count <= _options.KeepRecentMessages)
            return;

        // Perform auto-compression
        session.IsCompressing = true;

        try
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[dim yellow]⚠ Context threshold reached. Auto-compressing...[/]");

            var result = await _compressionService.CompactAsync(
                session.Messages,
                _options.KeepRecentMessages,
                session.CompressionStats.LastSummary);

            if (!result.Success || result.MessagesSummarized == 0)
            {
                AnsiConsole.MarkupLine($"[dim red]Auto-compression skipped: {result.Error ?? "No messages to summarize"}[/]");
                return;
            }

            // Replace old messages with summary
            var summaryMessage = ChatMessage.User(
                $"[Previous conversation summary]\n{result.Summary}",
                result.TokensAfter);

            var keptMessages = session.Messages
                .Skip(session.Messages.Count - _options.KeepRecentMessages)
                .ToList();

            session.Messages.Clear();
            session.Messages.Add(summaryMessage);
            foreach (var msg in keptMessages)
            {
                session.Messages.Add(msg);
            }

            // Update stats
            session.CompressionStats.Count++;
            session.CompressionStats.TotalTokensSaved += result.TokensBefore - result.TokensAfter;
            session.CompressionStats.LastCompressionTime = DateTime.Now;
            session.CompressionStats.LastSummary = result.Summary;

            AnsiConsole.MarkupLine($"[dim green]✓ Auto-compressed: {result.TokensBefore:N0} → {result.TokensAfter:N0} tokens[/]");
            AnsiConsole.MarkupLine("");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[dim red]Auto-compression failed: {ex.Message}[/]");
        }
        finally
        {
            session.IsCompressing = false;
        }
    }

    /// <summary>
    /// Checks if automatic memory extraction should be triggered.
    /// Extracts memories every N messages to capture user preferences.
    /// </summary>
    private async Task CheckMemoryExtractionAsync(ChatSession session)
    {
        // Only extract every 10 messages to avoid excessive API calls
        const int extractionInterval = 10;

        if (session.Messages.Count < extractionInterval)
            return;

        if (session.Messages.Count % extractionInterval != 0)
            return;

        try
        {
            var extracted = await _memoryService.ExtractMemoriesAsync(session.Messages);

            if (!string.IsNullOrEmpty(extracted))
            {
                _memoryService.SaveMemories(extracted);
                AnsiConsole.MarkupLine("[dim]💡 Extracted new memories from conversation[/]");
            }
        }
        catch
        {
            // Silently fail - memory extraction is not critical
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