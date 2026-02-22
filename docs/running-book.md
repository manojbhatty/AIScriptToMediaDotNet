# Running Book

Setup guide and troubleshooting reference for AI Script to Media.

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
ollama pull llama3.1

# Alternative (smaller, faster)
ollama pull mistral

# Alternative (larger, more capable)
ollama pull mixtral
```

**Verify Ollama**:
```bash
ollama list
# Should show installed models

ollama run llama3.1 "Hello, world!"
# Should return AI response
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

Create `appsettings.json` in project root:

```json
{
  "OllamaEndpoint": "http://localhost:11434",
  "ComfyUIEndpoint": "http://localhost:8188",
  "MaxRetries": 3,
  "OutputPath": "./output",
  "Models": {
    "SceneParser": "llama3.1",
    "SceneVerifier": "llama3.1",
    "PhotoPromptCreator": "llama3.1",
    "PhotoPromptVerifier": "llama3.1",
    "VideoPromptCreator": "llama3.1",
    "VideoPromptVerifier": "llama3.1"
  }
}
```

### Environment Variables (Alternative)

```bash
OLLAMA_ENDPOINT=http://localhost:11434
COMFYUI_ENDPOINT=http://localhost:8188
MAX_RETRIES=3
OUTPUT_PATH=./output
```

---

## Building and Running

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run -- --input script.txt --output ./output
```

### Run with Configuration
```bash
dotnet run -- --input script.txt --config appsettings.json
```

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

**Error**: `model 'llama3.1' not found`

**Solution**:
```bash
ollama pull llama3.1
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
3. Use smaller models in Ollama (e.g., `mistral` instead of `llama3.1`)
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
| Best quality | `llama3.1:70b`, `mixtral` |
| Balanced | `llama3.1:8b` |

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
