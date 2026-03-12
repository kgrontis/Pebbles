# Configuration Guide

Pebbles is configured via `appsettings.json` and environment variables.

## appsettings.json

Location: `src/Pebbles/appsettings.json` (copied to output directory)

### Basic Configuration

```json
{
  "Pebbles": {
    "Provider": "alibabacloud",
    "DefaultModel": "qwen3.5-plus",
    "AlibabaCloudApiKey": "your-api-key-here"
  }
}
```

### All Options

```json
{
  "Pebbles": {
    "// Provider": "alibabacloud | openai | anthropic | mock",
    "Provider": "alibabacloud",

    "// Default AI model": "See Available Models below",
    "DefaultModel": "qwen3.5-plus",

    "// Alibaba Cloud": "",
    "AlibabaCloudApiKey": "your-api-key",
    "AlibabaCloudBaseUrl": "https://coding-intl.dashscope.aliyuncs.com/v1",

    "// OpenAI": "",
    "OpenAiApiKey": "sk-...",
    "OpenAiBaseUrl": "https://api.openai.com/v1",

    "// Anthropic": "",
    "AnthropicApiKey": "sk-ant-...",
    "AnthropicBaseUrl": "https://api.anthropic.com",

    "// Token pricing (for cost estimation)": "",
    "InputCostPer1K": 0.0004,
    "OutputCostPer1K": 0.0024,

    "// Token estimation multiplier": "",
    "TokenEstimationMultiplier": 1.3,

    "// Auto-compression": "",
    "AutoCompressionEnabled": true,
    "CompressionThreshold": 0.7,
    "KeepRecentMessages": 6
  }
}
```

## Environment Variables

Preferred for API keys (more secure):

| Variable | Description |
|----------|-------------|
| `ALIBABA_CLOUD_API_KEY` | Alibaba Cloud Coding Plan API key (format: `sk-sp-xxxxx`) |
| `OPENAI_API_KEY` | OpenAI API key |
| `ANTHROPIC_API_KEY` | Anthropic API key |

> **Note:** Alibaba Cloud Coding Plan requires a dedicated API key (format: `sk-sp-xxxxx`) from the [Coding Plan page](https://www.alibabacloud.com/help/en/model-studio/coding-plan). The general Model Studio API key (format: `sk-xxxxx`) will not work.

### Setting Environment Variables

**Windows (PowerShell):**
```powershell
$env:ALIBABA_CLOUD_API_KEY="sk-sp-your-key-here"
```

**Windows (CMD):**
```cmd
set ALIBABA_CLOUD_API_KEY=sk-sp-your-key-here
```

**Linux/macOS:**
```bash
export ALIBABA_CLOUD_API_KEY=sk-sp-your-key-here
```

**Permanent (Windows):**
```powershell
[System.Environment]::SetEnvironmentVariable(
    "ALIBABA_CLOUD_API_KEY",
    "sk-sp-your-key-here",
    "User")
```

## Provider Configuration

### Alibaba Cloud Coding Plan (Default)

```json
{
  "Pebbles": {
    "Provider": "alibabacloud",
    "DefaultModel": "qwen3.5-plus",
    "AlibabaCloudApiKey": "sk-sp-your-key"
  }
}
```

**Available Models:**
- `qwen3.5-plus` - General purpose (default)
- `qwen3-coder-plus` - Code generation
- `qwen3-coder-next` - Latest coding model
- `qwen3-max-2026-01-23` - Most capable
- `glm-4.7` - Alternative model
- `glm-5` - Latest GLM
- `MiniMax-M2.5` - Long context
- `kimi-k2.5` - Extended conversations

### OpenAI

```json
{
  "Pebbles": {
    "Provider": "openai",
    "DefaultModel": "gpt-5.3",
    "OpenAiApiKey": "sk-..."
  }
}
```

### Anthropic

```json
{
  "Pebbles": {
    "Provider": "anthropic",
    "DefaultModel": "claude-4-5-sonnet",
    "AnthropicApiKey": "sk-ant-..."
  }
}
```

### Mock (Testing)

```json
{
  "Pebbles": {
    "Provider": "mock"
  }
}
```

Returns predefined responses for testing.

## Context Compression

Automatically compresses long conversations to save tokens.

| Option | Default | Description |
|--------|---------|-------------|
| `AutoCompressionEnabled` | `true` | Enable auto-compression |
| `CompressionThreshold` | `0.7` | Compress at 70% context window |
| `KeepRecentMessages` | `6` | Keep last 6 messages verbatim |

**Example:**
```json
{
  "Pebbles": {
    "AutoCompressionEnabled": true,
    "CompressionThreshold": 0.8,
    "KeepRecentMessages": 10
  }
}
```

## Token Pricing

Configure for accurate cost estimation:

```json
{
  "Pebbles": {
    "InputCostPer1K": 0.0004,
    "OutputCostPer1K": 0.0024,
    "TokenEstimationMultiplier": 1.3
  }
}
```

**Current pricing (Alibaba Cloud Qwen):**
- Input: $0.0004 per 1K tokens
- Output: $0.0024 per 1K tokens

**Update for your model:**
- GPT-4: Input $0.03, Output $0.06
- Claude 3.5 Sonnet: Input $0.003, Output $0.015

## Session Persistence

Sessions are auto-saved to: `~/.pebbles/sessions/`

No configuration needed - enabled by default.

## Plugin Directories

Plugins are loaded from:
- Global: `~/.pebbles/agent/plugins/scripts/`
- Project: `./.pebbles/agent/plugins/scripts/`

No configuration needed.

## Validation

Invalid configuration will fail at startup:

```
ValidateOptionsResult.Fail("DefaultModel is required.")
ValidateOptionsResult.Fail("CompressionThreshold must be between 0 and 1.")
```

## Examples

### Development Setup

```json
{
  "Pebbles": {
    "Provider": "mock",
    "DefaultModel": "test-model",
    "AutoCompressionEnabled": false
  }
}
```

### Production Setup

```json
{
  "Pebbles": {
    "Provider": "alibabacloud",
    "DefaultModel": "qwen3.5-plus",
    "AutoCompressionEnabled": true,
    "CompressionThreshold": 0.7,
    "KeepRecentMessages": 6
  }
}
```

### Multi-Provider Setup

Use environment variables to switch providers:

```json
{
  "Pebbles": {
    "Provider": "${PROVIDER:alibabacloud}",
    "DefaultModel": "qwen3.5-plus"
  }
}
```

Then set:
```bash
export PROVIDER=openai
```

## Troubleshooting

### API Key Not Found

```
InvalidOperationException: API key not configured
```

**Solution:** Set environment variable or add to appsettings.json

### Invalid Model

```
Error: Unknown model: invalid-model
```

**Solution:** Use a model from Available Models list

### Compression Not Working

```
Auto-compression skipped: Not enough messages
```

**Solution:** Increase conversation length or lower `KeepRecentMessages`

## Configuration Files

| File | Purpose |
|------|---------|
| `appsettings.json` | Main configuration |
| `appsettings.Development.json` | Development overrides |
| `appsettings.Production.json` | Production settings |

## Reloading Configuration

Configuration is loaded at startup only. To change:
1. Edit `appsettings.json`
2. Restart Pebbles

## Next Steps

- See [Architecture](architecture.md) for system design
- See [Plugins](plugins.md) for extending Pebbles
- See [README](../README.md)