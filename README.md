# Pebbles

A terminal-based AI coding assistant built with .NET 10 and Spectre.Console.

Your AI coding assistant in the terminal — fast, focused, and extensible.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![AOT](https://img.shields.io/badge/Native_AOT-Enabled-green)
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

### 💬 Slash Commands

Full control over your session with 13 built-in commands:

| Command        | Description                          |
| -------------- | ------------------------------------ |
| `/help`        | Show available commands              |
| `/clear`       | Clear chat history                   |
| `/model`       | Switch AI model (interactive picker) |
| `/compact`     | Toggle compact mode (hide thinking)  |
| `/history`     | Show conversation history summary    |
| `/cost`        | Show token usage and estimated cost  |
| `/context`     | Show loaded project context          |
| `/read <path>` | Read a file into context             |
| `/files`       | List loaded files in context         |
| `/clearfiles`  | Clear all loaded files from context  |
| `/reload`      | Reload extensions                    |
| `/extensions`  | List loaded extensions and commands  |
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

### ⚡ Native AOT

Compiled to native code for:

- Fast startup time
- Self-contained executable (no runtime required)
- Minimal memory footprint
- Optimized for speed with `OptimizationPreference=Speed`

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

# Or publish as native AOT
dotnet publish -c Release -r win-x64
./bin/Release/net10.0/win-x64/publish/Pebbles.exe
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

### 🔌 Extension System

Pebbles supports Lua extensions for adding custom commands. Extensions are loaded from:

- **Global:** `~/.pebbles/agent/extensions/scripts/`
- **Project:** `./.pebbles/agent/extensions/scripts/`

#### Extension Commands

| Command        | Description                          |
| -------------- | ------------------------------------ |
| `/reload`      | Reload all extensions                |
| `/extensions`  | List loaded extensions and commands  |

#### Creating an Extension

Create a `.lua` file in the extensions directory:

```lua
-- ~/.pebbles/agent/extensions/scripts/my-tools.lua

extension = {
    name = "my-tools",
    version = "1.0.0",
    description = "My custom commands"
}

commands = {
    {
        name = "/mycmd",
        description = "My custom command",
        usage = "/mycmd [args]",
        handler = function(args, session)
            return "Hello from my extension! Args: " .. table.concat(args, " ")
        end
    },
    {
        name = "/git",
        description = "Run git commands",
        handler = function(args, session)
            return shell("git " .. table.concat(args, " "))
        end
    }
}
```

#### Available Lua Functions

| Function | Description |
|----------|-------------|
| `shell(cmd, timeout?)` | Execute a shell command, return output |
| `read_file(path)` | Read file contents |
| `write_file(path, content)` | Write to a file |
| `file_exists(path)` | Check if file exists |
| `list_dir(path)` | List directory contents |
| `get_cwd()` | Get current working directory |
| `env(name)` | Get environment variable |
| `format_size(bytes)` | Format bytes as human-readable |

#### Session Object

The `session` parameter in command handlers provides:

- `session.model` — Current model name
- `session.total_input_tokens` — Input token count
- `session.total_output_tokens` — Output token count
- `session.total_cost` — Estimated cost in dollars

---

## Roadmap

### Future Extensions

Planned capabilities for the extension system:

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
