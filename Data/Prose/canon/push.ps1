# Pushes the staged GridGame canon story bible into Prose as UniverseCanon sections of universe "gridgame" (GG-T526).
# These files are a staging copy only: once the push succeeds, Prose is the canon. Edit it there, then delete this folder.
# Prose's database was paused (Azure free allowance) on 2026-11-21, so the push waits until the Hub can start.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sections = @(
    @{ Key = 'preamble';      Title = 'GridGame canon: the Somnolith';         File = '01-preamble.md' },
    @{ Key = 'GG-SOMNOLITH';  Title = 'The Somnolith';                     File = '02-somnolith.md' },
    @{ Key = 'GG-ONE-LIGHT';  Title = 'One light: the Somnolith and the people'; File = '03-one-light.md' },
    @{ Key = 'GG-WHY-NOW';    Title = 'Why it is coming now';              File = '04-whynow.md' },
    @{ Key = 'GG-PEOPLES';    Title = 'Peoples, powers and monsters';      File = '05-factions.md' },
    @{ Key = 'GG-ARC';        Title = 'Campaign arc and endings';          File = '06-arc.md' }
)
foreach ($s in $sections) {
    prose --set-canon-section --type UniverseCanon --universe gridgame --key $s.Key --title $s.Title --file (Join-Path $here $s.File)
    if ($LASTEXITCODE -ne 0) { throw "set-canon-section failed for $($s.Key)" }
}
prose --list-canon-sections --type UniverseCanon --universe gridgame
