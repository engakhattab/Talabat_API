param(
    [switch]$Json
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
$tasksTemplate = Join-Path $repoRoot ".specify/templates/tasks-template.md"

if (-not (Test-Path $featureDir)) {
    throw "Feature directory does not exist: $featureDir"
}

if (-not (Test-Path (Join-Path $featureDir "plan.md"))) {
    throw "Missing plan.md in feature directory. Run /speckit-plan first."
}

if (-not (Test-Path (Join-Path $featureDir "spec.md"))) {
    throw "Missing spec.md in feature directory. Run /speckit-specify first."
}

$availableDocs = @()
foreach ($candidate in @("research.md", "data-model.md", "quickstart.md", "contracts")) {
    if (Test-Path (Join-Path $featureDir $candidate)) {
        $availableDocs += $candidate
    }
}

$payload = [ordered]@{
    FEATURE_DIR = $featureDir
    TASKS_TEMPLATE = $tasksTemplate
    AVAILABLE_DOCS = $availableDocs
}

if ($Json) {
    $payload | ConvertTo-Json -Compress
}
else {
    $payload.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
}
