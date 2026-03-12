# Project Guidelines

## Project Overview

**Pebbles** is a terminal-based AI coding assistant chat application. It provides a rich TUI (Terminal User Interface) for interacting with an AI assistant, featuring markdown rendering, slash commands, and streaming responses.

- **Framework:** .NET 10.0
- **UI Library:** Spectre.Console
- **Architecture:** Clean architecture with DI, interfaces, and testable services
- **Deployment:** Native AOT (publish with `dotnet publish -c Release -r <RID>`)

## Core Mandates

- **Conventions:** Follow existing project conventions when reading or modifying code. Analyze surrounding code, tests, and configuration first.
- **Libraries/Frameworks:** Verify established usage within the project before employing new libraries.
- **Style & Structure:** Mimic the style (formatting, naming), structure, and architectural patterns of existing code.
- **Idiomatic Changes:** Understand local context (imports, methods/classes) to ensure changes integrate naturally.
- **Comments:** Add code comments sparingly. Focus on *why* something is done, not *what*.
- **Proactiveness:** Fulfill the user's request thoroughly. Consider edge cases and error handling.

## Project Structure

```
Pebbles/
├── Program.cs              # Minimal entry point (DI setup, run)
├── appsettings.json        # Configuration (models, costs)
├── Models/                 # Data models (records)
│   ├── ChatModels.cs       # ChatMessage, ChatSession, ThinkingBlock
│   └── CommandModels.cs    # SlashCommand, CommandResult
├── Services/               # Business logic + interfaces
│   ├── IAIProvider.cs      # AI response abstraction
│   ├── ICommandHandler.cs  # Command processing abstraction
│   ├── IChatService.cs     # Main loop abstraction
│   ├── ChatService.cs      # Main application loop
│   ├── CommandHandler.cs   # Slash command processing
│   └── MockAIProvider.cs   # Mock AI with streaming
├── UI/                     # Terminal UI components + interfaces
│   ├── IChatRenderer.cs    # Rendering abstraction
│   ├── IInputHandler.cs    # Input handling abstraction
│   ├── ChatRenderer.cs     # Message rendering, markdown
│   └── InputHandler.cs     # Input, history, autocomplete
├── Configuration/          # Strongly-typed config
│   └── PebblesOptions.cs   # Configuration POCO
└── Fonts/                  # Embedded FIGlet fonts
    ├── FigletFontLoader.cs
    └── slant.flf
```

## Architecture

The application follows dependency injection patterns:

```
Program.cs
    │
    ├── PebblesOptions (configuration)
    │
    └── ServiceCollection
            ├── IAIProvider → DashScopeProvider
            ├── ICommandHandler → CommandHandler
            ├── IChatRenderer → ChatRenderer
            ├── IInputHandler → InputHandler
            └── IChatService → ChatService
```

## Code Style

- **C# conventions:** File-scoped namespaces, target-typed `new()`, nullable reference types enabled
- **Naming:** PascalCase for public members, `_camelCase` for private fields
- **Models:** Use `record` types for immutability; `class` only for mutable state (ChatSession)
- **Methods:** Keep under 50 lines; extract complex logic into helper methods
- **Comments:** XML docs on public interfaces; prefer self-documenting code
- **Async:** Use `async`/`await` for all I/O; avoid `.Result` and `.Wait()`

## Markdown Formatting

When generating markdown tables, **each cell must be on a single line**. Do not include line breaks within table cells.

**Bad (broken rendering):**
```markdown
| Method | Purpose |
|--------|---------|
| `RunAsync` | `public async Task` | **Main loop** - handles
user input, slash commands, file references |
```

**Good (correct):**
```markdown
| Method | Purpose |
|--------|---------|
| `RunAsync` | `public async Task` | **Main loop** - handles user input, slash commands, file references |
```

If a cell content is too long, keep it on one line or use abbreviations. Line breaks inside cells break markdown table rendering.

## Key Patterns

### Adding a New Service

<example>
user: Add a new service for caching responses
model:
<function=WriteFile>
<parameter=file_path>
Services/ICacheService.cs
</parameter>
<parameter=content>
namespace Pebbles.Services;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
}
</parameter>
</function>
</example>

1. Create interface in appropriate folder
2. Implement the interface
3. Register in `Program.cs`:
   ```csharp
   .AddSingleton<ICacheService, CacheService>()
   ```

### Adding a New Slash Command

<example>
user: Add a /version command
model:
<function=Edit>
<parameter=file_path>
Services/CommandHandler.cs
</parameter>
<parameter=old_content>
["/help"] = new SlashCommand
{
    Name = "/help",
    Description = "Show available commands",
    Usage = "/help",
    Handler = HandleHelp
}
</parameter>
<parameter=new_content>
["/help"] = new SlashCommand
{
    Name = "/help",
    Description = "Show available commands",
    Usage = "/help",
    Handler = HandleHelp
},
["/version"] = new SlashCommand
{
    Name = "/version",
    Description = "Show Pebbles version",
    Usage = "/version",
    Handler = HandleVersion
}
</parameter>
</function>
</example>

1. Add command definition in `CommandHandler` constructor
2. Add handler method:
   ```csharp
   private Task<CommandResult> HandleVersion(string[] args, ChatSession session)
   {
       var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
       return Task.FromResult(CommandResult.Ok($"Pebbles v{version}"));
   }
   ```

### Adding Configuration

1. Add property to `PebblesOptions`
2. Add corresponding entry in `appsettings.json`
3. Bind in `Program.cs`

## Safety Rules

- **Explain Critical Commands:** Before executing commands that modify the file system, codebase, or system state, provide a brief explanation of the command's purpose and potential impact.
- **Security First:** Never hardcode secrets or API keys. Use environment variables or secure configuration.
- **Input Validation:** Validate user input at boundaries.
- **Error Handling:** Handle exceptions gracefully with user-friendly messages.
- **No Silent Failures:** Log or display errors. Never swallow exceptions without notification.

## Git

- **Conventions:** Use conventional commits (feat/fix/refactor/docs/test/chore)
- **Scope:** Atomic commits — one concern per commit
- **Branches:** Never force push to main

## Running the Application

```bash
dotnet build                 # Build the project
dotnet run                   # Run the chat application (debug)

# Native AOT publish (release)
dotnet publish -c Release -r win-x64    # Windows x64
dotnet publish -c Release -r linux-x64  # Linux x64
dotnet publish -c Release -r osx-x64    # macOS x64
```

**Native AOT Notes:**
- Single-file, self-contained executable with fast startup
- `InvariantGlobalization` enabled for smaller size
- `OptimizationPreference=Speed` for performance

## Testing

To add tests, create `Pebbles.Tests` project:
- Use xUnit v3
- Mock interfaces (`IAIProvider`, `IChatRenderer`, etc.)
- Test `ChatService` and `CommandHandler` in isolation

## Future Considerations

When connecting to a real AI provider:
- Implement `IAIProvider` (e.g., `OpenAIProvider`, `AnthropicProvider`)
- Add API key configuration via environment variables
- Register new provider in `Program.cs`