# Project Guidelines

## Project Overview

**Pebbles** is a terminal-based AI coding assistant chat application. It provides a rich TUI (Terminal User Interface) for interacting with an AI assistant, featuring markdown rendering, slash commands, and streaming responses.

- **Framework:** .NET 10.0
- **UI Library:** Spectre.Console
- **Architecture:** Clean architecture with DI, interfaces, and testable services
- **Deployment:** Native AOT (publish with `dotnet publish -c Release -r <RID>`)

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
            ├── IAIProvider → MockAIProvider
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

## Key Patterns

### Adding a New Service

1. Create interface in appropriate folder:
   ```csharp
   public interface INewService
   {
       Task DoSomethingAsync();
   }
   ```
2. Implement the interface
3. Register in `Program.cs`:
   ```csharp
   .AddSingleton<INewService, NewService>()
   ```

### Adding a New Slash Command

1. Add command definition in `CommandHandler` constructor:
   ```csharp
   ["/newcmd"] = new SlashCommand
   {
       Name = "/newcmd",
       Description = "Description here",
       Usage = "/newcmd [args]",
       Handler = HandleNewCommand
   }
   ```
2. Add handler method:
   ```csharp
   private Task<CommandResult> HandleNewCommand(string[] args, ChatSession session)
   {
       // Use CommandResult.Ok(), .Fail(), .Exit() factory methods
       return Task.FromResult(CommandResult.Ok("Done"));
   }
   ```

### Adding Configuration

1. Add property to `PebblesOptions`
2. Add corresponding entry in `appsettings.json`
3. Bind in `Program.cs`

## Git

- **Conventions:** Use conventional commits (feat/fix/refactor/docs/test/chore)
- **Scope:** Atomic commits — one concern per commit
- **Branches:** Never force push to main

## Safety

- Never hardcode secrets or API keys
- Validate user input at boundaries
- Handle exceptions gracefully with user-friendly messages
- No silent failures — log or display errors

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