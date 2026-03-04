using Spectre.Console;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.UI;

var session = new ChatSession();
var provider = new MockAIProvider();
var commands = new CommandHandler();
var renderer = new ChatRenderer(session);
var inputHandler = new InputHandler(commands.Commands);

renderer.RenderWelcome();

while (true)
{
    var input = inputHandler.ReadInput();

    if (input is null)
        break;

    input = input.Trim();
    if (string.IsNullOrWhiteSpace(input))
        continue;

    // Handle slash commands
    if (commands.IsCommand(input))
    {
        var result = await commands.ExecuteAsync(input, session);
        renderer.RenderCommandResult(result);

        if (result.ShouldExit)
            break;

        continue;
    }

    // User message
    var userMsg = new ChatMessage
    {
        Role = ChatRole.User,
        Content = input,
        TokenCount = EstimateTokens(input)
    };
    session.Messages.Add(userMsg);
    renderer.RenderUserMessage(input);

    // Get mock response
    var response = provider.GetResponse(input);

    // Render thinking (unless compact mode)
    if (!session.CompactMode)
    {
        await renderer.RenderThinkingAsync(provider, response);
    }

    // Stream assistant response
    await renderer.RenderAssistantStreamAsync(provider, response);

    // Record assistant message
    var assistantMsg = new ChatMessage
    {
        Role = ChatRole.Assistant,
        Content = response.Content,
        Thinking = new ThinkingBlock
        {
            Content = response.Thinking,
            Duration = response.ThinkingDuration
        },
        TokenCount = EstimateTokens(response.Content)
    };
    session.Messages.Add(assistantMsg);

    // Show token info
    var inputTokens = EstimateTokens(input);
    var outputTokens = EstimateTokens(response.Content) + EstimateTokens(response.Thinking);
    renderer.RenderTokenInfo(inputTokens, outputTokens);
}

static int EstimateTokens(string text) =>
    (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.3);
