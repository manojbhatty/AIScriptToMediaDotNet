# GitHub Issues Creator for AI Script to Media
# Creates GitHub issues from backlog.md user stories
#
# Prerequisites:
#   - GitHub CLI (gh) installed: https://cli.github.com/
#   - Authenticated with GitHub: gh auth login
#   - Run from project root directory
#
# Usage:
#   .\scripts\create-github-issues.ps1
#   .\scripts\create-github-issues.ps1 -Epic "Core"
#   .\scripts\create-github-issues.ps1 -Priority "P0"
#   .\scripts\create-github-issues.ps1 -WhatIf

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Core", "Scene", "Photo", "Video", "Export", "ComfyUI", "Ext")]
    [string]$Epic,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("P0", "P1", "P2")]
    [string]$Priority,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf,
    
    [Parameter(Mandatory = $false)]
    [switch]$ShowDetails
)

# Enable verbose output if ShowDetails is set
if ($ShowDetails) { $VerbosePreference = "Continue" }

# Configuration
$BacklogFile = "docs\backlog.md"
$RepoOwner = "bhattyma"  # TODO: Update with your GitHub username
$RepoName = "AIScriptToMediaDotNet"  # TODO: Update with your repo name

# Color output functions
function Write-Info { Write-Host $args[0] -ForegroundColor Cyan }
function Write-Success { Write-Host $args[0] -ForegroundColor Green }
function Write-Warning { Write-Host $args[0] -ForegroundColor Yellow }
function Write-Error { Write-Host $args[0] -ForegroundColor Red }

# Check prerequisites
Write-Info "Checking prerequisites..."

try {
    $ghVersion = gh --version 2>&1 | Select-Object -First 1
    Write-Success "GitHub CLI found: $ghVersion"
} catch {
    Write-Error "GitHub CLI (gh) not found. Please install from: https://cli.github.com/"
    exit 1
}

try {
    $ghStatus = gh auth status 2>&1
    if ($ghStatus -match "Logged in") {
        Write-Success "Authenticated with GitHub"
    } else {
        throw "Not authenticated"
    }
} catch {
    Write-Error "Not authenticated with GitHub. Run: gh auth login"
    exit 1
}

# Check backlog file exists
if (-not (Test-Path $BacklogFile)) {
    Write-Error "Backlog file not found: $BacklogFile"
    exit 1
}

Write-Info "Reading backlog from: $BacklogFile"

# Parse backlog.md line by line
$lines = Get-Content $BacklogFile
$backlogItems = @()
$currentItem = $null
$currentSection = $null
$acceptanceLines = @()

foreach ($line in $lines) {
    # Check for new backlog item header: ### [CORE-001] Title
    if ($line -match '^### \[([A-Z]+-\d+)\]\s+(.+)$') {
        # Save previous item if exists
        if ($currentItem -ne $null -and $acceptanceLines.Count -gt 0) {
            $currentItem.AcceptanceCriteria = $acceptanceLines -join "`n"
            $backlogItems += $currentItem
        }
        
        # Start new item
        $currentItem = [PSCustomObject]@{
            Id = $matches[1]
            Title = $matches[2].Trim()
            Priority = ""
            Status = ""
            AsA = ""
            WantTo = ""
            SoThat = ""
            AcceptanceCriteria = ""
            Epic = $matches[1].Split('-')[0]
        }
        $acceptanceLines = @()
        $currentSection = $null
        
        if ($ShowDetails) { Write-Info "Found item: $($currentItem.Id) - $($currentItem.Title)" }
        continue
    }
    
    # Skip if no current item
    if ($currentItem -eq $null) { continue }
    
    # Extract Priority
    if ($line -match '\*\*Priority\*\*:\s*(P[0-2])') {
        $currentItem.Priority = $matches[1]
        if ($ShowDetails) { Write-Info "  Priority: $($currentItem.Priority)" }
        continue
    }
    
    # Extract Status
    if ($line -match '\*\*Status\*\*:\s*(\w+(?:\s+\w+)*)') {
        $currentItem.Status = $matches[1].Trim()
        if ($ShowDetails) { Write-Info "  Status: $($currentItem.Status)" }
        continue
    }
    
    # Extract As a
    if ($line -match '\*\*As a\*\*\s+(.+)$') {
        $currentItem.AsA = $matches[1].Trim()
        continue
    }
    
    # Extract I want to
    if ($line -match '\*\*I want to\*\*\s+(.+)$') {
        $currentItem.WantTo = $matches[1].Trim()
        continue
    }
    
    # Extract So that
    if ($line -match '\*\*So that\*\*\s+(.+)$') {
        $currentItem.SoThat = $matches[1].Trim()
        continue
    }
    
    # Check if we're in Acceptance Criteria section
    if ($line -match '\*\*Acceptance Criteria\*\*:') {
        $currentSection = "AcceptanceCriteria"
        continue
    }
    
    # Collect acceptance criteria lines
    if ($currentSection -eq "AcceptanceCriteria") {
        # Stop at next section (--- or new ###)
        if ($line -match '^---$' -or $line -match '^### ') {
            $currentSection = $null
        } elseif ($line.Trim() -ne '') {
            $acceptanceLines += $line
        }
    }
}

# Don't forget the last item
if ($currentItem -ne $null -and $acceptanceLines.Count -gt 0) {
    $currentItem.AcceptanceCriteria = $acceptanceLines -join "`n"
    $backlogItems += $currentItem
}

Write-Info "Successfully parsed $($backlogItems.Count) backlog items"

if ($ShowDetails) {
    Write-Info "`nParsed items summary:"
    foreach ($item in $backlogItems) {
        Write-Info "  $($item.Id): $($item.Title) [Status: $($item.Status), Priority: $($item.Priority)]"
    }
}

# Filter by Epic if specified
if ($Epic) {
    $epicMap = @{
        "Core" = "CORE"
        "Scene" = "SCENE"
        "Photo" = "PHOTO"
        "Video" = "VIDEO"
        "Export" = "EXPORT"
        "ComfyUI" = "COMFY"
        "Ext" = "EXT"
    }
    $epicPrefix = $epicMap[$Epic]
    $backlogItems = $backlogItems | Where-Object { $_.Id -like "$epicPrefix-*" }
    Write-Info "Filtered to Epic: $Epic ($($backlogItems.Count) items)"
}

# Filter by Priority if specified
if ($Priority) {
    $backlogItems = $backlogItems | Where-Object { $_.Priority -eq $Priority }
    Write-Info "Filtered to Priority: $Priority ($($backlogItems.Count) items)"
}

# Skip already done items
$backlogItems = $backlogItems | Where-Object { $_.Status -eq "Todo" }
Write-Info "Remaining Todo items: $($backlogItems.Count)"

if ($backlogItems.Count -eq 0) {
    Write-Info "No items to create issues for!"
    exit 0
}

# Display what will be created
Write-Info "`nIssues to create:"
Write-Host ("{0,-12} {1,-8} {2}" -f "ID", "Priority", "Title")
Write-Host ("-" * 60)
foreach ($item in $backlogItems) {
    Write-Host ("{0,-12} {1,-8} {2}" -f $item.Id, $item.Priority, $item.Title)
}

if ($WhatIf) {
    Write-Warning "`nWhatIf mode - no issues will be created"
    exit 0
}

# Confirm before creating
$confirmation = Read-Host "`nCreate $($backlogItems.Count) issue(s)? (y/n)"
if ($confirmation -ne 'y') {
    Write-Info "Cancelled"
    exit 0
}

# Create labels if they don't exist
function Ensure-Label {
    param([string]$Name, [string]$Color, [string]$Description)

    try {
        # Check if label exists
        $existing = gh label list --search $Name 2>&1
        if ($existing -match $Name) {
            if ($ShowDetails) { Write-Info "Label exists: $Name" }
            return
        }
        
        # Create label
        if ($ShowDetails) { Write-Info "Creating label: $Name (#$Color)" }
        $createOutput = gh label create $Name --color $Color --description $Description 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Created label: $Name"
        } else {
            Write-Warning "Failed to create label $Name`: $createOutput"
        }
    } catch {
        Write-Warning "Could not create label $Name : $_"
    }
}

Write-Info "`nEnsuring labels exist..."
Ensure-Label "P0" "FF0000" "Critical priority"
Ensure-Label "P1" "FFA500" "Important priority"
Ensure-Label "P2" "FFFF00" "Nice-to-have priority"
Ensure-Label "epic/core" "0066CC" "Core Infrastructure"
Ensure-Label "epic/scene" "00AA00" "Scene Processing"
Ensure-Label "epic/photo" "AA00AA" "Photo Prompts"
Ensure-Label "epic/video" "00AAAA" "Video Prompts"
Ensure-Label "epic/export" "CC6600" "Export & Output"
Ensure-Label "epic/comfyui" "6600CC" "ComfyUI Integration"
Ensure-Label "epic/ext" "666666" "Extensibility"

# Create issues
Write-Info "`nCreating issues..."

foreach ($item in $backlogItems) {
    Write-Info "`nProcessing: $($item.Id)"
    
    # Build issue body
    $acceptanceCriteriaFormatted = $item.AcceptanceCriteria -replace '^\s*-\s+\[', '- [ ' -replace '\]\s*$', ' ]'
    
    $body = @"
## Description

**As a** $($item.AsA)  
**I want to** $($item.WantTo)  
**So that** $($item.SoThat)

## Acceptance Criteria

$($item.AcceptanceCriteria -split "`n" | Where-Object { $_.Trim() -match '^-\s*\[' } | ForEach-Object { $_.Trim() })

## Implementation Notes

- [ ] Follow [CONTRIBUTING.md](../blob/main/CONTRIBUTING.md)
- [ ] Create branch: \`feature/$($item.Id)-{short-name}\`
- [ ] Reference this issue in commits: \`Refs #$issueNumber\`
- [ ] Close this issue in final commit: \`Closes #$issueNumber\`

## Links

- **Backlog**: [docs/backlog.md](../blob/main/docs/backlog.md)
- **Related ADR**: Check [docs/adr/](../blob/main/docs/adr/) for relevant architecture decisions

---

*Created from backlog item [$($item.Id)](../blob/main/docs/backlog.md)*
"@

    # Create issue title
    $title = "[$($item.Id)] $($item.Title)"
    
    if ($ShowDetails) {
        Write-Info "Title: $title"
        Write-Info "Body length: $($body.Length) chars"
    }
    
    # Create the issue
    if ($WhatIf) {
        Write-Warning "Would create: $title"
        continue
    }
    
    try {
        # Create issue with labels (use -l flag, comma-separated for multiple)
        # Map epic names to label names
        $epicLabelMap = @{
            "COMFY" = "epic/comfyui"
        }
        $epicLabel = if ($epicLabelMap.ContainsKey($item.Epic)) { $epicLabelMap[$item.Epic] } else { "epic/$($item.Epic.ToLower())" }
        
        $labels = @($item.Priority, $epicLabel)
        $labelsString = $labels -join ","

        $issueOutput = gh issue create `
            --title $title `
            --body "$body" `
            -l $labelsString `
            2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Created: $issueOutput"
            
            # Extract issue number from output (usually in URL)
            if ($issueOutput -match '/issues/(\d+)') {
                $issueNumber = $matches[1]
                Write-Info "Issue #$issueNumber created for $($item.Id)"
            }
        } else {
            Write-Error "Failed to create issue for $($item.Id): $issueOutput"
        }
    } catch {
        Write-Error "Exception creating issue for $($item.Id): $_"
    }
    
    # Rate limiting - wait between creations
    Start-Sleep -Seconds 2
}

Write-Info "`nDone! Review created issues on GitHub."
Write-Info "Repo: https://github.com/$RepoOwner/$RepoName/issues"
