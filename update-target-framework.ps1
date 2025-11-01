# Update all .csproj files to target .NET 10
$projects = @(
    "DropBear.Codex.Core\DropBear.Codex.Core.csproj",
    "DropBear.Codex.Blazor\DropBear.Codex.Blazor.csproj",
    "DropBear.Codex.Notifications\DropBear.Codex.Notifications.csproj",
    "DropBear.Codex.Hashing\DropBear.Codex.Hashing.csproj",
    "DropBear.Codex.Files\DropBear.Codex.Files.csproj",
    "DropBear.Codex.Serialization\DropBear.Codex.Serialization.csproj",
    "DropBear.Codex.Utilities\DropBear.Codex.Utilities.csproj",
    "DropBear.Codex.Tasks\DropBear.Codex.Tasks.csproj",
    "DropBear.Codex.Workflow\DropBear.Codex.Workflow.csproj",
    "DropBear.Codex.Benchmarks\DropBear.Codex.Benchmarks.csproj",
    "DropBear.Codex.Core.Tests\DropBear.Codex.Core.Tests.csproj",
    "DropBear.Codex.Files.Tests\DropBear.Codex.Files.Tests.csproj",
    "DropBear.Codex.Hashing.Tests\DropBear.Codex.Hashing.Tests.csproj",
    "DropBear.Codex.Serialization.Tests\DropBear.Codex.Serialization.Tests.csproj",
    "DropBear.Codex.Notifications.Tests\DropBear.Codex.Notifications.Tests.csproj",
    "DropBear.Codex.Tasks.Tests\DropBear.Codex.Tasks.Tests.csproj",
    "DropBear.Codex.Utilities.Tests\DropBear.Codex.Utilities.Tests.csproj",
    "DropBear.Codex.Workflow.Tests\DropBear.Codex.Workflow.Tests.csproj",
    "DropBear.Codex.Blazor.Tests\DropBear.Codex.Blazor.Tests.csproj"
)

$updated = 0
foreach ($project in $projects) {
    $path = Join-Path $PSScriptRoot $project
    if (Test-Path $path) {
        $content = Get-Content $path -Raw
        $newContent = $content -replace '<TargetFramework>net9\.0</TargetFramework>', '<TargetFramework>net10.0</TargetFramework>'
        if ($content -ne $newContent) {
            Set-Content $path -Value $newContent -NoNewline
            Write-Host "Updated: $project"
            $updated++
        }
    } else {
        Write-Host "Not found: $project" -ForegroundColor Yellow
    }
}

Write-Host "`nTotal updated: $updated projects" -ForegroundColor Green
