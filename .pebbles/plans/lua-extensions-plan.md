# Lua Extension System Implementation Plan

## Overview

Add a plugin system using Lua scripts (MoonSharp) that allows users to extend Pebbles with custom commands and hooks. Extensions are loaded from `~/.pebbles/agent/extensions/` (global) and `./.pebbles/agent/extensions/` (project-local).

---

## Phase 1: Core Infrastructure

### 1.1 Add MoonSharp Dependency
- Add `MoonSharp` NuGet package to `Pebbles.csproj`
- MoonSharp is 100% managed C#, AOT-compatible

### 1.2 Create Extension Models
Create `Models/ExtensionModels.cs`:
- `LuaExtension` — metadata (name, version, description)
- `ExtensionCommand` — command definition from Lua
- `ExtensionHook` — hook callbacks (on_message, on_start, etc.)

### 1.3 Create Lua Runtime Service
Create `Services/LuaExtensionService.cs`:
- Initialize MoonSharp `Script` instance
- Register global functions exposed to Lua:
  - `shell(cmd, timeout?)` — execute shell commands
  - `read_file(path)` — read file contents
  - `write_file(path, content)` — write to file
  - `print(msg)` — output with Spectre markup
  - `add_context(text)` — add to AI context
  - `get_cwd()` — get current working directory
- Expose `session` object (read-only for now):
  - `session.model`
  - `session.total_input_tokens`
  - `session.total_output_tokens`
  - `session.total_cost`

### 1.4 Extension Discovery
Create `Services/ExtensionLoader.cs`:
- Scan `~/.pebbles/agent/extensions/scripts/*.lua`
- Scan `./.pebbles/agent/extensions/scripts/*.lua`
- Load and parse each `.lua` file
- Extract `extension`, `commands`, `hooks` tables
- Handle errors gracefully (bad script = skip + warn)

### 1.5 Update CommandHandler
Modify `Services/CommandHandler.cs`:
- Inject `ExtensionLoader`
- Merge extension commands with built-in commands
- Extension commands use same `SlashCommand` model
- Handle `/reload` command

---

## Phase 2: Commands

### 2.1 Lua Command Structure
```lua
extension = {
    name = "git-tools",
    version = "1.0.0",
    description = "Git workflow commands"
}

commands = {
    {
        name = "/git",
        description = "Run git commands",
        usage = "/git <args>",
        handler = function(args, session)
            local result = shell("git " .. table.concat(args, " "))
            return result
        end
    }
}
```

### 2.2 Handler Bridge
- Create `Func<string[], ChatSession, Task<CommandResult>>` wrapper
- Convert Lua return value to `CommandResult`
- Handle Lua errors with try/catch, show friendly message

### 2.3 Reload Command
Add `/reload` command:
- Clear loaded extensions
- Re-scan extension directories
- Reload all `.lua` files
- Report loaded extensions: "Loaded 2 extensions: git-tools, sql-helper"

---

## Phase 3: Hooks (Optional, Phase 2)

Hooks allow extensions to intercept the chat pipeline:

```lua
hooks = {
    on_start = function()
        print("[dim]Extension loaded![/]")
    end,
    
    on_before_send = function(message, session)
        -- Modify message before sending to AI
        return message
    end,
    
    on_after_receive = function(response, session)
        -- Process response after receiving
        return response
    end
}
```

Hook types:
- `on_start` — extension loaded
- `on_before_send` — before sending to AI
- `on_after_receive` — after receiving response
- `on_command` — before command execution

---

## File Structure

```
Services/
├── IExtensionLoader.cs          # Interface for extension loading
├── ExtensionLoader.cs           # Discovers and loads extensions
├── LuaExtensionService.cs       # MoonSharp runtime, global functions
└── CommandHandler.cs            # Modified to include extension commands

Models/
├── ExtensionModels.cs           # Extension, Command, Hook models
└── CommandModels.cs             # Existing (add extension support)

Configuration/
└── PebblesOptions.cs            # Add ExtensionsPath option
```

---

## Example Extensions

### Simple Git Commands
```lua
-- ~/.pebbles/agent/extensions/scripts/git.lua

extension = {
    name = "git",
    version = "1.0.0"
}

commands = {
    {
        name = "/git",
        description = "Run git commands",
        handler = function(args)
            return shell("git " .. table.concat(args, " "))
        end
    },
    {
        name = "/branch",
        description = "Show current branch",
        handler = function(args)
            local branch = shell("git rev-parse --abbrev-ref HEAD")
            return "Current branch: " .. branch
        end
    },
    {
        name = "/staged",
        description = "Show staged files",
        handler = function(args)
            return shell("git diff --cached --stat")
        end
    }
}
```

### DotNet Commands
```lua
-- ~/.pebbles/agent/extensions/scripts/dotnet.lua

extension = {
    name = "dotnet",
    version = "1.0.0"
}

commands = {
    {
        name = "/build",
        description = "Build the project",
        handler = function(args)
            return shell("dotnet build", 60000)
        end
    },
    {
        name = "/test",
        description = "Run tests",
        handler = function(args)
            return shell("dotnet test", 120000)
        end
    },
    {
        name = "/run",
        description = "Run the project",
        handler = function(args)
            return shell("dotnet run", 30000)
        end
    }
}
```

---

## Implementation Order

1. **Add MoonSharp package**
2. **Create models** (`ExtensionModels.cs`)
3. **Create `LuaExtensionService`** with global functions
4. **Create `ExtensionLoader`** for discovery
5. **Modify `CommandHandler`** to merge commands
6. **Add `/reload` command**
7. **Test with sample extensions**
8. **Add hooks support** (Phase 2)

---

## Questions/Decisions

1. **Should we also support JSON commands?** (simpler for non-programmers)
2. **Error handling:** Show Lua errors inline or in a separate panel?
3. **Session mutation:** Allow Lua to change `session.model` or just read?
4. **Async shell commands:** MoonSharp doesn't support async natively. Need to wrap in `Task.Run()`.