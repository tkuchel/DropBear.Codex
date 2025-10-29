#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates a release tag for DropBear.Codex with validation.

.DESCRIPTION
    This script automates the creation of release tags with proper validation:
    - Verifies version format (vYYYY.MM.patch)
    - Checks you're on master branch
    - Ensures working directory is clean
    - Validates version.json matches tag
    - Creates annotated tag with changelog excerpt
    - Optionally pushes tag to trigger release workflow

.PARAMETER Version
    The version to tag (e.g., "2025.11.0"). The 'v' prefix is optional.

.PARAMETER Message
    Optional custom tag message. If not provided, extracts from CHANGELOG.md.

.PARAMETER Push
    If specified, automatically pushes the tag to origin after creation.

.PARAMETER Force
    Skip validation checks (use with caution).

.EXAMPLE
    .\create-release-tag.ps1 -Version "2025.11.0"
    Creates tag v2025.11.0 with changelog message, prompts before pushing.

.EXAMPLE
    .\create-release-tag.ps1 -Version "2025.11.0" -Push
    Creates tag and automatically pushes to origin.

.EXAMPLE
    .\create-release-tag.ps1 -Version "v2025.11.0" -Message "Hotfix release"
    Creates tag with custom message.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Message,

    [Parameter(Mandatory = $false)]
    [switch]$Push,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { Write-Host "✅ $args" -ForegroundColor Green }
function Write-Error { Write-Host "❌ $args" -ForegroundColor Red }
function Write-Warning { Write-Host "⚠️  $args" -ForegroundColor Yellow }
function Write-Info { Write-Host "ℹ️  $args" -ForegroundColor Cyan }

# Ensure we're in the repository root
if (-not (Test-Path ".git")) {
    Write-Error "Must be run from repository root (directory with .git folder)"
    exit 1
}

# Normalize version (add 'v' prefix if missing)
if (-not $Version.StartsWith("v")) {
    $Version = "v$Version"
}

$VersionWithoutV = $Version.Substring(1)

Write-Info "Creating release tag: $Version"
Write-Host ""

#region Validation

if (-not $Force) {
    Write-Info "Running validation checks..."

    # 1. Validate version format
    if ($Version -notmatch '^v\d{4}\.\d{1,2}\.\d+$') {
        Write-Error "Invalid version format: $Version"
        Write-Host "Expected format: vYYYY.MM.patch (e.g., v2025.11.0)"
        exit 1
    }
    Write-Success "Version format valid"

    # 2. Check current branch is master
    $currentBranch = git rev-parse --abbrev-ref HEAD
    if ($currentBranch -ne "master") {
        Write-Error "Must be on 'master' branch (currently on '$currentBranch')"
        Write-Host "Run: git checkout master"
        exit 1
    }
    Write-Success "On master branch"

    # 3. Check working directory is clean
    $status = git status --porcelain
    if ($status) {
        Write-Error "Working directory has uncommitted changes:"
        git status --short
        Write-Host ""
        Write-Host "Commit or stash changes before creating release tag"
        exit 1
    }
    Write-Success "Working directory clean"

    # 4. Check if up to date with origin
    git fetch origin master --quiet
    $local = git rev-parse master
    $remote = git rev-parse origin/master

    if ($local -ne $remote) {
        Write-Error "Local master is not up to date with origin/master"
        Write-Host "Run: git pull origin master"
        exit 1
    }
    Write-Success "Up to date with origin/master"

    # 5. Validate version.json
    if (Test-Path "version.json") {
        $versionJson = Get-Content "version.json" -Raw | ConvertFrom-Json
        $jsonVersion = $versionJson.version

        # Extract major.minor from tag (e.g., "2025.11" from "v2025.11.0")
        if ($VersionWithoutV -match '^(\d{4}\.\d{1,2})') {
            $tagMajorMinor = $matches[1]

            if ($jsonVersion -ne $tagMajorMinor) {
                Write-Error "Version mismatch!"
                Write-Host "  version.json: $jsonVersion"
                Write-Host "  Tag version:  $tagMajorMinor"
                Write-Host ""
                Write-Host "Update version.json to match the tag version (without patch)"
                exit 1
            }
            Write-Success "version.json matches tag ($jsonVersion)"
        }
    } else {
        Write-Warning "version.json not found, skipping version validation"
    }

    # 6. Check tag doesn't already exist
    $existingTag = git tag -l $Version
    if ($existingTag) {
        Write-Error "Tag $Version already exists"
        Write-Host ""
        Write-Host "To delete and recreate:"
        Write-Host "  git tag -d $Version"
        Write-Host "  git push origin :refs/tags/$Version"
        exit 1
    }
    Write-Success "Tag $Version is available"

    Write-Host ""
}

#endregion

#region Generate Tag Message

if (-not $Message) {
    Write-Info "Extracting changelog for $VersionWithoutV..."

    if (Test-Path "CHANGELOG.md") {
        $changelog = Get-Content "CHANGELOG.md" -Raw

        # Try to extract the specific version section
        $pattern = "## \[$([regex]::Escape($VersionWithoutV))\][^\n]*\n(.*?)(?=\n## \[|$)"
        if ($changelog -match $pattern) {
            $versionChangelog = $matches[1].Trim()

            if ($versionChangelog) {
                $Message = "Release $VersionWithoutV`n`n$versionChangelog"
                Write-Success "Extracted changelog from CHANGELOG.md"
            }
        }

        # Fallback: extract unreleased section
        if (-not $Message) {
            $pattern = "## \[Unreleased\][^\n]*\n(.*?)(?=\n## \[|$)"
            if ($changelog -match $pattern) {
                $unreleasedChangelog = $matches[1].Trim()

                if ($unreleasedChangelog) {
                    $Message = "Release $VersionWithoutV`n`n$unreleasedChangelog"
                    Write-Warning "Using [Unreleased] section from CHANGELOG.md"
                    Write-Host "Consider moving these changes to a [$VersionWithoutV] section before releasing"
                }
            }
        }
    }

    # Final fallback
    if (-not $Message) {
        $Message = "Release $VersionWithoutV`n`nSee CHANGELOG.md for details."
        Write-Warning "Could not extract changelog, using default message"
    }
}

#endregion

#region Create Tag

Write-Host ""
Write-Info "Tag Message:"
Write-Host "─────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host $Message
Write-Host "─────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

if (-not $Force) {
    $confirm = Read-Host "Create tag $Version? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Warning "Tag creation cancelled"
        exit 0
    }
}

try {
    # Create annotated tag
    git tag -a $Version -m $Message

    Write-Success "Created tag $Version"

    # Show tag details
    Write-Host ""
    Write-Info "Tag details:"
    git show $Version --quiet

} catch {
    Write-Error "Failed to create tag: $_"
    exit 1
}

#endregion

#region Push Tag

Write-Host ""

if ($Push) {
    $shouldPush = $true
} else {
    $pushConfirm = Read-Host "Push tag to origin? This will trigger the release workflow (y/N)"
    $shouldPush = ($pushConfirm -eq "y" -or $pushConfirm -eq "Y")
}

if ($shouldPush) {
    try {
        Write-Info "Pushing tag to origin..."
        git push origin $Version

        Write-Success "Tag pushed to origin"
        Write-Host ""
        Write-Info "Release workflow triggered!"
        Write-Host "Monitor progress: https://github.com/tkuchel/DropBear.Codex/actions"

    } catch {
        Write-Error "Failed to push tag: $_"
        Write-Host ""
        Write-Host "Tag created locally but not pushed. To push manually:"
        Write-Host "  git push origin $Version"
        exit 1
    }
} else {
    Write-Warning "Tag created locally but not pushed"
    Write-Host ""
    Write-Host "To push later:"
    Write-Host "  git push origin $Version"
    Write-Host ""
    Write-Host "To delete if needed:"
    Write-Host "  git tag -d $Version"
}

Write-Host ""
Write-Success "Done!"
