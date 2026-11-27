# Android release pipeline (GG-T1513): release_lint, the release AAB build, the bundletool size gate, check_aab.py
# (64-bit, 16 KB, permissions), the target-SDK log check and release notes; then Build/AndroidRelease/release.json
# (version, buildNumber, sizes, sha256, notes: what tools/play_upload.py reads) and a v<version>+<build> git tag.
#   pwsh tools/release.ps1 [-DryRun] [-SkipProse] [-MaxMB 190]
# -DryRun skips the build and the tag; the lint, release notes (printed, not written) and, when a bundle is already in
# Build/AndroidRelease, the size gate and check_aab run against it, and release.json is not written.
param(
    [switch]$DryRun,
    [switch]$SkipProse,
    [int]$MaxMB = 190
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$out = Join-Path $root "Build/AndroidRelease"
$aab = Join-Path $out "GridGame.aab"
$requiredSdk = 36

function Invoke-Native([string]$Exe, [string[]]$Arguments) {
    $o = & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe $($Arguments -join ' ') failed (exit $LASTEXITCODE)" }
    $o
}

function Find-AndroidTools {
    $dirs = Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer" -Directory -ErrorAction SilentlyContinue | Sort-Object FullName -Descending
    foreach ($d in $dirs) {
        $jar = Get-ChildItem (Join-Path $d.FullName "Tools") -Filter "bundletool-all-*.jar" -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        $java = Join-Path $d.FullName "OpenJDK/bin/java.exe"
        if ($jar -and (Test-Path $java)) { return @{ Java = $java; Jar = $jar.FullName } }
    }
    throw "bundletool jar and Unity's JDK not found under C:\Program Files\Unity\Hub\Editor"
}

$version = (Get-Content (Join-Path $root "version.txt") -Raw).Trim()
if (-not $version) { throw "version.txt is empty" }

Write-Host "== release_lint"
Invoke-Native python @("tools/release_lint.py") | Write-Host

if ($DryRun) {
    $build = [int](git rev-list --count HEAD)
    Write-Host "== release notes (dry run, nothing written)"
    Invoke-Native python @("tools/release_notes.py", "--print") | Write-Host
}
else {
    Write-Host "== build"
    & "$PSScriptRoot/build.ps1" -Target Android -SkipProse:$SkipProse
    if ($LASTEXITCODE -ne 0) { throw "Android release build failed (exit $LASTEXITCODE)" }

    $log = Join-Path $root "Logs/build-Android.log"
    $sdk = Select-String -Path $log -Pattern "\[Build\] Android: min SDK \d+, target SDK (\d+)" | Select-Object -Last 1
    if (-not $sdk) { throw "no '[Build] Android: min SDK ..., target SDK ...' line in $log" }
    $target = [int]$sdk.Matches[0].Groups[1].Value
    if ($target -lt $requiredSdk) { throw "target SDK $target is below Play's required $requiredSdk" }
    Write-Host "target SDK $target (required $requiredSdk)"

    $ver = Select-String -Path $log -Pattern "\[Build\] Android: version (\S+) \((\d+)\)" | Select-Object -Last 1
    if (-not $ver) { throw "no '[Build] Android: version ... (code)' line in $log" }
    $version = $ver.Matches[0].Groups[1].Value
    $build = [int]$ver.Matches[0].Groups[2].Value

    Write-Host "== release notes"
    Invoke-Native python @("tools/release_notes.py") | Write-Host
}

$sizes = $null
$sha = $null
if (Test-Path $aab) {
    $tools = Find-AndroidTools
    Write-Host "== size gate (limit $MaxMB MB of Play's 200 MB)"
    $apks = Join-Path $out "size-check.apks"
    Remove-Item $apks -ErrorAction SilentlyContinue
    Invoke-Native $tools.Java @("-jar", $tools.Jar, "build-apks", "--bundle=$aab", "--output=$apks", "--overwrite") | Out-Null
    $total = Invoke-Native $tools.Java @("-jar", $tools.Jar, "get-size", "total", "--apks=$apks")
    Remove-Item $apks -ErrorAction SilentlyContinue
    $nums = @($total | Select-String -Pattern "^\d+,\d+$" | Select-Object -Last 1)
    if (-not $nums) { throw "could not read bundletool get-size output: $total" }
    $minB, $maxB = ($nums[0].Line -split ",") | ForEach-Object { [long]$_ }
    $sizes = [ordered]@{ aabBytes = (Get-Item $aab).Length; downloadMinBytes = $minB; downloadMaxBytes = $maxB }
    Write-Host ("aab {0:N1} MB, download {1:N1} to {2:N1} MB" -f ($sizes.aabBytes / 1MB), ($minB / 1MB), ($maxB / 1MB))
    if ($maxB -gt $MaxMB * 1MB) { throw ("download size {0:N1} MB is over the {1} MB gate" -f ($maxB / 1MB), $MaxMB) }

    Write-Host "== check_aab"
    Invoke-Native python @("tools/check_aab.py", $aab) | Write-Host
    $sha = (Get-FileHash $aab -Algorithm SHA256).Hash.ToLower()
}
elseif ($DryRun) {
    Write-Host "dry run: no bundle at $aab, size gate and check_aab skipped"
}
else { throw "build finished but $aab is missing" }

$tag = "v$version+$build"
if ($DryRun) {
    Write-Host "dry run: would write release.json and tag $tag"
    exit 0
}

$notesFile = Join-Path $root "docs/release/whatsnew-play.txt"
$notes = if (Test-Path $notesFile) { (Get-Content $notesFile -Raw).Trim() } else { "" }
$release = [ordered]@{
    version     = $version
    buildNumber = $build
    commit      = (git rev-parse HEAD).Trim()
    builtAt     = (Get-Date).ToUniversalTime().ToString("o")
    sizes       = $sizes
    sha256      = $sha
    notes       = $notes
}
$release | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out "release.json") -Encoding utf8
git tag $tag
if ($LASTEXITCODE -ne 0) { throw "git tag $tag failed" }
Write-Host "release.json written, tagged $tag (push it with: git push origin $tag)"
