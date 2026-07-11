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
$featureSpec = Join-Path $featureDir "spec.md"
$implPlan = Join-Path $featureDir "plan.md"
$planTemplate = Join-Path $repoRoot ".specify/templates/plan-template.md"

if (-not (Test-Path $featureSpec)) {
    throw "Feature specification does not exist: $featureSpec"
}

if (-not (Test-Path $planTemplate)) {
    throw "Plan template does not exist: $planTemplate"
}

if (-not (Test-Path $implPlan)) {
    Copy-Item -Path $planTemplate -Destination $implPlan
}

$branch = "not-created"
try {
    $gitBranch = git -C $repoRoot branch --show-current 2>$null
    if (-not [string]::IsNullOrWhiteSpace($gitBranch)) {
        $branch = $gitBranch.Trim()
    }
}
catch {
    $branch = "not-created"
}

$payload = [ordered]@{
    FEATURE_SPEC = $featureSpec
    IMPL_PLAN = $implPlan
    SPECS_DIR = $featureDir
    BRANCH = $branch
}

if ($Json) {
    $payload | ConvertTo-Json -Compress
}
else {
    $payload.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
}
