#Requires -Version 5.1
<#
.SYNOPSIS
    GG-T547: publishes the Android development APK to C:\Apps\GridGame.Unity\ for local device emulation with the
    Android Emulator Suite (D:\Projects\MindAttic\Android.Emulator.Suite, deployed to C:\Apps\Android.Emulator.Suite),
    whose "GridGame" app entry installs C:\Apps\GridGame.Unity\GridGame.apk.

.DESCRIPTION
    -Build first runs tools\build.ps1 -Platform Android -Dev (IL2CPP, ARM64 + x86_64, development, debug-signed:
    docs/RELEASE_BUILD.md), which needs the Unity Editor closed and no other Unity batchmode on this project.
    Then copies Build\Android\GridGame.apk to C:\Apps\GridGame.Unity\ with a GridGame.txt beside it
    (version, version code, commit, build time) that the suite shows.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy-apk.ps1 -Build
    powershell -ExecutionPolicy Bypass -File tools\deploy-apk.ps1          # publish the APK already built
#>
param(
    [switch]$Build,
    [switch]$SkipProse,
    [string]$Out = 'C:\Apps\GridGame.Unity'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$apk = Join-Path $repo 'Build\Android\GridGame.apk'

if ($Build) {
    # One Unity at a time: a second batchmode on the same project fails on the project lock.
    $busy = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$repo*" }
    if ($busy) { Write-Host "Unity is already running on $repo (pid $($busy.ProcessId -join ', ')). Wait for it to finish, then run this again." -ForegroundColor Red; exit 1 }
    & (Join-Path $PSScriptRoot 'build.ps1') -Platform Android -Dev -SkipProse:$SkipProse
    if ($LASTEXITCODE -ne 0) { Write-Host "Android build failed (exit $LASTEXITCODE): see Logs\build-Android.log" -ForegroundColor Red; exit $LASTEXITCODE }
}
if (-not (Test-Path $apk)) { Write-Host "No APK at $apk. Run with -Build." -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Force $Out | Out-Null
Copy-Item -Force $apk (Join-Path $Out 'GridGame.apk')
$version = (Get-Content (Join-Path $repo 'version.txt') -Raw).Trim()
$commit = (git -C $repo rev-parse --short HEAD).Trim()
$count = (git -C $repo rev-list --count HEAD).Trim()
$built = (Get-Item $apk).LastWriteTime.ToString('yyyy-MM-dd HH:mm')
Set-Content -Encoding UTF8 -Path (Join-Path $Out 'GridGame.txt') -Value "GridGame $version, development APK (ARM64 + x86_64), commit $commit (#$count), built $built"
Write-Host "Published $apk -> $Out\GridGame.apk ($([math]::Round((Get-Item $apk).Length / 1MB)) MB)" -ForegroundColor Green
Write-Host "Play it on an emulated device: C:\Apps\Android.Emulator.Suite\AndroidEmulatorSuite.exe"
