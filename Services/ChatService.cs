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
            _renderer.RenderUserMessage(input);

            // Get AI response
            var response = _aiProvider.GetResponse(input);

            // Render thinking (unless compact mode)
            if (!session.CompactMode)
            {
                await _renderer.RenderThinkingAsync(_aiProvider, response);
            }

            // Stream assistant response
            await _renderer.RenderAssistantStreamAsync(_aiProvider, response);

            // Record assistant message
            var outputTokens = EstimateTokens(response.Content) + EstimateTokens(response.Thinking);
            var assistantMsg = ChatMessage.Assistant(
                response.Content,
                outputTokens,
                new ThinkingBlock
                {
                    Content = response.Thinking,
                    Duration = response.ThinkingDuration
                });
            session.Messages.Add(assistantMsg);

            // Update session totals and show token info
            session.TotalInputTokens += inputTokens;
            session.TotalOutputTokens += outputTokens;
            _renderer.RenderTokenInfo(inputTokens, outputTokens, session);
        }
    }

    private int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * _options.TokenEstimationMultiplier);
}