# Running Book

Setup guide and troubleshooting reference for AI Script to Media.

---

## Project Structure

```
AIScriptToMediaDotNet/
├── src/
│   ├── AIScriptToMediaDotNet.Core/        # Shared kernel (interfaces, options)
│   ├── AIScriptToMediaDotNet.Providers/   # AI providers (Ollama)
│   ├── AIScriptToMediaDotNet.Agents/      # Agent implementations
│   ├── AIScriptToMediaDotNet.Services/    # External services (ComfyUI)
│   └── AIScriptToMediaDotNet.App/         # Entry point (Program.cs, appsettings.json)
│
├── tests/
│   ├── AIScriptToMediaDotNet.Core.Tests/
│   ├── AIScriptToMediaDotNet.Providers.Tests/
│   ├── AIScriptToMediaDotNet.Agents.Tests/
│   └── AIScriptToMediaDotNet.Integration.Tests/
│
└── docs/
```

See [ADR-004](adr/ADR-004-solution-structure.md) for the architecture decision.

---

## Prerequisites

### 1. .NET 10 SDK

**Download**: https://dotnet.microsoft.com/download

**Verify Installation**:
```bash
dotnet --version
# Should show 10.0.x or higher
```

---

### 2. Ollama (Local LLM Runtime)

**Download**: https://ollama.ai

**Installation** (Windows):
```powershell
# Download and run installer from ollama.ai
# Or use winget:
winget install Ollama.Ollama
```

**Start Ollama**:
```bash
ollama serve
# Runs on http://localhost:11434 by default
```

**Install Recommended Models**:
```bash
# General purpose model (for parsing, creation, verification agents)
# Configure model name in appsettings.json (Ollama:DefaultModel)
ollama pull lfm2.5-thinking

# Alternative (smaller, faster)
ollama pull mistral

# Alternative (larger, more capable)
ollama pull mixtral
```

**Verify Ollama**:
```bash
ollama list
# Should show installed models

ollama run lfm2.5-thinking "Hello, world!"
# Should return AI response (use model configured in appsettings.json)
```

---

### 3. ComfyUI (Image Generation)

**Download**: https://github.com/comfyanonymous/ComfyUI

**Installation** (Windows):
```powershell
# Option 1: Standalone (recommended)
# Download ComfyUI_windows_portable from releases
# Extract and run ComfyUI.exe

# Option 2: Manual installation
git clone https://github.com/comfyanonymous/ComfyUI
cd ComfyUI
pip install -r requirements.txt
```

**Start ComfyUI**:
```bash
# Default runs on http://localhost:8188
python main.py --listen
```

**Verify ComfyUI**:
- Open browser to http://localhost:8188
- Should see ComfyUI interface
- Try generating a test image

---

## Configuration

### appsettings.json

The `appsettings.json` file is located in `src/AIScriptToMediaDotNet.App/`:

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "DefaultModel": "lfm2.5-thinking",
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "RetryDelaySeconds": 2,
    "AgentModels": {
      "DefaultModel": "lfm2.5-thinking",
      "SceneParser": "lfm2.5-thinking",
      "SceneVerifier": "lfm2.5-thinking",
      "PhotoPromptCreator": "lfm2.5-thinking",
      "PhotoPromptVerifier": "lfm2.5-thinking",
      "VideoPromptCreator": "lfm2.5-thinking",
      "VideoPromptVerifier": "lfm2.5-thinking"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  }
}
```

> **Note**: Model names are configurable. Replace `lfm2.5-thinking` with your preferred model (e.g., `llama3.1`, `mistral`, `mixtral`).

### Environment Variables (Alternative)

```bash
OLLAMA_ENDPOINT=http://localhost:11434
COMFYUI_ENDPOINT=http://localhost:8188
MAX_RETRIES=3
OUTPUT_PATH=./output
```

---

## Output Structure

Each pipeline run creates a timestamped folder with detailed logs:

```
./output/{Title}_{YYYY-MM-DD_HH-mm-ss}/
├── script.md           # Original script
├── scenes.md           # Parsed scenes with metadata
├── agent-log.md        # Agent execution summary
├── execution-log.md    # Detailed execution log (on success)
└── error-{id}.md       # Error reference log (on failure)
```

### execution-log.md (Success)

Detailed execution log including:
- **Summary**: Execution ID, status, duration, total retries
- **Configuration**: Model settings, endpoints, retry limits
- **Input Script**: Full script with character count
- **Execution Timeline**: Chronological log of all agent events
  - Start events with input summaries
  - Complete events with output summaries and execution times
  - Retry events with feedback messages
  - Error events with full error details and stack traces
- **Statistics**: Success/failure counts, execution time metrics

### error-{id}.md (Failure)

Concise error reference created on pipeline failure:
- List of all errors with timestamps
- Error details and stack traces
- Input that caused each error
- Retry counts per stage
- Summary of total errors and failed stages

### Example execution-log.md

```markdown
# Pipeline Execution Log

**Execution ID:** f216b639
**Status:** ✅ Success
**Duration:** 9.77s

## Configuration
- **Ollama.Endpoint:** http://localhost:11434
- **Ollama.DefaultModel:** qwen2.5-coder:latest

## Execution Timeline

### 🎬 SceneParsing

#### ▶️ [02:33:55.505] Start
**Agent:** SceneParser

#### ✅ [02:34:05.250] Complete
**Agent:** SceneParser
**Execution Time:** 545ms

## Statistics
- **Successful Stages:** 2
- **Avg Execution Time:** 272ms
```

---

## Building and Running

### Build
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/AIScriptToMediaDotNet.Core
```

### Run Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/AIScriptToMediaDotNet.Core.Tests

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Run Application
```bash
# Run from solution root
dotnet run --project src/AIScriptToMediaDotNet.App -- --input script.txt --output ./output

# Or change to App directory first
cd src/AIScriptToMediaDotNet.App
dotnet run -- --input script.txt --output ./output

# Run with configuration
dotnet run --project src/AIScriptToMediaDotNet.App -- --input script.txt --config appsettings.json
```

---

## Debugging with Logs

### View Execution Log

After a successful run, check the detailed execution log:

```bash
# View execution log
cat output/{Title}_{timestamp}/execution-log.md

# Or on Windows
type output\{Title}_{timestamp}\execution-log.md
```

### View Error Log

After a failed run, check the error log:

```bash
# View error log
cat output/error-{executionId}.md

# Or on Windows
type output\error-{executionId}.md
```

### Enable Debug Logging

For more verbose output during development:

```bash
# Set environment variable
export DOTNET_LOGGING__CONSOLE__LOGLEVEL=Debug  # Linux/macOS
set DOTNET_LOGGING__CONSOLE__LOGLEVEL=Debug     # Windows

# Or modify appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Common Log Patterns

| Pattern | Meaning |
|---------|---------|
| `▶️ Start` | Agent/stage started |
| `✅ Complete` | Agent/stage completed successfully |
| `🔄 Retry` | Retry attempt due to validation failure |
| `❌ Error` | Agent/stage failed with error |
| `⏳ Running` | Stage in progress |

---

## Troubleshooting

### Ollama Connection Refused

**Error**: `Connection refused to http://localhost:11434`

**Solutions**:
1. Ensure Ollama is running: `ollama serve`
2. Check if port is in use: `netstat -ano | findstr 11434`
3. Verify endpoint in config matches Ollama's actual address

---

### Model Not Found

**Error**: `model '<model-name>' not found`

**Solution**:
```bash
# Check which model is configured in appsettings.json
# Then pull that model:
ollama pull lfm2.5-thinking
```

---

### ComfyUI Connection Issues

**Error**: `Cannot connect to ComfyUI at http://localhost:8188`

**Solutions**:
1. Ensure ComfyUI is running
2. Check if ComfyUI is listening on correct port
3. Verify no firewall blocking connection
4. Try accessing http://localhost:8188 in browser

---

### Out of Memory (GPU)

**Error**: CUDA out of memory during image generation

**Solutions**:
1. Reduce image resolution in ComfyUI settings
2. Close other GPU-intensive applications
3. Use smaller models in Ollama (configure in `appsettings.json`, e.g., `mistral` instead of larger models)
4. Set `--lowvram` flag for ComfyUI

---

### Slow Response Times

**Symptoms**: Agents taking >30 seconds to respond

**Solutions**:
1. Use smaller/faster models in Ollama
2. Ensure GPU acceleration is enabled for Ollama
3. Reduce concurrent agent calls (if implemented)
4. Check system resources (RAM, CPU, GPU)

---

### API Timeout

**Error**: `Request timeout after X ms`

**Solutions**:
1. Increase timeout in configuration
2. Check network connectivity
3. Ensure Ollama/ComfyUI aren't overloaded
4. Restart Ollama/ComfyUI services

---

## Development Tips

### Debugging AI Responses

Enable verbose logging to see raw AI responses:

```bash
dotnet run -- --input script.txt --verbose
```

### Testing Individual Agents

(Once implemented) Test agents in isolation:

```bash
# Test scene parser
dotnet run -- --test-agent SceneParser --input test-script.txt
```

### Viewing Generated Output

```bash
# Open output directory
start ./output

# View photo prompts
code ./output/photo-prompts.md

# View generated images
start ./output/images
```

---

## Performance Optimization

### Recommended Hardware

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| RAM | 16 GB | 32 GB |
| GPU | 8 GB VRAM | 12+ GB VRAM |
| Storage | SSD | NVMe SSD |
| CPU | 4 cores | 8+ cores |

### Model Selection Guide

| Use Case | Recommended Model |
|----------|-------------------|
| Fast iteration | `mistral`, `phi3` |
| Best quality | Configure in `appsettings.json` (e.g., `lfm2.5-thinking`, `mixtral`) |
| Balanced | Configure in `appsettings.json` (e.g., `lfm2.5-thinking`) |

---

## Quick Reference

| Service | Default URL | Purpose |
|---------|-------------|---------|
| Ollama | http://localhost:11434 | LLM inference |
| ComfyUI | http://localhost:8188 | Image generation |

| Command | Purpose |
|---------|---------|
| `ollama serve` | Start Ollama server |
| `ollama pull <model>` | Download a model |
| `ollama list` | List installed models |
| `dotnet build` | Build project |
| `dotnet run` | Run application |

---

## Getting Help

- Check existing issues in repository
- Review Ollama docs: https://ollama.ai/docs
- Review ComfyUI docs: https://github.com/comfyanonymous/ComfyUI#readme
- Check .NET docs: https://docs.microsoft.com/dotnet/
