# Contributing to AI Script to Media

Thank you for contributing to this project! This guide covers workflows for our ad-hoc development model.

---

## Quick Links

- [Backlog](docs/backlog.md) - All user stories and features
- [Architecture Decisions](docs/adr/) - Technical decisions
- [Running Book](docs/running-book.md) - Setup and troubleshooting
- [Context Schema](docs/context-schema.md) - Data structures

---

## Getting Started

### 1. Pick an Issue

Browse the [backlog](docs/backlog.md) and look for:
- **P0** items (critical priority)
- Status: `Todo`
- GitHub issues labeled `good first issue` for first contributions

### 2. Create a Branch

```bash
# Checkout main and pull latest
git checkout main
git pull origin main

# Create feature branch
git checkout -b feature/{ISSUE-ID}-{short-name}

# Examples:
git checkout -b feature/CORE-002-ai-provider
git checkout -b feature/SCENE-001-parser-agent
```

**Branch Naming Convention:**
```
feature/{ISSUE-ID}-{kebab-case-name}
fix/{ISSUE-ID}-{kebab-case-name}
docs/{ISSUE-ID}-{kebab-case-name}
```

---

## Making Changes

### Commit Messages

Write clear, descriptive commit messages:

```
{Short summary in present tense}

{Optional detailed description}

{Issue reference}
```

**Examples:**

```bash
# Simple
git commit -m "Add IAIProvider interface

Refs #2"

# Detailed
git commit -m "Implement OllamaProvider HTTP client

- Add HttpClient configuration
- Implement GenerateResponseAsync
- Add error handling and retries
- Add timeout settings (2 minutes)

Refs #2"

# Final commit (closes issue)
git commit -m "Complete OllamaProvider with model selection

- Add model configuration from settings
- Implement fallback chain using configured DefaultModel
- Add IsAvailableAsync health check

Closes #2"
```

**Auto-Close Keywords:**
- `Closes #N` - Closes issue when PR merges
- `Fixes #N` - Closes issue when PR merges
- `Resolves #N` - Closes issue when PR merges

---

## Code Style

### C# Conventions

- Follow existing code style in the project
- Use nullable reference types (`string?` for nullables)
- Prefer `async/await` over blocking calls
- Use dependency injection for services
- Add XML docs for public APIs

```csharp
/// <summary>
/// Provides AI inference via Ollama.
/// </summary>
public class OllamaProvider : IAIProvider
{
    public string Name => "Ollama";
    
    /// <inheritdoc />
    public async Task<string> GenerateResponseAsync(string prompt, ModelOptions options)
    {
        // Implementation
    }
}
```

### File Organization

```
AIScriptToMediaDotNet/
├── src/
│   ├── AIScriptToMediaDotNet.Core/        # Interfaces, options, shared types
│   ├── AIScriptToMediaDotNet.Providers/   # AI provider implementations
│   ├── AIScriptToMediaDotNet.Agents/      # Agent implementations (vertical slices)
│   ├── AIScriptToMediaDotNet.Services/    # External service clients
│   └── AIScriptToMediaDotNet.App/         # Entry point, DI configuration
│
└── tests/
    ├── AIScriptToMediaDotNet.Core.Tests/
    ├── AIScriptToMediaDotNet.Providers.Tests/
    ├── AIScriptToMediaDotNet.Agents.Tests/
    └── AIScriptToMediaDotNet.Integration.Tests/
```

### Project Responsibilities

| Project | Responsibility | Examples |
|---------|---------------|----------|
| **Core** | Shared abstractions | `IAIProvider`, `ModelOptions`, `OllamaOptions` |
| **Providers** | AI provider implementations | `OllamaProvider`, future `OpenAIProvider` |
| **Agents** | Agent business logic | `SceneParserAgent`, `PhotoPromptCreatorAgent` |
| **Services** | External service clients | `ComfyUIClient`, `ExportService` |
| **App** | Composition, configuration | `Program.cs`, `appsettings.json` |

### Namespace Conventions

| Project | Root Namespace | Example |
|---------|---------------|---------|
| Core | `AIScriptToMediaDotNet.Core` | `AIScriptToMediaDotNet.Core.Interfaces.IAIProvider` |
| Providers | `AIScriptToMediaDotNet.Providers` | `AIScriptToMediaDotNet.Providers.Ollama.OllamaProvider` |
| Agents | `AIScriptToMediaDotNet.Agents` | `AIScriptToMediaDotNet.Agents.Scene.SceneParserAgent` |
| Services | `AIScriptToMediaDotNet.Services` | `AIScriptToMediaDotNet.Services.ComfyUI.ComfyUIClient` |
| App | `AIScriptToMediaDotNet.App` | `AIScriptToMediaDotNet.App.Program` |

---

## Pull Requests

### Before Submitting

- [ ] Code builds: `dotnet build`
- [ ] Tests pass: `dotnet test`
- [ ] No compiler warnings
- [ ] Updated relevant documentation
- [ ] Project references are correct (no circular dependencies)

### Building and Testing

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/AIScriptToMediaDotNet.Core.Tests

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run the application
dotnet run --project src/AIScriptToMediaDotNet.App
```

### Creating a PR

1. **Push your branch:**
   ```bash
   git push origin feature/CORE-002-ai-provider
   ```

2. **Create PR on GitHub** with this template:

---

## PR Template

```markdown
## Summary
{Brief description of changes}

## Changes
- {Change 1}
- {Change 2}
- {Change 3}

## Related Issues
Closes #{issue-number}

## Checklist
- [ ] Code builds without errors
- [ ] Tests pass (if applicable)
- [ ] Documentation updated
- [ ] ADRs reviewed (if architectural change)

## Testing Notes
{How to test this PR, if not obvious}
```

---

## Documentation Updates

### When to Update Docs

| Change Type | Update |
|-------------|--------|
| New feature | `backlog.md` → mark as Done |
| New ADR | Create `docs/adr/ADR-XXX-{name}.md` |
| Schema change | `docs/context-schema.md` |
| Setup change | `docs/running-book.md` |
| README change | `README.md` (overview only) |

### Updating Backlog

When completing an item:

```markdown
### [CORE-002] AI Provider Abstraction
**Priority**: P0  
**Status**: ✅ Done  
**GitHub**: [#2](https://github.com/youruser/repo/issues/2)
**Completed**: 2026-02-23
**PR**: [#15](https://github.com/youruser/repo/pull/15)
```

---

## Ad-Hoc Workflow

Since we work ad-hoc (no sprints):

1. **Pick up work** when you have time
2. **Complete one issue** at a time
3. **Submit PR** when ready
4. **Update backlog** after merge
5. **Pick next P0 item**

No pressure, no deadlines - progress at your own pace.

---

## Questions?

- Check existing [documentation](docs/)
- Review [Architecture Decisions](docs/adr/)
- Look at similar issues/PRs for examples

---

## Thank You!

Every contribution helps move this project forward. 🙏
