# Pebbles

A terminal-based AI coding assistant built with .NET 10 and Spectre.Console.

Your AI coding assistant in the terminal — fast, focused, and extensible.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-blue)
![Tests](https://img.shields.io/github/actions/workflow/status/yourusername/pebbles/ci.yml?label=tests)
![Coverage](https://img.shields.io/codecov/c/github/yourusername/pebbles)


## Features

### 🚀 Streaming AI Chat

Real-time streaming responses from AI models. Watch the answer unfold as it's generated — no waiting for complete responses.

### 🤖 Multiple AI Providers

Choose from multiple AI providers:

- **Alibaba Cloud**
- **OpenAI**
- **Anthropic**

### 🛠️ Built-in Tools

Pebbles includes 5 built-in tools for autonomous task execution:

| Tool | Command | Description |
|------|---------|-------------|
| **Read File** | `read_file` | Read file contents |
| **Write File** | `write_file` | Create/modify files |
| **Shell** | `run_command` | Execute shell commands |
| **List Directory** | `list_directory` | Browse folders |
| **Search Files** | `search_files` | Search text patterns |

### 💾 Session Persistence

Your conversations are automatically saved and restored:

- Auto-save after each message
- Auto-load previous session on startup
- Multiple sessions supported
- Commands: `/save`, `/load`, `/sessions`, `/delete`

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

Full control over your session with 22 built-in commands:

| Command | Description |
|---------|-------------|
| `/help` | Show available commands |
| `/clear` | Clear chat history |
| `/model` | Switch AI model |
| `/provider` | Show current AI provider |
| `/history` | Show conversation history |
| `/cost` | Show token usage and cost |
| `/context` | Show project context |
| `/read <path>` | Read a file |
| `/files` | List loaded files |
| `/clearfiles` | Clear loaded files |
| `/compress` | Compress conversation |
| `/autocompress` | Toggle auto-compression |
| `/remember <text>` | Save to memory |
| `/memory` | View memories |
| `/save` | Save current session |
| `/load <id>` | Load session |
| `/sessions` | List all sessions |
| `/delete <id>` | Delete session |
| `/reload` | Reload plugins |
| `/plugins` | List plugins |
| `/tools` | List available tools |
| `/exit` | Exit Pebbles |

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
dotnet run --project src/Pebbles/Pebbles.csproj
```

### Configuration

Create `appsettings.json` in the application directory:

```json
{
  "Pebbles": {
    "Provider": "alibabacloud",
    "DefaultModel": "qwen3.5-plus",
    "AlibabaCloudApiKey": "your-api-key-here"
  }
}
```

### API Key

On first run, Pebbles will prompt you to select a provider and enter your API key. The key is stored as an environment variable for the session.

**Alibaba Cloud Coding Plan:** Use your Coding Plan-specific API key (format: `sk-sp-xxxxx`) from the [Coding Plan page](https://www.alibabacloud.com/help/en/model-studio/coding-plan).

Alternatively, set your API key via environment variable before starting:

```bash
# Alibaba Cloud Coding Plan (use sk-sp-xxxxx format key)
export ALIBABA_CLOUD_API_KEY=sk-sp-your-key-here

# OpenAI
export OPENAI_API_KEY=your-api-key-here

# Anthropic
export ANTHROPIC_API_KEY=your-api-key-here
```

For persistence, add these to your shell profile (e.g., `~/.bashrc`, `~/.zshrc`).

---

## Usage

### Quick Start

```bash
dotnet run --project src/Pebbles/Pebbles.csproj
```

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

### Session Management

```
❯ You: /save
✓ Session saved
  ID: 7a4f55d5
  Messages: 24

❯ You: /sessions
Saved Sessions
  ● 7a4f55d5
  3b2c88e1

❯ You: /load 3b2c88e1
✓ Session loaded
  ID: 3b2c88e1
  Messages: 18
```

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
│  ┌──────────────┐  ┌───────────────────────────────────┐   │
│  │ InputHandler │→ │  CompositeCommandHandler          │   │
│  │ (User Input) │  │  ├─ ChatCommands (static)         │   │
│  └──────────────┘  │  ├─ FileCommands                  │   │
│         │          │  ├─ SessionCommands               │   │
│         │          │  ├─ CompressionCommands           │   │
│         │          │  ├─ MemoryCommands                │   │
│         │          │  └─ PluginCommands                │   │
│         │          └───────────────────────────────────┘   │
│         │                      │                            │
│         │                      ▼                            │
│         │          ┌───────────────────────┐                │
│         └─────────→│    IAIProvider        │                │
│                    │    (Streaming)        │                │
│                    └───────────────────────┘                │
│                           │                                 │
│              ┌────────────┼────────────┐                    │
│              ▼            ▼            ▼                    │
│        ┌──────────┐ ┌───────────┐ ┌──────────┐             │
│        │ToolExec  │ │FileService│ │Context   │             │
│        │Service   │ │(@files)   │ │Manager   │             │
│        └──────────┘ └───────────┘ └──────────┘             │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility |
|-----------|---------------|
| `ChatService` | Main application loop |
| `IAIProvider` | AI provider abstraction (Alibaba Cloud, OpenAI, Anthropic) |
| `CompositeCommandHandler` | Aggregates specialized command handlers |
| `ChatRenderer` | UI rendering with Spectre.Console |
| `InputHandler` | User input processing |
| `FileService` | File loading and @reference parsing |
| `ContextManager` | Project/global context management |
| `ToolRegistry` | Tool management and execution |
| `SessionStore` | Session persistence |

### Design Patterns

- **Dependency Injection** — All services registered via `IServiceCollection`
- **Provider Pattern** — `IAIProvider` interface for swappable AI backends
- **Options Pattern** — Configuration via `PebblesOptions` class with validation
- **Command Pattern** — Composite pattern for slash command handling
- **Registry Pattern** — `ToolRegistry` for tool management
- **Streaming** — `IAsyncEnumerable<T>` for real-time token streaming

---

## Testing

### Test Projects

| Project | Purpose | Tests |
|---------|---------|-------|
| `Pebbles.Tests` | Unit tests | 42 tests |
| `Pebbles.IntegrationTests` | Integration tests | 6 tests |
| `Pebbles.E2ETests` | End-to-end tests | 6 tests |
| `Pebbles.Benchmarks` | Performance benchmarks | 8 benchmarks |

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific project
dotnet test tests/Pebbles.Tests/Pebbles.Tests.csproj
```

### Coverage Target

- **Target:** 80%+ code coverage
- **Current:** 85%+ coverage

---

## CI/CD

### GitHub Actions

- **CI Pipeline:** Builds and tests on every push and PR
- **Release Pipeline:** Creates Native AOT releases on git tags

### Build Artifacts

- Windows (win-x64)
- Linux (linux-x64)
- macOS (osx-x64)

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.

### Quick Start

```bash
git clone https://github.com/yourusername/pebbles.git
cd pebbles
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Pebbles/Pebbles.csproj
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/architecture.md) | System architecture and design |
| [Plugins](docs/plugins.md) | Plugin development guide |
| [Configuration](docs/configuration.md) | Configuration options |
| [Contributing](CONTRIBUTING.md) | Contribution guidelines |

---

## Roadmap

**Status:** ✅ 100% Feature Complete

All planned features have been implemented:
- ✅ CI/CD pipeline
- ✅ Integration tests
- ✅ E2E tests
- ✅ Session persistence
- ✅ Multi-provider support (Alibaba Cloud, OpenAI, Anthropic)
- ✅ Performance benchmarks
- ✅ Complete documentation

Future enhancements will be based on community feedback.

---

## License

MIT License - see LICENSE file for details.

---

<p align="center">
  <strong>Made with ❤️ for developers who love the terminal</strong>
</p>
