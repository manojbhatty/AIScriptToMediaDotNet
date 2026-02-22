# GitHub Issues Creator Scripts

PowerShell scripts to automate GitHub issue creation from `docs/backlog.md`.

---

## Prerequisites

### 1. Install GitHub CLI

**Windows:**
```powershell
winget install GitHub.cli
# or
choco install gh
```

**Verify:**
```bash
gh --version
```

### 2. Authenticate with GitHub

```bash
gh auth login
```

Follow the prompts to authenticate.

### 3. Update Script Configuration

Edit `scripts/create-github-issues.ps1` and update:

```powershell
$RepoOwner = "yourusername"  # Your GitHub username
$RepoName = "AIScriptToMediaDotNet"  # Your repo name
```

---

## Usage

### Create All Issues

```powershell
# From project root
.\scripts\create-github-issues.ps1
```

### Filter by Epic

```powershell
# Create only Core infrastructure issues
.\scripts\create-github-issues.ps1 -Epic "Core"

# Other epics: Scene, Photo, Video, Export, ComfyUI, Ext
.\scripts\create-github-issues.ps1 -Epic "Scene"
```

### Filter by Priority

```powershell
# Create only P0 (critical) issues
.\scripts\create-github-issues.ps1 -Priority "P0"
```

### Preview Mode (WhatIf)

```powershell
# See what would be created without actually creating
.\scripts\create-github-issues.ps1 -WhatIf
```

### Verbose Output

```powershell
# Show detailed output
.\scripts\create-github-issues.ps1 -ShowDetails
```

---

## Examples

### Create all P0 Core issues (preview first)

```powershell
# Preview
.\scripts\create-github-issues.ps1 -Epic "Core" -Priority "P0" -WhatIf

# Create
.\scripts\create-github-issues.ps1 -Epic "Core" -Priority "P0"
```

### Create all Scene processing issues

```powershell
.\scripts\create-github-issues.ps1 -Epic "Scene"
```

---

## Labels Created

The script automatically creates these labels if they don't exist:

| Label | Color | Description |
|-------|-------|-------------|
| `P0` | #FF0000 | Critical priority |
| `P1` | #FFA500 | Important priority |
| `P2` | #FFFF00 | Nice-to-have priority |
| `epic/core` | #0066CC | Core Infrastructure |
| `epic/scene` | #00AA00 | Scene Processing |
| `epic/photo` | #AA00AA | Photo Prompts |
| `epic/video` | #00AAAA | Video Prompts |
| `epic/export` | #CC6600 | Export & Output |
| `epic/comfyui` | #6600CC | ComfyUI Integration |
| `epic/ext` | #666666 | Extensibility |

---

## Issue Format

Each issue is created with:

- **Title**: `[CORE-002] AI Provider Abstraction`
- **Labels**: Priority (P0/P1/P2) + Epic (epic/core, etc.)
- **Body**:
  - User story format (As a / I want / So that)
  - Acceptance criteria as checkboxes
  - Implementation notes with branching instructions
  - Links back to backlog.md and ADRs

---

## Troubleshooting

### "gh command not found"

Install GitHub CLI: https://cli.github.com/

### "Not authenticated"

```bash
gh auth login
```

### "Repository not found"

Update `$RepoOwner` and `$RepoName` in the script.

### "Label already exists"

The script handles this automatically - it checks before creating.

### Rate limiting

The script waits 2 seconds between issue creations to avoid rate limits.

---

## Manual Alternative

If you prefer not to use the script, create issues manually:

```bash
gh issue create \
  --title "[CORE-002] AI Provider Abstraction" \
  --label "P0,epic/core" \
  --body "As a system architect..."
```

---

## Next Steps After Creating Issues

1. Review created issues on GitHub
2. Add to a GitHub Project board (optional)
3. Start working on P0 items
4. Reference issues in commits: `Refs #123`
5. Close issues in PRs: `Closes #123`
