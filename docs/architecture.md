# Pebbles Architecture

## Overview

Pebbles is a terminal-based AI coding assistant built with .NET 10 and Spectre.Console. It follows clean architecture principles with clear separation of concerns.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Program.cs                              │
│  • Console encoding setup                                       │
│  • Configuration building                                       │
│  • DI container setup                                           │
│  • Service provider build                                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ChatService                                │
│  • Main application loop                                        │
│  • Session management (auto-load/save)                          │
│  • Orchestrates: Input → Commands → AI → Tools → Output         │
└─────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  InputHandler   │ │CommandHandler   │ │  ChatRenderer   │
│  • Read input   │ │• /commands      │ │• Render UI      │
│  • @file refs   │ │• Execute        │ │• Markdown       │
│  • History      │ │• Plugin cmds    │ │• Streaming      │
└─────────────────┘ └─────────────────┘ └─────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   CompositeCommandHandler                       │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
│  │ChatCommands   │ │FileCommands   │ │SessionCommands│         │
│  │/clear, /exit  │ │/read, /files  │ │/save, /load   │         │
│  └───────────────┘ └───────────────┘ └───────────────┘         │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
│  │CompressionCmds│ │MemoryCommands │ │PluginCommands │         │
│  │/compress      │ │/remember      │ │/plugins       │         │
│  └───────────────┘ └───────────────┘ └───────────────┘         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      IAIProvider                                │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
│  │AlibabaCloud   │ │ OpenAIProvider│ │AnthropicProv. │         │
│  │Provider       │ │(GPT-4)        │ │(Claude)       │         │
│  │(Qwen, GLM)    │ │               │ │               │         │
│  └───────────────┘ └───────────────┘ └───────────────┘         │
│  ┌───────────────┐                                             │
│  │ MockAIProvider│ (for testing)                               │
│  └───────────────┘                                             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    ToolExecutionService                         │
│  • Tool calling loop (max 5 iterations)                         │
│  • Tool result handling                                         │
│  • Error handling with retry                                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       ToolRegistry                              │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
│  │ReadFileTool   │ │WriteFileTool  │ │ ShellTool     │         │
│  │read_file      │ │write_file     │ │run_command    │         │
│  └───────────────┘ └───────────────┘ └───────────────┘         │
│  ┌───────────────┐ ┌───────────────┐ ┌───────────────┐         │
│  │ListDirectory  │ │SearchFilesTool│ │ToolPlugins    │         │
│  │list_directory │ │search_files   │ │(Roslyn)       │         │
│  └───────────────┘ └───────────────┘ └───────────────┘         │
└─────────────────────────────────────────────────────────────────┘
```

## Core Services

### ChatService
**Responsibility:** Main application loop and orchestration

**Dependencies:**
- `IAIProvider` - AI responses
- `ICommandHandler` - Command execution
- `IChatRenderer` - UI rendering
- `IInputHandler` - User input
- `IFileService` - File operations
- `IToolExecutionService` - Tool execution
- `IContextManagementService` - Compression/memory
- `ISessionStore` - Session persistence
- `PebblesOptions` - Configuration

**Key Methods:**
- `RunAsync()` - Main loop
- `LoadFilesAsync()` - Load @file references

### CompositeCommandHandler
**Responsibility:** Aggregate and route slash commands

**Pattern:** Composite Pattern

**Sub-handlers:**
- `ChatCommands` - /clear, /exit, /help, /model, /history, /cost
- `FileCommands` - /read, /files, /clearfiles
- `SessionCommands` - /save, /load, /sessions, /delete
- `CompressionCommands` - /compress, /autocompress
- `MemoryCommands` - /remember, /memory
- `PluginCommands` - /reload, /plugins, /tools

### ToolExecutionService
**Responsibility:** Execute AI tool calls

**Pattern:** Service Pattern

**Key Methods:**
- `ExecuteToolLoopAsync()` - Tool calling loop with max iterations

### ToolRegistry
**Responsibility:** Tool registration and execution

**Pattern:** Registry Pattern

**Built-in Tools:**
1. `ReadFileTool` - Read file contents
2. `WriteFileTool` - Write/create files
3. `ShellTool` - Execute shell commands
4. `ListDirectoryTool` - List directory contents
5. `SearchFilesTool` - Search text patterns

## Data Flow

### User Message Flow
```
User Input → InputHandler → ChatService
                              │
                              ├──→ FileService (parse @refs)
                              │
                              ├──→ AIProvider (get response)
                              │        │
                              │        └──→ Tool calls?
                              │                 │
                              │                 ▼
                              │         ToolExecutionService
                              │                 │
                              │                 ▼
                              │         ToolRegistry → Execute
                              │
                              └──→ ChatRenderer (display)
```

### Command Flow
```
/command → CommandHandler.IsCommand()
             │
             ▼
    CompositeCommandHandler
             │
             ├──→ ChatCommands
             ├──→ FileCommands
             ├──→ SessionCommands
             ├──→ CompressionCommands
             ├──→ MemoryCommands
             └──→ PluginCommands
```

## Configuration

### appsettings.json
```json
{
  "Pebbles": {
    "Provider": "alibabacloud|openai|anthropic|mock",
    "DefaultModel": "qwen3.5-plus",
    "AlibabaCloudApiKey": "...",
    "OpenAiApiKey": "...",
    "AnthropicApiKey": "...",
    "AutoCompressionEnabled": true,
    "CompressionThreshold": 0.7,
    "KeepRecentMessages": 6
  }
}
```

### Environment Variables
- `ALIBABA_CLOUD_API_KEY` - Alibaba Cloud API key
- `OPENAI_API_KEY` - OpenAI API key
- `ANTHROPIC_API_KEY` - Anthropic API key

## Session Persistence

**Storage:** `~/.pebbles/sessions/{sessionId}.json`

**Auto-save:** After each message

**Commands:**
- `/save` - Save current session
- `/load <id>` - Load session
- `/sessions` - List sessions
- `/delete <id>` - Delete session

## Plugin System

### Command Plugins
**Location:** `~/.pebbles/agent/plugins/scripts/`

**Base Class:** `PluginBase`

**Example:**
```csharp
public class MyPlugin : PluginBase
{
    public override string Name => "my-plugin";
    public override string Version => "1.0.0";
    
    public override IEnumerable<Command> GetCommands()
    {
        yield return new Command { /* ... */ };
    }
}
```

### Tool Plugins
**Interface:** `IToolPlugin`

**Base Class:** `ToolPluginBase`

**Helper Methods:**
- `ShellAsync()` - Execute commands
- `ReadFile()` - Read files
- `WriteFile()` - Write files

## Testing Strategy

### Unit Tests (`Pebbles.Tests`)
- Test individual components in isolation
- Mock external dependencies
- 42+ tests covering core functionality

### Integration Tests (`Pebbles.IntegrationTests`)
- Test tool execution with real services
- Test file system operations
- 10+ tests

### E2E Tests (`Pebbles.E2ETests`)
- Test full user sessions
- Test session persistence
- Test plugin loading
- 5+ tests

### Benchmarks (`Pebbles.Benchmarks`)
- Tool execution latency
- Compression performance
- File I/O performance

## CI/CD

### GitHub Actions
- **CI Workflow:** Build and test on push/PR
- **Release Workflow:** Native AOT builds on tags

### Coverage
- Target: 80%+ code coverage
- Reports via Codecov

## Design Patterns

| Pattern | Usage |
|---------|-------|
| Dependency Injection | All services |
| Provider Pattern | IAIProvider implementations |
| Composite Pattern | Command handlers |
| Registry Pattern | ToolRegistry |
| Options Pattern | PebblesOptions |
| Service Pattern | Business logic services |

## Performance Considerations

- **Streaming:** Real-time token streaming
- **Async/Await:** All I/O operations async
- **Retry Policies:** Polly for transient failures
- **Context Compression:** Automatic token management
- **Native AOT:** Single-file optimized builds

## Security

- API keys via environment variables
- Command validation (blocks dangerous commands)
- Path validation (blocks system directories)
- File size limits (10MB max)
- Timeout limits (300s max for commands)
