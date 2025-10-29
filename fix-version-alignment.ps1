#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Removes hardcoded versions from .csproj files to let Nerdbank.GitVersioning manage versions.

.DESCRIPTION
    This script removes hardcoded <Version> elements from all .csproj files in the solution.
    Nerdbank.GitVersioning will automatically set the version based on version.json.

    This prevents version misalignment between:
    - Individual .csproj files
    - version.json (Nerdbank.GitVersioning)
    - Actual package versions

.PARAMETER DryRun
    If specified, shows what would be changed without making actual modifications.

.EXAMPLE
    .\fix-version-alignment.ps1
    Removes all hardcoded versions from .csproj files.

.EXAMPLE
    .\fix-version-alignment.ps1 -DryRun
    Shows which files would be modified without making changes.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$DryRun
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

Write-Info "Scanning for .csproj files with hardcoded versions..."
Write-Host ""

# Find all .csproj files
$csprojFiles = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

$modifiedFiles = @()
$totalFiles = 0

foreach ($file in $csprojFiles) {
    $totalFiles++
    $content = Get-Content $file.FullName -Raw

    # Check if file contains <Version> element
    if ($content -match '<Version>([^<]+)</Version>') {
        $currentVersion = $matches[1]

        Write-Warning "Found hardcoded version in: $($file.Name)"
        Write-Host "  Current version: $currentVersion" -ForegroundColor Yellow

        if (-not $DryRun) {
            # Remove the <Version> line
            $newContent = $content -replace '\s*<Version>[^<]+</Version>\s*\r?\n', ''

            # Save the file
            Set-Content -Path $file.FullName -Value $newContent -NoNewline

            Write-Success "  Removed hardcoded version"
            $modifiedFiles += $file
        } else {
            Write-Info "  [DRY RUN] Would remove: <Version>$currentVersion</Version>"
        }

        Write-Host ""
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan

if ($modifiedFiles.Count -eq 0) {
    Write-Success "No hardcoded versions found! All .csproj files are clean."
} else {
    if ($DryRun) {
        Write-Warning "DRY RUN: Would modify $($modifiedFiles.Count) file(s):"
    } else {
        Write-Success "Modified $($modifiedFiles.Count) file(s):"
    }

    foreach ($file in $modifiedFiles) {
        Write-Host "  - $($file.FullName.Replace($PWD.Path, '.'))" -ForegroundColor Cyan
    }

    if (-not $DryRun) {
        Write-Host ""
        Write-Info "Next steps:"
        Write-Host "  1. Review changes: git diff"
        Write-Host "  2. Test build: dotnet build"
        Write-Host "  3. Commit changes: git add . && git commit -m 'chore: Remove hardcoded versions, use Nerdbank.GitVersioning'"
    }
}

Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Show version.json for reference
if (Test-Path "version.json") {
    Write-Info "Current version.json configuration:"
    $versionJson = Get-Content "version.json" | ConvertFrom-Json
    Write-Host "  Version: $($versionJson.version)" -ForegroundColor Cyan
    Write-Host ""
    Write-Success "Nerdbank.GitVersioning will manage all package versions based on version.json"
}

Write-Success "Done!"
