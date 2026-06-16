<#
.SYNOPSIS
  Runs one matrix cell: all 17 crash scenarios for a given runtime + configuration.

.DESCRIPTION
  Assumes the app for this cell is ALREADY built, deployed, and launchable on the connected
  device (see SKILL.md step 2 for the build/deploy commands, including the Release host-pack
  workaround). For each scenario it calls drive-scenario.ps1 to trigger + capture, then
  analyze-crash.cs to produce a deterministic verdict. Writes cell-summary.md.

  Set DOTNET_ROOT / PATH for the matching SDK before calling, or pass -DotnetRoot.

.PARAMETER Runtime  mono | coreclr (ground-truth axis; must match what was built).
.PARAMETER Config   Debug | Release.
.PARAMETER RunRoot  Root run directory; the cell writes to <RunRoot>/<runtime>-<config>/.
.PARAMETER DotnetRoot  Optional SDK root to prepend to PATH (overrides ambient DOTNET_ROOT).
.PARAMETER Package  App id (passed through to drive-scenario.ps1).
#>
param(
  [Parameter(Mandatory)][ValidateSet('mono','coreclr')][string]$Runtime,
  [Parameter(Mandatory)][ValidateSet('Debug','Release')][string]$Config,
  [Parameter(Mandatory)][string]$RunRoot,
  [string]$DotnetRoot = $env:DOTNET_ROOT,
  [string]$Package = 'codes.redth.mauidiagnosticsgallery'
)
$ErrorActionPreference = 'Continue'
if ($DotnetRoot) {
  $env:DOTNET_ROOT = $DotnetRoot
  $env:PATH = "$DotnetRoot;$env:PATH"
}

$cell = "$Runtime-$Config"
$cellDir = Join-Path $RunRoot $cell
New-Item -ItemType Directory -Force -Path $cellDir | Out-Null
$drive = Join-Path $PSScriptRoot 'drive-scenario.ps1'
$analyze = Join-Path $PSScriptRoot 'analyze-crash.cs'

# Title | Key | Special | SettleSeconds
$scenarios = @(
  @('Unhandled managed exception','managed-ui-unhandled','none',3),
  @('Background thread exception','managed-background-unhandled','none',3),
  @('Async void exception','managed-async-void','none',3),
  @('NullReferenceException','runtime-null-reference','none',3),
  @('Access violation','runtime-access-violation','none',3),
  @('Environment.FailFast','runtime-failfast','none',3),
  @('Stack overflow','runtime-stack-overflow','none',4),
  @('Android native abort','native-android-abort','none',3),
  @('Android native SIGSEGV','native-android-sigsegv','none',3),
  @('Native illegal instruction','native-illegal-instruction','none',3),
  @('Background native thread crash','native-background-thread','none',3),
  @('Mixed managed/native stack','mixed-managed-native','none',3),
  @('UI thread hang','resource-ui-hang','none',14),
  @('Vendor handled exception','vendor-handled-exception','none',3),
  @('Out-of-memory pressure','resource-memory-pressure','none',20),
  @('Startup lifecycle crash','lifecycle-startup-crash','startup',3),
  @('Resume lifecycle crash','lifecycle-resume-crash','resume',3)
)

$summary = New-Object System.Collections.Generic.List[string]
foreach ($s in $scenarios) {
  $title, $key, $special, $settle = $s
  Write-Host ">>> [$cell] $key ($title)" -ForegroundColor Cyan
  try {
    & $drive -Title $title -Key $key -OutDir $cellDir -Special $special -SettleSeconds $settle -Package $Package
  } catch {
    Write-Host "  drive error: $_" -ForegroundColor Yellow
  }
  $logcat = Join-Path $cellDir "logcat-$key.log"
  $proc   = Join-Path $cellDir "process-$key.log"
  $verdict = Join-Path $cellDir "verdict-$key.json"
  if (-not (Test-Path $proc)) { '' | Set-Content $proc }
  if (Test-Path $logcat) {
    $line = dotnet run $analyze -- --scenario $key --runtime $Runtime --config $Config --process $proc --logcat $logcat --out $verdict 2>&1 |
      Where-Object { $_ -match '^\|' } | Select-Object -Last 1
    if ($line) { $summary.Add([string]$line) } else { $summary.Add("| $key | $cell | ERR | analyze-failed |") }
  } else {
    $summary.Add("| $key | $cell | ERR | no-logcat |")
  }
}

# recover the device after the memory scenario
adb shell am force-stop $Package *> $null

$summary | Set-Content (Join-Path $cellDir 'cell-summary.md')
Write-Host "===== $cell summary =====" -ForegroundColor Green
$summary | ForEach-Object { Write-Host $_ }
