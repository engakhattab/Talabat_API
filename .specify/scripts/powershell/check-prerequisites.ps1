param(
    [switch]$Json,
    [switch]$PathsOnly,
    [switch]$RequireTasks,
    [switch]$IncludeTasks
)

$ErrorActionPreference = "Stop"

function Find-RepoRoot {
    $current = (Get-Location).Path
    while ($true) {
        if (Test-Path (Join-Path $current ".specify")) {
            return $current
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current -or [string]::IsNullOrWhiteSpace($parent)) {
            throw "Could not locate repository root containing .specify."
        }

        $current = $parent
    }
}

$repoRoot = Find-RepoRoot
$featureJsonPath = Join-Path $repoRoot ".specify/feature.json"

if (-not (Test-Path $featureJsonPath)) {
    throw "Missing .specify/feature.json. Run /speckit-specify first."
}

$featureConfig = Get-Content $featureJsonPath -Raw | ConvertFrom-Json
$featureDirectory = $featureConfig.feature_directory

if ([string]::IsNullOrWhiteSpace($featureDirectory)) {
    throw "Missing feature_directory in .specify/feature.json."
}

$featureDir = Join-Path $repoRoot $featureDirectory
$featureSpec = Join-Path $featureDir "spec.md"
$implPlan = Join-Path $featureDir "plan.md"
$tasks = Join-Path $featureDir "tasks.md"

if (-not (Test-Path $featureDir)) {
    throw "Feature directory does not exist: $featureDir"
}

if (-not (Test-Path $featureSpec)) {
    throw "Feature specification does not exist: $featureSpec"
}

if ($RequireTasks -and -not (Test-Path $tasks)) {
    throw "Tasks file does not exist: $tasks"
}

$availableDocs = @()
foreach ($candidate in @("research.md", "data-model.md", "quickstart.md", "contracts", "tasks.md")) {
    if (Test-Path (Join-Path $featureDir $candidate)) {
        $availableDocs += $candidate
    }
}

$payload = [ordered]@{
    FEATURE_DIR = $featureDir
    FEATURE_SPEC = $featureSpec
    IMPL_PLAN = $implPlan
    TASKS = $tasks
}

if (-not $PathsOnly) {
    $payload["AVAILABLE_DOCS"] = $availableDocs
}

if ($Json) {
    $payload | ConvertTo-Json -Compress
}
else {
    $payload.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
}
