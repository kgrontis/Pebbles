namespace Pebbles.Services;

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
    private readonly PebblesOptions _options;

    public ChatService(
        IAIProvider aiProvider,
        ICommandHandler commandHandler,
        IChatRenderer renderer,
        IInputHandler inputHandler,
        PebblesOptions options)
    {
        _aiProvider = aiProvider;
        _commandHandler = commandHandler;
        _renderer = renderer;
        _inputHandler = inputHandler;
        _options = options;
    }

    public async Task RunAsync()
    {
        var session = ChatSession.Create(_options.DefaultModel);
        _renderer.RenderWelcome(session);

        while (true)
        {
            var input = _inputHandler.ReadInput();

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

            // User message
            var inputTokens = EstimateTokens(input);
            var userMsg = ChatMessage.User(input, inputTokens);
            session.Messages.Add(userMsg);
            _aiProvider.AddToHistory(userMsg);
            _renderer.RenderUserMessage(input);

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

    private int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * _options.TokenEstimationMultiplier);
}