# Plugin Development Guide

Pebbles supports two types of plugins: **Command Plugins** and **Tool Plugins**. Both are compiled at runtime using Roslyn.

## Plugin Locations

- **Global:** `~/.pebbles/agent/plugins/scripts/`
- **Project:** `./.pebbles/agent/plugins/scripts/`

## Command Plugins

Command plugins add new slash commands to Pebbles.

### Creating a Command Plugin

1. Create a `.cs` file in the plugins directory
2. Inherit from `PluginBase`
3. Override required properties and methods

```csharp
using Pebbles.Plugins;

public class GitPlugin : PluginBase
{
    public override string Name => "git-plugin";
    public override string Version => "1.0.0";
    public override string Description => "Git commands for version control";

    public override IEnumerable<Command> GetCommands()
    {
        yield return new Command
        {
            Name = "/git",
            Description = "Execute git commands",
            Usage = "/git <command>",
            Handler = (args, session) =>
            {
                var gitArgs = string.Join(" ", args);
                return Shell($"git {gitArgs}");
            }
        };

        yield return new Command
        {
            Name = "/status",
            Description = "Show git status",
            Handler = (args, session) => Shell("git status")
        };
    }
}
```

### PluginBase Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Plugin identifier |
| `Version` | `string` | Plugin version (SemVer) |
| `Description` | `string` | Short description |

### PluginBase Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetCommands()` | `IEnumerable<Command>` | Return plugin commands |

### Helper Methods

| Method | Description |
|--------|-------------|
| `Shell(cmd, timeoutMs?)` | Execute shell command (default 30s timeout) |
| `ReadFile(path)` | Read file contents |
| `WriteFile(path, content)` | Write to file |
| `FileExists(path)` | Check if file exists |
| `ListDirectory(path)` | List directory contents |
| `GetWorkingDirectory()` | Get current directory |
| `GetEnvironmentVariable(name)` | Get env variable |
| `FormatSize(bytes)` | Format bytes as human-readable |

### Command Object

```csharp
public class Command
{
    public string Name { get; set; }      // e.g., "/mycmd"
    public string Description { get; set; }
    public string Usage { get; set; }     // e.g., "/mycmd [args]"
    public Func<string[], Session, string> Handler { get; set; }
}
```

## Tool Plugins

Tool plugins add new tools that the AI can call autonomously.

### Creating a Tool Plugin

1. Create a `.cs` file in the plugins directory
2. Inherit from `ToolPluginBase`
3. Implement required properties and methods

```csharp
using Pebbles.Plugins;
using Pebbles.Models;

public class WeatherTool : ToolPluginBase
{
    public override string Name => "weather";
    public override string Version => "1.0.0";
    public override string Description => "Get weather information for a location";

    public override ToolDefinition GetDefinition()
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = "get_weather",
                Description = "Get current weather for a location",
                Parameters = new ToolParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolParameterProperty>
                    {
                        ["location"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "City name or ZIP code"
                        }
                    },
                    Required = ["location"]
                }
            }
        };
    }

    public override async Task<ToolExecutionResult> ExecuteAsync(
        string arguments, 
        CancellationToken cancellationToken)
    {
        var args = DeserializeArgs<WeatherArgs>(arguments);
        
        // Call weather API
        var weather = await FetchWeather(args.Location);
        
        return new ToolExecutionResult
        {
            Success = true,
            Content = $"Weather in {args.Location}: {weather}"
        };
    }

    private record WeatherArgs
    {
        public string Location { get; init; } = string.Empty;
    }
}
```

### ToolPluginBase Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Tool identifier |
| `Version` | `string` | Tool version |
| `Description` | `string` | Tool description |

### ToolPluginBase Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetDefinition()` | `ToolDefinition` | Tool schema for AI |
| `ExecuteAsync()` | `Task<ToolExecutionResult>` | Execute tool |

### Helper Methods

Same as Command Plugins, plus:
- `DeserializeArgs<T>(json)` - Deserialize tool arguments

## Testing Plugins

### Local Testing

1. Create plugin file in project plugins directory
2. Run Pebbles
3. Use `/reload` to load plugin
4. Test with `/plugins` to verify

### Debugging

Add console output for debugging:

```csharp
public override IEnumerable<Command> GetCommands()
{
    Console.WriteLine($"[DEBUG] Loading {Name} plugin...");
    
    yield return new Command { /* ... */ };
}
```

## Best Practices

### Naming
- Use lowercase with hyphens: `my-plugin`, `weather-tool`
- Prefix commands with `/`: `/mycmd`
- Prefix tools with action verbs: `get_weather`, `search_files`

### Error Handling
```csharp
try
{
    var result = Shell("git status");
    return result;
}
catch (Exception ex)
{
    return $"Error: {ex.Message}";
}
```

### Security
- Validate all user input
- Don't execute arbitrary code
- Use timeouts for shell commands
- Don't expose sensitive data

### Performance
- Cache expensive operations
- Use async/await for I/O
- Keep plugin initialization fast

## Plugin Lifecycle

1. **Discovery** - Pebbles scans plugin directories
2. **Compilation** - Roslyn compiles `.cs` files
3. **Loading** - Plugins loaded into `AssemblyLoadContext`
4. **Execution** - Commands/tools executed on demand
5. **Reload** - `/reload` recompiles and reloads all plugins

## Distribution

### Sharing Plugins

1. Create GitHub repository
2. Include `.cs` plugin files
3. Document installation:
   ```bash
   # Copy to global plugins
   cp MyPlugin.cs ~/.pebbles/agent/plugins/scripts/
   
   # Or clone to project
   git clone <repo> ./.pebbles/agent/plugins/scripts/
   ```

### Plugin Template

```csharp
// MyPlugin.cs
// Version: 1.0.0
// Author: Your Name
// Description: What the plugin does

using Pebbles.Plugins;

public class MyPlugin : PluginBase
{
    public override string Name => "my-plugin";
    public override string Version => "1.0.0";
    public override string Description => "Description here";

    public override IEnumerable<Command> GetCommands()
    {
        // Your commands here
    }
}
```

## Troubleshooting

### Plugin Not Loading

1. Check file is in correct directory
2. Check for compilation errors in `/reload` output
3. Ensure class is `public`
4. Ensure inherits from correct base class

### Command Not Working

1. Verify command name starts with `/`
2. Check `/plugins` output for errors
3. Test handler logic with simple return first

### Tool Not Called by AI

1. Verify `GetDefinition()` returns valid schema
2. Check tool name is descriptive
3. Ensure description is clear
4. Test tool manually first

## Examples

See example plugins in the Pebbles repository or community plugins at:
- GitHub: [link to community plugins when available]

## Questions?

- Open an issue for bugs
- Use Discussions for questions
- Check existing plugins for examples
