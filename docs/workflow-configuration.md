# Workflow Configuration Guide

## Overview

The ComfyUI workflow builder is now **workflow-agnostic**. You can switch between different ComfyUI workflow JSON files without modifying any C# code.

## How It Works

The system uses **dynamic node discovery** to find the relevant nodes in any ComfyUI workflow:

1. **Auto-Discovery Mode** (default): Automatically finds nodes by their class types
2. **Explicit Mapping Mode**: Manually configure which node IDs to use

## Quick Start

### Option 1: Command Line

```bash
# Use a custom workflow
dotnet run --title "My Script" --input script.txt --generate-images --workflow ./ComfyUiWorkflows/ComfyUI_SDXL_Image_Generation.json
```

### Option 2: Configuration File

Edit `appsettings.json`:

```json
{
  "ComfyUI": {
    "Endpoint": "http://localhost:8188",
    "WorkflowPath": "ComfyUiWorkflows/ComfyUI_SDXL_Image_Generation.json"
  }
}
```

### Option 3: Environment Variable

```bash
set ComfyUI__WorkflowPath=ComfyUiWorkflows/ComfyUI_SDXL_Image_Generation.json
```

## Supported Workflow Types

The system works with any ComfyUI workflow that contains:

- **CLIPTextEncode** nodes for prompts (positive and negative)
- **KSampler** or **KSamplerAdvanced** for generation
- **SaveImage** or **PreviewImage** for output

### Tested Workflows

- ✅ SD1.5 (basic KSampler)
- ✅ SDXL (KSamplerAdvanced with refiner)
- ✅ SD3 (EmptySD3LatentImage)

## Auto-Discovery Behavior

When no explicit node mapping is provided, the system automatically:

1. **Positive Prompt**: First `CLIPTextEncode` node found
2. **Negative Prompt**: Second `CLIPTextEncode` node found
3. **Sampler**: First `KSampler` or `KSamplerAdvanced` node found
4. **Save Image**: First `SaveImage` or `PreviewImage` node found
5. **Seed**: Updates `seed` or `noise_seed` field in the sampler node

## Advanced: Explicit Node Mapping

For complex workflows or precise control, configure node mappings in `appsettings.json`:

```json
{
  "ComfyUI": {
    "Endpoint": "http://localhost:8188",
    "WorkflowPath": "ComfyUiWorkflows/MyCustomWorkflow.json",
    "NodeMapping": {
      "PositivePromptNodeId": "6",
      "NegativePromptNodeId": "7",
      "SamplerNodeId": "10",
      "SaveImageNodeId": "19",
      "LatentImageNodeId": "5",
      "ClassTypes": {
        "TextEncoder": ["CLIPTextEncode"],
        "Sampler": ["KSampler", "KSamplerAdvanced"],
        "SaveImage": ["SaveImage", "PreviewImage"],
        "LatentImage": ["EmptyLatentImage", "EmptySD3LatentImage"]
      }
    }
  }
}
```

## Creating Custom Workflows

To create a workflow that works with the system:

1. **Export from ComfyUI**: Save your workflow as API format (JSON)
2. **Ensure Required Nodes**:
   - At least one `CLIPTextEncode` for positive prompt
   - Optionally a second `CLIPTextEncode` for negative prompt
   - One `KSampler` or `KSamplerAdvanced` for generation
   - One `SaveImage` or `PreviewImage` for output
3. **Place in Workflow Folder**: Copy to `ComfyUiWorkflows/` directory
4. **Configure**: Use command line or appsettings to specify the workflow

## Example: Switching Workflows

```bash
# Use SDXL workflow
dotnet run --title "My Script" -i script.txt -g -w ComfyUiWorkflows/ComfyUI_SDXL_Image_Generation.json

# Use SD1.5 workflow
dotnet run --title "My Script" -i script.txt -g -w ComfyUiWorkflows/ComfyUIWorkflow.json

# Use custom workflow
dotnet run --title "My Script" -i script.txt -g -w ./my-custom-workflow.json
```

## Troubleshooting

### "No positive prompt node found"
Your workflow may not have a `CLIPTextEncode` node. Add one or configure explicit node mapping.

### "Node X not found in workflow"
The node ID in your mapping doesn't exist. Use auto-discovery (remove NodeMapping config) or verify the node IDs.

### Generation uses wrong seed
Ensure your sampler node has a `seed` or `noise_seed` field. The system updates whichever exists.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                 ComfyUIWorkflowBuilder                   │
├─────────────────────────────────────────────────────────┤
│  IWorkflowTemplateProvider (loads workflow JSON)        │
│  ├─ FileWorkflowTemplateProvider (file-based)           │
│  └─ InMemoryWorkflowTemplateProvider (testing)          │
│                                                         │
│  WorkflowNodeMapping (optional configuration)           │
│  ├─ PositivePromptNodeId                                │
│  ├─ NegativePromptNodeId                                │
│  ├─ SamplerNodeId                                       │
│  ├─ SaveImageNodeId                                     │
│  └─ ClassTypes (for auto-discovery)                    │
└─────────────────────────────────────────────────────────┘
```

## Code Example: Custom Workflow Provider

```csharp
// Register a custom workflow provider (e.g., from database)
services.AddComfyUI(
    configureOptions: cfg => cfg.Endpoint = "http://localhost:8188",
    templateProviderFactory: sp =>
    {
        var workflowJson = GetWorkflowFromDatabase();
        var workflow = JsonNode.Parse(workflowJson);
        return new InMemoryWorkflowTemplateProvider(
            workflow,
            "Database Workflow",
            new WorkflowNodeMapping
            {
                PositivePromptNodeId = "6",
                NegativePromptNodeId = "7"
            });
    });
```

## See Also

- [ComfyUI Documentation](https://github.com/comfyanonymous/ComfyUI)
- [Workflow JSON Format](https://comfyanonymous.github.io/ComfyUI_documentation/)
