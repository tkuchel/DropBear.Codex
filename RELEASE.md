# Release Process

This document describes how to create and publish releases for DropBear.Codex.

## Version Scheme

DropBear.Codex uses **calendar versioning** with Nerdbank.GitVersioning:
- Format: `YYYY.MM.patch`
- Example: `2025.10.0`, `2025.10.1`, `2025.11.0`

Current version is defined in [`version.json`](version.json).

## Prerequisites

- Write access to the repository
- `NUGET_API_KEY` secret configured in GitHub repository settings
- Clean working directory (no uncommitted changes)
- On `master` branch with latest changes pulled

## Release Checklist

### 1. Pre-Release Verification

- [ ] All CI workflows passing on `master` branch
- [ ] All intended changes merged from `develop` to `master`
- [ ] `CHANGELOG.md` updated with release notes under `[Unreleased]`
- [ ] Security issues documented in `SECURITY.md`
- [ ] Breaking changes clearly documented

### 2. Update Version

Edit `version.json` to set the new version:

```json
{
  "version": "2025.11"  // Update this
}
```

**Important:** Only update the `version` field. Do not modify other settings.

### 3. Update CHANGELOG

Move changes from `[Unreleased]` to a new version section:

```markdown
## [Unreleased]

(Leave empty for future changes)

## [2025.11.0] - 2025-11-15

### Added
- Feature descriptions...

### Changed
- Change descriptions...

### Fixed
- Fix descriptions...
```

### 4. Commit Version Bump

```bash
git add version.json CHANGELOG.md
git commit -m "chore: Bump version to 2025.11.0"
git push origin master
```

### 5. Create and Push Tag

**Option A: Using the Helper Script (Recommended)**

```powershell
# From repository root
.\create-release-tag.ps1 -Version "2025.11.0"
```

The script will:
- Validate version format
- Verify you're on master branch
- Check working directory is clean
- Create annotated tag with changelog
- Push tag to trigger release workflow

**Option B: Manual Process**

```bash
# Verify you're on master
git checkout master
git pull origin master

# Create annotated tag (use v prefix!)
git tag -a v2025.11.0 -m "Release 2025.11.0

## Highlights
- Feature 1
- Feature 2
- Security fix

See CHANGELOG.md for full details."

# Verify tag
git tag -l "v2025.11.0"
git show v2025.11.0

# Push tag to trigger release workflow
git push origin v2025.11.0
```

### 6. Monitor Release Workflow

1. Go to [Actions](https://github.com/tkuchel/DropBear.Codex/actions)
2. Watch the "Release (NuGet)" workflow
3. Verify all steps complete successfully:
   - ✅ Build
   - ✅ Pack
   - ✅ Validate Packages
   - ✅ Publish to NuGet
   - ✅ Create GitHub Release

### 7. Verify Publication

**NuGet.org:**
- Check packages are live: https://www.nuget.org/packages?q=DropBear.Codex
- Verify all projects published
- Check package metadata is correct

**GitHub Release:**
- Verify release created: https://github.com/tkuchel/DropBear.Codex/releases
- Confirm release notes populated
- Check assets attached (if any)

### 8. Post-Release

- [ ] Announce release (if needed)
- [ ] Update dependent projects
- [ ] Monitor for issues

## Tag Format

**Correct:**
```bash
v2025.10.0   ✅ Correct
v2025.11.0   ✅ Correct
v2026.1.0    ✅ Correct
```

**Incorrect:**
```bash
2025.10.0    ❌ Missing 'v' prefix
v2025.10     ❌ Missing patch version
v2025.10.0.1 ❌ Too many version parts
release-2025 ❌ Wrong format
```

## Version Increment Rules

### Major Version (Year)
Increment when the calendar year changes:
- `2025.12.5` → `2026.1.0`

### Minor Version (Month)
Increment for monthly feature releases:
- `2025.10.0` → `2025.11.0`

### Patch Version (Build)
Auto-incremented by Nerdbank.GitVersioning based on commit height.
Manually set to 0 when bumping year or month:
- `2025.10.5` → `2025.11.0` (new month, reset to 0)

## Rollback a Release

If a release has critical issues:

### 1. Unlist Packages (Immediately)

```bash
# Install dotnet CLI if needed
dotnet tool install -g dotnet-nuget-unlister

# Unlist the broken version
dotnet nuget delete DropBear.Codex.Core 2025.11.0 --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json --non-interactive
```

**Note:** This doesn't delete the package, just hides it from search results.

### 2. Fix Issues

Create hotfix branch, fix issues, test thoroughly.

### 3. Release Hotfix

Bump patch version and follow normal release process:
- `2025.11.0` (broken) → `2025.11.1` (fixed)

## Troubleshooting

### Tag Already Exists

```bash
# Delete local tag
git tag -d v2025.11.0

# Delete remote tag (CAUTION!)
git push origin :refs/tags/v2025.11.0

# Recreate tag correctly
git tag -a v2025.11.0 -m "Release 2025.11.0"
git push origin v2025.11.0
```

### Release Workflow Failed

1. Check workflow logs in GitHub Actions
2. Common issues:
   - `NUGET_API_KEY` not configured or expired
   - Network issues with NuGet.org
   - Package validation failed
   - Build errors

3. Fix issue and re-run workflow or delete tag and recreate

### Wrong Version Published

1. Unlist the incorrect version on NuGet.org
2. Fix version.json
3. Create new tag with correct version
4. Publish corrected version

## Security Releases

For security fixes:

1. **Do not** publicly discuss vulnerability before release
2. Prepare fix in private branch
3. Follow normal release process
4. After publication, update SECURITY.md
5. Publish security advisory on GitHub

## Emergency Hotfix Process

For critical production issues:

```bash
# 1. Create hotfix branch from master
git checkout master
git pull origin master
git checkout -b hotfix/v2025.11.1

# 2. Apply fix and test
# ... make changes ...
git add .
git commit -m "fix: Critical issue description"

# 3. Merge to master
git checkout master
git merge hotfix/v2025.11.1
git push origin master

# 4. Follow normal release process
.\create-release-tag.ps1 -Version "2025.11.1"

# 5. Merge back to develop
git checkout develop
git merge master
git push origin develop
```

## Related Documentation

- [CHANGELOG.md](CHANGELOG.md) - Release history
- [SECURITY.md](SECURITY.md) - Security policy and known issues
- [CONTRIBUTING.md](CONTRIBUTING.md) - Development guidelines
- [version.json](version.json) - Version configuration

## GitHub Actions Workflows

- **[ci.yml](.github/workflows/ci.yml)**: Runs on every push/PR
- **[release.yml](.github/workflows/release.yml)**: Triggered by version tags (`v*`)
- **[codeql.yml](.github/workflows/codeql.yml)**: Security analysis

## Questions?

- Check existing [releases](https://github.com/tkuchel/DropBear.Codex/releases)
- Review [GitHub Actions](https://github.com/tkuchel/DropBear.Codex/actions)
- Open an [issue](https://github.com/tkuchel/DropBear.Codex/issues)
