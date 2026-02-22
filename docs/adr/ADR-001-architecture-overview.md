# ADR-001: Architecture Overview

**Status**: Accepted  
**Date**: 2026-02-23  
**Author**: Development Team

---

## Context

We are building a multi-agent AI system that transforms text scripts into visual media (images and videos). The system needs to:

1. Parse scripts into scenes
2. Generate image and video prompts
3. Validate outputs at each stage
4. Generate images using ComfyUI
5. Support local AI (Ollama) with future cloud provider options

## Decision

We will use an **Orchestrator-Based Multi-Agent Pipeline** architecture with the following characteristics:

### Pipeline Stages

```mermaid
flowchart TB
    subgraph Input["Input"]
        Script[📄 Text Script]
    end
    
    subgraph Stage1["Stage 1: Scene Creation"]
        Parser[🤖 Scene Parser]
        Verifier1[🤖 Scene Verifier]
    end
    
    subgraph Stage2["Stage 2: Photo Prompts"]
        PhotoCreator[🤖 Photo Creator]
        Verifier2[🤖 Photo Verifier]
    end
    
    subgraph Stage3["Stage 3: Video Prompts"]
        VideoCreator[🤖 Video Creator]
        Verifier3[🤖 Video Verifier]
    end
    
    subgraph Stage4["Stage 4: Export & Generate"]
        Export[📝 Markdown Exporter]
        ComfyUI[🎨 ComfyUI Generator]
        Log[📋 Agent Logger]
    end

    subgraph Output["Output"]
        Script[script.md]
        Scenes[scenes.md]
        PhotoPrompts[photo-prompts.md]
        VideoPrompts[video-prompts.md<br/>reference only]
        Images[🖼️ Generated Images]
        AgentLog[agent-log.md]
    end

    Script --> Parser
    Parser <-->|retry ×3| Verifier1
    Verifier1 --> PhotoCreator
    PhotoCreator <-->|retry ×3| Verifier2
    Verifier2 --> VideoCreator
    VideoCreator <-->|retry ×3| Verifier3
    Verifier3 --> Export
    Verifier3 --> ComfyUI
    Verifier3 --> Log
    Export --> Script
    Export --> Scenes
    Export --> PhotoPrompts
    Export --> VideoPrompts
    ComfyUI --> Images
    Log --> AgentLog
```

### Component Interaction

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Logger as Agent Logger
    participant Parser as Scene Parser
    participant Verifier1 as Scene Verifier
    participant PhotoCreator as Photo Creator
    participant Verifier2 as Photo Verifier
    participant VideoCreator as Video Creator
    participant Verifier3 as Video Verifier
    participant Exporter as Markdown Exporter
    participant ComfyUI as ComfyUI Client

    User->>Orchestrator: Load Script (Title + Text)
    Orchestrator->>Logger: Log: Pipeline Started
    Orchestrator->>Parser: ProcessAsync(Context)
    Parser->>Logger: Log: SceneParser Started
    Parser-->>Orchestrator: Scenes
    Parser->>Logger: Log: SceneParser Completed
    loop Retry up to 3 times
        Orchestrator->>Verifier1: ValidateAsync(Context)
        Verifier1->>Logger: Log: SceneVerifier Started
        alt Valid
            Verifier1-->>Orchestrator: ValidationResult ✓
            Verifier1->>Logger: Log: ValidationPassed
        else Invalid
            Verifier1-->>Orchestrator: ValidationResult ✗ + Feedback
            Verifier1->>Logger: Log: ValidationFailed + Feedback
            Orchestrator->>Parser: Retry with Feedback
            Parser->>Logger: Log: Retry #N with Feedback
        end
    end

    Orchestrator->>PhotoCreator: ProcessAsync(Context)
    PhotoCreator->>Logger: Log: PhotoCreator Started
    PhotoCreator-->>Orchestrator: Photo Prompts
    PhotoCreator->>Logger: Log: PhotoCreator Completed
    loop Retry up to 3 times
        Orchestrator->>Verifier2: ValidateAsync(Context)
        Verifier2->>Logger: Log: PhotoVerifier Started
        alt Valid
            Verifier2-->>Orchestrator: ValidationResult ✓
            Verifier2->>Logger: Log: ValidationPassed
        else Invalid
            Verifier2-->>Orchestrator: ValidationResult ✗ + Feedback
            Verifier2->>Logger: Log: ValidationFailed + Feedback
            Orchestrator->>PhotoCreator: Retry with Feedback
            PhotoCreator->>Logger: Log: Retry #N with Feedback
        end
    end

    Orchestrator->>VideoCreator: ProcessAsync(Context)
    VideoCreator->>Logger: Log: VideoCreator Started
    VideoCreator-->>Orchestrator: Video Prompts
    VideoCreator->>Logger: Log: VideoCreator Completed
    loop Retry up to 3 times
        Orchestrator->>Verifier3: ValidateAsync(Context)
        Verifier3->>Logger: Log: VideoVerifier Started
        alt Valid
            Verifier3-->>Orchestrator: ValidationResult ✓
            Verifier3->>Logger: Log: ValidationPassed
        else Invalid
            Verifier3-->>Orchestrator: ValidationResult ✗ + Feedback
            Verifier3->>Logger: Log: ValidationFailed + Feedback
            Orchestrator->>VideoCreator: Retry with Feedback
            VideoCreator->>Logger: Log: Retry #N with Feedback
        end
    end

    Orchestrator->>Exporter: Export Prompts
    Exporter->>Logger: Log: Export Started
    Exporter->>Logger: Log: Export Completed
    Orchestrator->>ComfyUI: Generate Images
    ComfyUI->>Logger: Log: Image Generation Started
    ComfyUI-->>Orchestrator: Generated Images
    ComfyUI->>Logger: Log: Image Generation Completed
    Orchestrator->>Logger: Save agent-log.md
    Logger-->>Orchestrator: Log Saved
    Orchestrator-->>User: Output Folder ({Title}_{date}/)
```

### Agent Flow Detail

```mermaid
flowchart LR
    subgraph Agent["Any Agent"]
        Input[Input Context] --> Process[AI Processing]
        Process --> AI[LLM Call via IAIProvider]
        AI --> Parse[Parse Response]
        Parse --> Output[Output Result]
    end
    
    subgraph Verifier["Verifier Agent"]
        VInput[Input Context] --> VValidate[Validation Logic]
        VValidate --> VAI[LLM Call]
        VAI --> VParse[Parse ValidationResult]
        VParse --> VOutput[Valid/Invalid + Feedback]
    end
```

### Agent Pattern

Each agent follows a consistent pattern:
- Receives `ScriptToMediaContext`
- Performs specific transformation/validation
- Returns result with success/failure status
- Verifiers provide feedback for retries

### Output Structure

All output is saved to a folder named `{ScriptTitle}_{YYYY-MM-DD_HH-mm-ss}/`:

| File | Description |
|------|-------------|
| `script.md` | Original input script |
| `scenes.md` | Parsed scenes with metadata |
| `photo-prompts.md` | Image generation prompts per scene |
| `video-prompts.md` | Video prompts per scene (reference only, no generation) |
| `agent-log.md` | Detailed agent execution log |
| `images/` | Generated images from ComfyUI |

**Note**: Video prompts are created for future reference and planning purposes only. Version 1 does not generate videos.

## Consequences

### Positive

- **Clear separation of concerns**: Each agent has single responsibility
- **Quality gates**: Verifier agents catch errors early
- **Retry resilience**: Automatic correction attempts
- **Extensibility**: Easy to add new agents or stages
- **Testability**: Agents can be tested in isolation
- **Provider flexibility**: Swap AI backends per-agent or globally

### Negative

- **Complexity**: More components than simple linear pipeline
- **Latency**: Multiple AI calls per stage increases total time
- **State management**: Shared context must be carefully managed
- **Debugging**: Harder to trace issues across multiple agents

### Risks Mitigated

| Risk | Mitigation |
|------|------------|
| AI produces poor output | Verifier agents + retry loop |
| Local AI too slow | Configurable models, future cloud option |
| ComfyUI unavailable | Client handles errors, continues pipeline |
| Context corruption | Immutable snapshots, validation at each stage |

---

## Alternatives Considered

### Option 1: Single Monolithic Agent
One AI handles everything in one call.

**Rejected because**:
- No quality control
- Hard to debug failures
- No retry granularity
- Less control over output format

### Option 2: Event-Driven Architecture
Agents communicate via events/message bus.

**Rejected because**:
- Over-engineering for current scope
- Adds infrastructure complexity
- Harder to maintain execution order
- Debugging distributed flows is complex

### Option 3: Functional Pipeline
Pure functional transformations (F# style).

**Rejected because**:
- Less flexible for retry logic
- Harder to inject cross-cutting concerns (logging, metrics)
- Team more comfortable with OOP

---

## Compliance

This decision aligns with:
- Local-first AI requirement (Ollama)
- Future cloud provider extensibility
- Quality verification requirements
- Retry mechanism requirements (3 attempts)

---

## Notes

- Architecture may evolve as implementation reveals challenges
- Core pattern (orchestrator + agents) should remain stable
- Future: Consider parallel execution for photo/video prompt stages
