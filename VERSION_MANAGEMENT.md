# Version Management Strategy

## Overview

DropBear.Codex uses **Nerdbank.GitVersioning** for automatic, deterministic version management. This document explains how versions are managed and how to avoid common pitfalls.

## Single Source of Truth

**version.json** is the ONLY place where versions should be defined.

```json
{
  "version": "2025.10"
}
```

This means:
- ✅ **Major.Minor**: `2025.10` (defined in version.json)
- ✅ **Patch**: Auto-calculated from git commit height
- ✅ **Final version**: `2025.10.47` (where 47 is commit height)

## ⚠️ DO NOT Add Hardcoded Versions

**NEVER** add `<Version>` elements to .csproj files:

```xml
<!-- ❌ WRONG -->
<PropertyGroup>
    <Version>2025.10.0</Version>  <!-- Don't do this! -->
</PropertyGroup>

<!-- ✅ CORRECT -->
<PropertyGroup>
    <!-- No <Version> element - let Nerdbank.GitVersioning handle it -->
</PropertyGroup>
```

## How Nerdbank.GitVersioning Works

1. **Reads version.json**
   - Gets base version (e.g., "2025.10")

2. **Calculates commit height**
   - Counts commits since version was set
   - Example: 47 commits since version changed

3. **Generates final version**
   - Combines: `2025.10.47`
   - Applies to ALL projects automatically

4. **Benefits**:
   - Deterministic: Same commit = same version
   - Automatic: No manual version bumps
   - Consistent: All packages get same version
   - Traceable: Version maps to specific commit

## Version Format

```
vYYYY.MM.patch
```

Examples:
- `v2025.10.0` - October 2025, first release
- `v2025.10.47` - October 2025, 47 commits since 2025.10 was set
- `v2025.11.0` - November 2025, version bumped, commit height reset

## Validation Tools

### validate-version-alignment.ps1

Checks that no .csproj files have hardcoded versions.

```powershell
# Check for issues
.\validate-version-alignment.ps1

# Auto-fix if issues found
.\validate-version-alignment.ps1 -Fix
```

**Exit Codes:**
- `0` - All validations passed
- `1` - Hardcoded versions found
- `2` - Script execution error

### fix-version-alignment.ps1

Removes hardcoded versions from .csproj files.

```powershell
# Preview what would change
.\fix-version-alignment.ps1 -DryRun

# Actually fix the files
.\fix-version-alignment.ps1
```

## CI/CD Integration

The CI pipeline automatically validates version alignment:

```yaml
- name: Validate Version Alignment
  shell: pwsh
  run: |
    ./validate-version-alignment.ps1
    if ($LASTEXITCODE -ne 0) {
      exit 1
    }
```

This prevents hardcoded versions from being merged.

## Updating Versions

### For Monthly Releases

1. **Edit version.json**:
   ```json
   {
     "version": "2025.11"  // Changed from 2025.10
   }
   ```

2. **Commit the change**:
   ```bash
   git add version.json
   git commit -m "chore: Bump version to 2025.11"
   git push origin master
   ```

3. **Create release tag**:
   ```bash
   .\create-release-tag.ps1 -Version "2025.11.0"
   ```

The commit height resets to 0 when you change the version in version.json.

### For Patch Releases

**Don't** manually change anything. The patch version auto-increments with each commit:

- Commit 1 after version bump: `2025.11.1`
- Commit 2 after version bump: `2025.11.2`
- Commit 47 after version bump: `2025.11.47`

## Common Scenarios

### Scenario 1: "My package shows wrong version"

**Problem**: .csproj has hardcoded `<Version>` that overrides Nerdbank.GitVersioning.

**Solution**:
```powershell
.\fix-version-alignment.ps1
git add .
git commit -m "chore: Remove hardcoded versions"
```

### Scenario 2: "Different packages have different versions"

**Problem**: Some .csproj files have hardcoded versions, others don't.

**Solution**: Same as Scenario 1 - remove ALL hardcoded versions.

### Scenario 3: "CI validation fails"

**Problem**: Someone committed .csproj with hardcoded version.

**Solution**:
```powershell
# Locally:
.\validate-version-alignment.ps1 -Fix
git add .
git commit -m "fix: Remove hardcoded versions for CI"
git push
```

### Scenario 4: "I want to skip validation (emergency)"

**Not recommended**, but if absolutely necessary:

```bash
# Temporarily disable CI validation
# Edit .github/workflows/ci.yml and comment out validation step
```

**Better approach**: Use the fix script instead of skipping validation.

## Version History

You can see version history in git:

```bash
# See all version changes
git log --oneline version.json

# See what version was used at a specific commit
git show <commit>:version.json
```

## Package Metadata

Version is automatically applied to package metadata:

```xml
<!-- Generated automatically by Nerdbank.GitVersioning -->
<PackageVersion>2025.10.47</PackageVersion>
<AssemblyVersion>2025.10.0.0</AssemblyVersion>
<FileVersion>2025.10.47</FileVersion>
<InformationalVersion>2025.10.47+abc1234</InformationalVersion>
```

The `+abc1234` suffix is the git commit SHA (first 7 chars).

## Troubleshooting

### "MSBuild reports wrong version"

```bash
# Check what Nerdbank.GitVersioning sees
dotnet nbgv get-version

# Expected output:
# Version:                      2025.10.47
# AssemblyVersion:              2025.10.0
# AssemblyFileVersion:          2025.10.47
# NuGetPackageVersion:          2025.10.47
```

### "Validation script reports false positives"

The validation script looks for ANY `<Version>` element in .csproj files. If you see false positives:

1. Check the reported file
2. Verify it's not in a test/example project
3. If legitimate, update validation script exclusion list

### "Build shows different version than expected"

Common causes:
1. Shallow git clone (missing commit history)
   - Solution: Use `git clone` with full history
2. Detached HEAD state
   - Solution: Check out a branch
3. Uncommitted changes to version.json
   - Solution: Commit or stash changes

## References

- [Nerdbank.GitVersioning Documentation](https://github.com/dotnet/Nerdbank.GitVersioning)
- [version.json Schema](https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json)
- [RELEASE.md](RELEASE.md) - Complete release process
- [Directory.Build.props](Directory.Build.props) - Version management configuration

## Quick Reference

```powershell
# Validate versions
.\validate-version-alignment.ps1

# Fix issues
.\fix-version-alignment.ps1

# Check current version
dotnet nbgv get-version

# Update to new version
# 1. Edit version.json
# 2. Commit and push
# 3. Tag release

# Create release tag
.\create-release-tag.ps1 -Version "2025.11.0"
```

## Architecture Diagram

```
version.json (2025.10)
    ↓
Nerdbank.GitVersioning
    ↓
Git Commit Height (47)
    ↓
Final Version (2025.10.47)
    ↓
Applied to ALL .csproj files automatically
    ↓
NuGet Packages (DropBear.Codex.Core 2025.10.47)
```

---

**Last Updated**: 2025-10-29
**Maintainer**: DropBear.Codex Team
