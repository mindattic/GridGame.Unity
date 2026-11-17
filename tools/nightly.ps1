# GridGame Gaiden nightly run (owner 2026-11-17: "keep coding and building, leave the tests for overnight; a
# MindAttic.Scheduler job that does all the logging and telemetry after 2am every night").
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\nightly.ps1 [-SkipBuild] [-Encounters 30] [-NoTickets]
#
# Registered as the MindAttic.Scheduler manifest job "GridGame.Nightly" (its jobs.json, daily 02:00, task
# \MindAttic\Scheduler\GridGame.Nightly).
# This is the one place outside an owner /test turn where the suites and soaks run (the golden rule's "tests run
# last, once, when the user asks": the owner asked for them nightly). Everything lands in artifacts\nightly\<date>\:
#
#   1. suite    - a clean copy of origin/main (a throwaway worktree under %LOCALAPPDATA%, removed after; the shared
#                 checkout is never touched): dotnet test (trx), check_glyphs, check_assets.
#   2. balance  - tools\polish.py --balance in that copy: the greedy + casual bot soaks and the polish scorecard.
#   3. play     - only when no Unity Editor/player is running in the shared checkout: a dev build of main and a
#                 telemetry run of N endless encounters (harness.py endless), then telemetry_report.py on it.
#   4. tickets  - one fix ticket per failing test that has no open ticket naming it yet (skip with -NoTickets).
#   5. summary  - summary.md: what ran, pass/fail counts, failing tests, report paths, how long each step took.
#
# Each step is time-boxed, so a hung step never blocks the next night (the Scheduler's IgnoreNew would).
param(
    [switch]$SkipBuild,
    [int]$Encounters = 30,
    [switch]$NoTickets
)
$ErrorActionPreference = "Continue"
$repo = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format "yyyy-MM-dd"
$out = Join-Path $repo "artifacts\nightly\$stamp"
New-Item -ItemType Directory -Force $out | Out-Null
$log = Join-Path $out "nightly.log"
$env:PYTHONIOENCODING = "utf-8"
$env:GG_SKIP_PROSE = "1"
$summary = [ordered]@{}
$failing = @()

function Write-Log([string]$line) {
    $l = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $line
    Add-Content -Path $log -Value $l -Encoding utf8
    Write-Output $l
}

# Runs a command line with a time limit, its output to <name>.log; returns the exit code (124 on timeout).
function Invoke-Step([string]$name, [string]$file, [string[]]$arguments, [string]$dir, [int]$minutes) {
    $stepLog = Join-Path $out "$name.log"
    $t0 = Get-Date
    Write-Log "step $name : $file $($arguments -join ' ')"
    $p = Start-Process -FilePath $file -ArgumentList $arguments -WorkingDirectory $dir -NoNewWindow -PassThru `
        -RedirectStandardOutput $stepLog -RedirectStandardError "$stepLog.err"
    if (-not $p.WaitForExit($minutes * 60 * 1000)) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch {}
        $code = 124
    } else { $code = $p.ExitCode }
    $secs = [int]((Get-Date) - $t0).TotalSeconds
    Write-Log "step $name : exit $code in ${secs}s"
    $summary[$name] = @{ exit = $code; seconds = $secs; log = $stepLog }
    return $code
}

Write-Log "nightly start ($repo)"

# ── 1. suite on a clean origin/main ─────────────────────────────────────────────────────────────────────────
$wt = Join-Path $env:LOCALAPPDATA "MindAttic\GridGameNightly\main"
git -C $repo fetch -q origin main 2>&1 | Out-Null
if (Test-Path $wt) { git -C $repo worktree remove --force $wt 2>&1 | Out-Null; Remove-Item -Recurse -Force $wt -ErrorAction SilentlyContinue }
git -C $repo worktree prune 2>&1 | Out-Null
git -C $repo worktree add --detach -q $wt origin/main 2>&1 | Out-Null
$head = (git -C $wt rev-parse --short HEAD).Trim()
Write-Log "suite copy at $wt ($head)"
# The suite links the sibling Unity.Shared source (Tests/GridGame.Core.csproj); a worktree sits elsewhere, so point
# that one relative path at the real sibling with a junction.
$shared = Join-Path (Split-Path -Parent $repo) "Unity.Shared"
$wtShared = Join-Path (Split-Path -Parent $wt) "Unity.Shared"
if ((Test-Path $shared) -and -not (Test-Path $wtShared)) { cmd /c mklink /J "$wtShared" "$shared" | Out-Null }

$trx = Join-Path $out "suite.trx"
Invoke-Step "suite" "dotnet" @("test", "Tests\Core.Tests\Core.Tests.csproj", "--logger", "trx;LogFileName=$trx", "--logger", "console;verbosity=minimal") $wt 120 | Out-Null
Invoke-Step "glyphs" "python" @("tools\check_glyphs.py") $wt 10 | Out-Null
Invoke-Step "assets" "python" @("tools\check_assets.py") $wt 15 | Out-Null

$passed = 0; $failed = 0
if (Test-Path $trx) {
    [xml]$x = Get-Content $trx -Raw
    $c = $x.TestRun.ResultSummary.Counters
    $passed = [int]$c.passed; $failed = [int]$c.failed
    $failing = @($x.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq "Failed" } | ForEach-Object {
        [pscustomobject]@{ Name = $_.testName; Message = (($_.Output.ErrorInfo.Message -as [string]) -replace '\s+', ' ').Trim() }
    })
}
Write-Log "suite: $passed passed, $failed failed"

# ── 2. balance soaks ────────────────────────────────────────────────────────────────────────────────────────
Invoke-Step "balance" "python" @("tools\polish.py", "--balance") $wt 180 | Out-Null
$polishOut = Get-ChildItem (Join-Path $wt "artifacts") -Recurse -Filter "polish*" -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in ".md", ".json", ".html" } | Sort-Object LastWriteTime -Descending | Select-Object -First 4
foreach ($f in $polishOut) { Copy-Item $f.FullName (Join-Path $out ("balance-" + $f.Name)) -Force }

# ── 3. build + endless telemetry (shared checkout, only when it is idle) ────────────────────────────────────
$busy = @(Get-Process Unity, GridGameGaiden -ErrorAction SilentlyContinue).Count -gt 0
if ($busy) {
    Write-Log "play: skipped (Unity or the player is running)"
    $summary["play"] = @{ exit = "skipped"; seconds = 0; log = "Unity or the player was running" }
} else {
    $built = 0
    if (-not $SkipBuild) { $built = Invoke-Step "build" "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "tools\build.ps1", "-SkipProse") $repo 30 }
    if ($built -eq 0) {
        $run = "nightly-endless-$stamp"
        Invoke-Step "endless" "python" @("tools\harness.py", "endless", "$Encounters", "--run", $run) $repo 150 | Out-Null
        Invoke-Step "telemetry" "python" @("tools\telemetry_report.py", $run) $repo 15 | Out-Null
        foreach ($n in "report.md", "report.html") {
            $r = Join-Path $repo "artifacts\endless\$run\$n"
            if (Test-Path $r) { Copy-Item $r (Join-Path $out "telemetry-$n") -Force }
        }
    }
}

# ── 4. a fix ticket per new failing test ────────────────────────────────────────────────────────────────────
$filed = @()
if (-not $NoTickets -and $failing.Count -gt 0) {
    $open = (tickets ticket list --repo Unity.GridGame --status open --format csv 2>$null) -join "`n"
    foreach ($f in $failing | Select-Object -First 25) {
        $short = ($f.Name -split '\.')[-1]
        if ($open -match [regex]::Escape($short)) { continue }
        $msg = if ($f.Message.Length -gt 300) { $f.Message.Substring(0, 300) + "..." } else { $f.Message }
        $id = (tickets ticket new --type fix --name "Nightly: $($short.Substring(0, [Math]::Min(40, $short.Length)))" `
            --summary "Test $short fails on main $head (nightly $stamp): $msg" --ref $short 2>$null | Select-Object -Last 1)
        if ($id) { $filed += "$id $short"; Write-Log "filed $id for $short" }
    }
}

# ── 5. summary ──────────────────────────────────────────────────────────────────────────────────────────────
$md = @("# GridGame nightly $stamp", "", "main at ``$head``. Suite: **$passed passed, $failed failed**.", "",
        "| Step | Exit | Seconds | Log |", "|---|---|---|---|")
foreach ($k in $summary.Keys) { $s = $summary[$k]; $md += "| $k | $($s.exit) | $($s.seconds) | $($s.log) |" }
if ($failing.Count) { $md += ""; $md += "## Failing tests"; $md += ""; foreach ($f in $failing) { $md += "- ``$($f.Name)``: $($f.Message)" } }
if ($filed.Count) { $md += ""; $md += "## Tickets filed"; $md += ""; foreach ($t in $filed) { $md += "- $t" } }
$md | Set-Content -Path (Join-Path $out "summary.md") -Encoding utf8

git -C $repo worktree remove --force $wt 2>&1 | Out-Null
git -C $repo worktree prune 2>&1 | Out-Null
Write-Log "nightly done: $out"
