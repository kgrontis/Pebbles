# Pebbles

A terminal-based AI coding assistant built with .NET 10 and Spectre.Console.

Your AI coding assistant in the terminal — fast, focused, and extensible.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-blue)

## Features

### 🚀 Streaming AI Chat

Real-time streaming responses from AI models. Watch the answer unfold as it's generated — no waiting for complete responses.

### 🤖 Multiple AI Models

Choose from 8 powerful AI models optimized for coding:

| Model                  | Best For                  |
| ---------------------- | ------------------------- |
| `qwen3.5-plus`         | General purpose (default) |
| `qwen3-coder-plus`     | Code generation & review  |
| `qwen3-coder-next`     | Latest coding model       |
| `qwen3-max-2026-01-23` | Most capable reasoning    |
| `glm-4.7`              | Alternative general model |
| `glm-5`                | Latest GLM generation     |
| `MiniMax-M2.5`         | Long-context tasks        |
| `kimi-k2.5`            | Extended conversations    |

### ⌨️ Keyboard Shortcuts

Navigate and control Pebbles efficiently:

| Shortcut     | Action                              |
| ------------ | ----------------------------------- |
| `Tab`        | Accept autocomplete suggestion      |
| `Escape`     | Dismiss suggestions / Clear input   |
| `↑` / `↓`    | Navigate suggestions / History      |
| `←` / `→`    | Move cursor                         |
| `Home`       | Jump to start of line               |
| `End`        | Jump to end of line                 |
| `Ctrl+U`     | Clear current line                  |
| `Ctrl+C`     | Exit Pebbles                        |

### 🔍 Interactive Autocomplete

**Command Autocomplete:** Type `/` to see available commands. Use `↑`/`↓` to navigate and `Tab` to accept.

**File Picker:** Type `@` to browse files interactively:
- Shows files and directories in the current folder
- Type to filter the list
- Navigate into directories by selecting them
- Press `Tab` to insert the selected path

### 💭 Thinking Mode

When using models that support extended reasoning (like `qwen3-max-2026-01-23`), Pebbles displays the AI's thinking process in a block before the response.

### 💬 Slash Commands

Full control over your session with 13 built-in commands:

| Command        | Description                          |
| -------------- | ------------------------------------ |
| `/help`        | Show available commands              |
| `/clear`       | Clear chat history                   |
| `/model`       | Switch AI model (interactive picker) |
| `/history`     | Show conversation history summary    |
| `/cost`        | Show token usage and estimated cost  |
| `/context`     | Show loaded project context          |
| `/read <path>` | Read a file into context             |
| `/files`       | List loaded files in context         |
| `/clearfiles`  | Clear all loaded files from context  |
| `/reload`      | Reload plugins                       |
| `/plugins`     | List loaded plugins and commands     |
| `/exit`        | Exit Pebbles                         |

### 📁 File Context

Reference files directly in your messages using `@file.cs` syntax:

```
What does the Process method do in @Services/ChatService.cs?
```

Pebbles automatically loads and includes the file content in the AI context. Supports:

- All text file formats (.cs, .js, .py, .md, .json, .yaml, etc.)
- Size limit: 1MB per file
- Binary files are automatically detected and skipped

### 🗂️ Project Context

Store project-specific instructions in `.pebbles/agent/AGENTS.md`:

```markdown
# Project Guidelines

- Use Entity Framework Core for data access
- Follow async/await patterns
- Write unit tests for all services
```

Pebbles automatically includes these guidelines in every AI conversation. Supports both:

- **Project context:** `.pebbles/agent/AGENTS.md` (current directory)
- **Global context:** `~/.pebbles/agent/AGENTS.md` (user profile)

### 📊 Token Tracking

Real-time token usage and cost estimation:

```
⎯ 156 input → 423 output • $0.0012 • 579 total tokens
```

Track your spending with `/cost` command.

---

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/pebbles.git
cd pebbles

# Build and run
dotnet run
```

### Configuration

Create `appsettings.json` in the application directory:

```json
{
  "Pebbles": {
    "DefaultModel": "qwen3.5-plus",
    "Provider": "dashscope",
    "DashScopeBaseUrl": "https://coding-intl.dashscope.aliyuncs.com/v1",
    "InputCostPer1K": 0.0004,
    "OutputCostPer1K": 0.0024,
    "TokenEstimationMultiplier": 1.3,
    "SystemPrompt": "You are Pebbles, a helpful AI coding assistant."
  }
}
```

### API Key

Set your DashScope API key via environment variable:

```bash
# Linux/macOS
export BAILIAN_CODING_PLAN_API_KEY=your-api-key-here

# Windows PowerShell
$env:BAILIAN_CODING_PLAN_API_KEY="your-api-key-here"

# Windows CMD
set BAILIAN_CODING_PLAN_API_KEY=your-api-key-here
```

Or add it to `appsettings.json`:

```json
{
  "Pebbles": {
    "DashScopeApiKey": "your-api-key-here"
  }
}
```

---

## Usage

### Quick Start

```bash
dotnet run
```

You'll see the welcome screen.

### Basic Chat

Just type your question:

```
❯ You: How do I implement a retry pattern with Polly?

⬡ Pebbles:
Here's how to implement a retry pattern with Polly in C#:

┌─ csharp ──────────────────────────────────
│ var retryPolicy = Policy
│     .Handle<HttpRequestException>()
│     .WaitAndRetryAsync(3, retryAttempt =>
│         TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
│
│ await retryPolicy.ExecuteAsync(async () =>
│ {
│     await httpClient.GetAsync(url);
│ });
└──────────────────────────────────────────
```

### Loading Files

Use `@` to reference files:

```
❯ You: Review this code for potential issues:
      @Program.cs
      @Services/ChatService.cs

✓ Program.cs (2.5 KB)
✓ ChatService.cs (4.1 KB)
Loaded 2 file(s) into context

⬡ Pebbles: I've reviewed the files. Here are my suggestions...
```

### Switching Models

Interactive model picker:

```
❯ You: /model

? Select a model:
  qwen3.5-plus (current)
→ qwen3-coder-plus
  qwen3-coder-next
  qwen3-max-2026-01-23
  glm-4.7
  glm-5
  MiniMax-M2.5
  kimi-k2.5
```

Or specify directly:

```
❯ You: /model qwen3-coder-plus
● Switched to model: qwen3-coder-plus
```

---

## Configuration Options

| Option                      | Type     | Default                                         | Description                           |
| --------------------------- | -------- | ----------------------------------------------- | ------------------------------------- |
| `DefaultModel`              | string   | `qwen3.5-plus`                                  | Default AI model                      |
| `AvailableModels`           | string[] | (8 models)                                      | Models available for selection        |
| `Provider`                  | string   | `dashscope`                                     | AI provider (`dashscope` or `mock`)   |
| `DashScopeApiKey`           | string?  | `null`                                          | API key (or use env variable)         |
| `DashScopeBaseUrl`          | string   | `https://coding-intl.dashscope.aliyuncs.com/v1` | API endpoint                          |
| `InputCostPer1K`            | decimal  | `0.0004`                                        | Cost per 1K input tokens (USD)        |
| `OutputCostPer1K`           | decimal  | `0.0024`                                        | Cost per 1K output tokens (USD)       |
| `TokenEstimationMultiplier` | double   | `1.3`                                           | Words × multiplier = estimated tokens |
| `SystemPrompt`              | string   | `"You are Pebbles..."`                          | System prompt for AI                  |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Program.cs                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │ Config      │  │ DI Container│  │ Service Provider    │ │
│  │ (Options)   │→ │ Setup       │→ │ Build & Resolve     │ │
│  └─────────────┘  └─────────────┘  └─────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                       ChatService                           │
│  ┌──────────────┐  ┌───────────────┐  ┌────────────────┐   │
│  │ InputHandler │→ │ CommandHandler│→ │ ChatRenderer   │   │
│  │ (User Input) │  │ (/commands)   │  │ (Output)       │   │
│  └──────────────┘  └───────────────┘  └────────────────┘   │
│         │                  │                   ▲            │
│         │                  ▼                   │            │
│         │          ┌───────────────┐           │            │
│         └─────────→│  IAIProvider  │───────────┘            │
│                    │  (Streaming)  │                        │
│                    └───────────────┘                        │
│                           │                                 │
│              ┌────────────┼────────────┐                    │
│              ▼            ▼            ▼                    │
│        ┌──────────┐ ┌───────────┐ ┌──────────┐             │
│        │DashScope │ │FileService│ │Context   │             │
│        │Provider  │ │(@files)   │ │Manager   │             │
│        └──────────┘ └───────────┘ └──────────┘             │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component        | Responsibility                                   |
| ---------------- | ------------------------------------------------ |
| `ChatService`    | Main application loop, orchestrates all services |
| `IAIProvider`    | Abstraction for AI providers (DashScope, Mock)   |
| `CommandHandler` | Parses and executes slash commands               |
| `ChatRenderer`   | UI rendering with Spectre.Console                |
| `InputHandler`   | User input processing                            |
| `FileService`    | File loading and @reference parsing              |
| `ContextManager` | Project/global context management                |
| `ModelPicker`    | Interactive model selection UI                   |

### Design Patterns

- **Dependency Injection** — All services registered via `IServiceCollection`
- **Provider Pattern** — `IAIProvider` interface for swappable AI backends
- **Options Pattern** — Configuration via `PebblesOptions` class
- **Streaming** — `IAsyncEnumerable<T>` for real-time token streaming

---

### 🔌 Plugin System

Pebbles supports C# plugins compiled at runtime using Roslyn. Plugins are loaded from:

- **Global:** `~/.pebbles/agent/plugins/scripts/`
- **Project:** `./.pebbles/agent/plugins/scripts/`

#### Plugin Commands

| Command        | Description                          |
| -------------- | ------------------------------------ |
| `/reload`      | Reload all plugins                   |
| `/plugins`     | List loaded plugins and commands     |

#### Creating a Plugin

Create a `.cs` file in the plugins directory:

```csharp
// ~/.pebbles/agent/plugins/scripts/MyTools.cs

using Pebbles.Plugins;

public class MyTools : PluginBase
{
    public override string Name => "my-tools";
    public override string Version => "1.0.0";
    public override string Description => "My custom commands";

    public override IEnumerable<Command> GetCommands()
    {
        yield return new Command
        {
            Name = "/mycmd",
            Description = "My custom command",
            Usage = "/mycmd [args]",
            Handler = (args, session) =>
            {
                return $"Hello from my plugin! Args: {string.Join(" ", args)}";
            }
        };

        yield return new Command
        {
            Name = "/git",
            Description = "Run git commands",
            Handler = (args, session) =>
            {
                return Shell($"git {string.Join(" ", args)}");
            }
        };
    }
}
```

#### Plugin Base Class

Inherit from `PluginBase` and override:

| Member | Type | Description |
|--------|------|-------------|
| `Name` | `string` | Plugin identifier |
| `Version` | `string` | Plugin version |
| `Description` | `string` | Short description |
| `GetCommands()` | `IEnumerable<Command>` | Return plugin commands |

#### Available Helper Methods

| Method | Description |
|--------|-------------|
| `Shell(cmd, timeoutMs?)` | Execute a shell command, return output (default timeout 30s) |
| `ReadFile(path)` | Read file contents |
| `WriteFile(path, content)` | Write to a file |
| `FileExists(path)` | Check if file exists |
| `ListDirectory(path)` | List directory contents |
| `GetWorkingDirectory()` | Get current working directory |
| `GetEnvironmentVariable(name)` | Get environment variable |
| `FormatSize(bytes)` | Format bytes as human-readable |

#### Session Object

The `session` parameter in command handlers provides:

| Property | Type | Description |
|----------|------|-------------|
| `Model` | `string` | Current model name |
| `TotalInputTokens` | `int` | Input token count |
| `TotalOutputTokens` | `int` | Output token count |
| `TotalCost` | `decimal` | Estimated cost in dollars |

---

## Context Compaction

When conversations grow too large, Pebbles compresses the history into a structured XML snapshot. This preserves the "learning trajectory" (what failed, what worked, why) rather than just the final state.

### Why It Matters

- **Prevents repeated failures** — The agent remembers what didn't work
- **Preserves constraints** — User preferences and requirements survive compaction
- **Maintains progress** — Partial work isn't lost when context fills
- **Reduces hallucination** — Exact file paths and error messages are preserved

### The XML Format

Compaction produces a `<state_snapshot>` with structured sections:

```xml
<state_snapshot>
    <overall_goal>Refactor auth service to use new JWT library</overall_goal>
    <constraints>
        - Must maintain backward compatibility
        - User prefers minimal comments
    </constraints>
    <key_knowledge>
        - Build: `dotnet build`
        - Tests: `dotnet test` (files end in `.Tests.cs`)
    </key_knowledge>
    <file_system_state>
        - MODIFIED: Services/AuthService.cs
        - CREATED: Services/JwtService.cs
    </file_system_state>
    <recent_actions>
        - Ran tests, 2 failures in AuthTests.cs
    </recent_actions>
    <failed_approaches>
        - Tried Newtonsoft.Json - conflicts with System.Text.Json
        - `dotnet test` failed: "Mock&lt;IUserService&gt; not setup"
    </failed_approaches>
    <avoid>
        - Do NOT modify LegacyAuth.cs (deprecated)
        - Do NOT use `dynamic` keyword (runtime errors)
    </avoid>
    <current_plan>
        1. [DONE] Identify deprecated API usage
        2. [IN PROGRESS] Refactor AuthService.cs
        3. [TODO] Fix failing tests
    </current_plan>
</state_snapshot>
```

### References

- [Effective Context Engineering for AI Agents](https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents) — Anthropic, 2025
- [Compaction vs Summarization](https://www.morphllm.com/compaction-vs-summarization) — Morph, 2024
- [Two Experiments on AI Agent Compaction](https://jxnl.co/writing/2025/08/30/context-engineering-compaction/) — Jason Liu, 2025
- [Context Engineering for Agents](https://blog.langchain.com/context-engineering-for-agents/) — LangChain, 2024
- [Characterizing Prompt Compression Methods](https://arxiv.org/html/2407.08892v1) — arXiv, 2024

---

## Roadmap

### Future Plugins

Planned capabilities for the plugin system:

- Additional AI providers (OpenAI, Anthropic, local models)
- Tool/function calling integration
- Hooks into the chat pipeline (on_message, on_response)
- Custom renderers and UI components

Stay tuned for updates.

---

## License

This project is licensed under the MIT License.

---

<p align="center">
  <strong>Made with ❤️ for developers who love the terminal</strong>
</p>
