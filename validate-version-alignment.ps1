#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates that no .csproj files have hardcoded versions.

.DESCRIPTION
    This script checks all .csproj files to ensure they don't contain hardcoded
    <Version> elements. Versions should be managed solely by Nerdbank.GitVersioning
    via version.json.

    This script is designed to run in CI pipelines to catch version misalignment
    before it gets merged.

.PARAMETER Fix
    If specified, automatically runs fix-version-alignment.ps1 to remove hardcoded versions.

.EXAMPLE
    .\validate-version-alignment.ps1
    Validates all .csproj files and exits with error code if issues found.

.EXAMPLE
    .\validate-version-alignment.ps1 -Fix
    Validates and automatically fixes any issues found.

.NOTES
    Exit codes:
    0 - All validations passed
    1 - Hardcoded versions found
    2 - Script execution error
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Fix
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { Write-Host "✅ $args" -ForegroundColor Green }
function Write-Error { Write-Host "❌ $args" -ForegroundColor Red }
function Write-Warning { Write-Host "⚠️  $args" -ForegroundColor Yellow }
function Write-Info { Write-Host "ℹ️  $args" -ForegroundColor Cyan }

Write-Info "Validating version alignment..."
Write-Host ""

# Check 1: Verify version.json exists
if (-not (Test-Path "version.json")) {
    Write-Error "version.json not found!"
    Write-Host "Nerdbank.GitVersioning requires version.json in the repository root."
    exit 2
}

$versionJson = Get-Content "version.json" | ConvertFrom-Json
$expectedVersion = $versionJson.version

Write-Success "version.json found"
Write-Host "  Expected version: $expectedVersion" -ForegroundColor Cyan
Write-Host ""

# Check 2: Find .csproj files with hardcoded versions
Write-Info "Scanning .csproj files..."

$csprojFiles = Get-ChildItem -Recurse -Filter "*.csproj" | Where-Object {
    $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\"
}

$filesWithHardcodedVersions = @()

foreach ($file in $csprojFiles) {
    $content = Get-Content $file.FullName -Raw

    if ($content -match '<Version>([^<]+)</Version>') {
        $hardcodedVersion = $matches[1]
        $filesWithHardcodedVersions += [PSCustomObject]@{
            File = $file.FullName.Replace($PWD.Path, '.').TrimStart('\').TrimStart('/')
            Version = $hardcodedVersion
        }
    }
}

Write-Host ""

# Report results
if ($filesWithHardcodedVersions.Count -eq 0) {
    Write-Success "All .csproj files are clean!"
    Write-Host "  No hardcoded versions found." -ForegroundColor Green
    Write-Host "  Nerdbank.GitVersioning will manage all versions from version.json" -ForegroundColor Green
    Write-Host ""
    exit 0
}

# Hardcoded versions found
Write-Error "Found $($filesWithHardcodedVersions.Count) .csproj file(s) with hardcoded versions:"
Write-Host ""

foreach ($file in $filesWithHardcodedVersions) {
    Write-Host "  📄 $($file.File)" -ForegroundColor Red
    Write-Host "     Hardcoded version: $($file.Version)" -ForegroundColor Yellow
    Write-Host "     Expected: Managed by Nerdbank.GitVersioning ($expectedVersion + build height)" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host "═══════════════════════════════════════" -ForegroundColor Red
Write-Host ""

if ($Fix) {
    Write-Info "Auto-fix enabled. Running fix-version-alignment.ps1..."
    Write-Host ""

    if (Test-Path "fix-version-alignment.ps1") {
        & "./fix-version-alignment.ps1"

        Write-Host ""
        Write-Success "Auto-fix completed. Re-validating..."
        Write-Host ""

        # Re-run validation
        & $PSCommandPath
        exit $LASTEXITCODE
    } else {
        Write-Error "fix-version-alignment.ps1 not found!"
        Write-Host "Download it from: https://github.com/tkuchel/DropBear.Codex/blob/master/fix-version-alignment.ps1"
        exit 2
    }
} else {
    Write-Warning "How to fix:"
    Write-Host ""
    Write-Host "  Option 1 - Automatic fix:" -ForegroundColor Cyan
    Write-Host "    .\validate-version-alignment.ps1 -Fix"
    Write-Host ""
    Write-Host "  Option 2 - Manual fix:" -ForegroundColor Cyan
    Write-Host "    .\fix-version-alignment.ps1"
    Write-Host ""
    Write-Host "  Option 3 - Manual editing:" -ForegroundColor Cyan
    Write-Host "    Remove <Version>...</Version> from the .csproj files listed above"
    Write-Host ""
    Write-Info "Why this matters:"
    Write-Host "  • Hardcoded versions override Nerdbank.GitVersioning"
    Write-Host "  • This causes version misalignment between packages"
    Write-Host "  • version.json should be the single source of truth"
    Write-Host ""

    exit 1
}
