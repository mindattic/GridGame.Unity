# Builds a player. Windows default: the development player (with the automation endpoint compiled in).
#   pwsh tools/build.ps1 [-Release] [-Target|-Platform Windows|Android|WebGL] [-Dev] [-SkipProse]
# Android (GG-T478, docs/RELEASE_BUILD.md): the Google Play bundle Build/AndroidRelease/GridGame.aab (IL2CPP, ARM64,
# version code = commit count), signed with the upload key named in the git-ignored android-signing.env (or GG_ANDROID_*
# environment variables), debug-signed when there is none. -Dev instead builds the emulator APK
# Build/Android/GridGame.apk (ARM64 + x86_64, development, debug-signed; tools/androidcheck.py).
# GG-T355: first pulls the latest published story text from Prose into Content/Text (tools/prose-sync.ps1); with the
# Hub offline it warns and builds with the committed snapshot. -SkipProse (or GG_SKIP_PROSE=1) skips the pull.
param(
    [switch]$Release,
    [Alias("Platform")]
    [ValidateSet("Windows", "Android", "WebGL")]
    [string]$Target = "Windows",
    [switch]$Dev,
    [switch]$SkipProse
)
& "$PSScriptRoot/prose-sync.ps1" -Skip:$SkipProse
# The Unity-side pre-build hook (GridGameBuild.SyncProse) sees this and does not pull a second time.
$env:GG_PROSE_SYNCED = "1"

if ($Target -eq "Android" -and -not $Dev) {
    # Signing secrets live only in this git-ignored file or the environment; they reach Unity as environment variables
    # for this one process and are never written to the project.
    $signing = Join-Path (Split-Path -Parent $PSScriptRoot) "android-signing.env"
    if (Test-Path $signing) {
        foreach ($line in Get-Content $signing) {
            if ($line -match '^\s*(GG_ANDROID_[A-Z_]+)\s*=\s*(.*?)\s*$') {
                Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
            }
        }
        Write-Host "android-signing.env loaded"
    }
    if (-not $env:GG_ANDROID_KEYSTORE) {
        Write-Warning "No GG_ANDROID_KEYSTORE: the release build will stop. Set GG_ANDROID_ALLOW_DEBUG_SIGNING=1 for a local DEBUG-signed test bundle that Google Play will refuse (docs/RELEASE_BUILD.md)."
    }
}

$method = switch ($Target) {
    "Android" { if ($Dev) { "GridGame.EditorTools.GridGameBuild.BuildAndroid" } else { "GridGame.EditorTools.GridGameBuild.BuildAndroidRelease" } }
    "WebGL" { "GridGame.EditorTools.GridGameBuild.BuildWebGL" }
    "Windows" { if ($Release) { "GridGame.EditorTools.GridGameBuild.BuildWindowsRelease" } else { "GridGame.EditorTools.GridGameBuild.BuildWindowsDev" } }
}
& "$PSScriptRoot/unity.ps1" -Method $method -Log "build-$Target"
exit $LASTEXITCODE
