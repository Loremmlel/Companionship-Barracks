#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$installer = Join-Path $repo 'Install-Mod.ps1'
. $installer
function Assert($condition, [string]$message) { if (!$condition) { throw $message }; Write-Host "PASS $message" }
$fixture = Join-Path $repo ('obj/install-test-' + [guid]::NewGuid().ToString('N'))
$steam = Join-Path $fixture 'Steam'
$library = Join-Path $fixture 'Custom Library'
$game = Join-Path $library 'steamapps/common/Custom RimWorld'
$old = Join-Path $game 'Mods/My old mod'
New-Item -ItemType Directory -Path "$steam/steamapps", "$old/About", "$old/Source", "$old/Defs", "$old/Assemblies" -Force | Out-Null
# 生成测试安装数据，不启动游戏。
Set-Content "$steam/steamapps/libraryfolders.vdf" ('"libraryfolders" { "0" { "path" "' + $library.Replace('\', '\\') + '" } }')
Set-Content "$library/steamapps/appmanifest_294100.acf" '"AppState" { "installdir" "Custom RimWorld" }'
Set-Content "$game/RimWorld.exe" ''
Set-Content "$game/Version.txt" '1.7.4981 rev136'
Copy-Item "$repo/About/About.xml" "$old/About/About.xml"
Set-Content "$old/Source/preserve.txt" 'original source'
Set-Content "$old/Assemblies/old.dll" 'obsolete'
$libraries = @(Get-SteamLibraries -Roots @($fixture))
Assert ($libraries -contains $library) 'VDF discovers custom library with spaces and escaped backslashes'
Assert (@(Find-RimWorld @($library)) -contains $game) 'Manifest resolves non-default game directory'
& $installer -RimWorldDir $game -SkipBuild -WhatIf
Assert (!(Test-Path "$game/ModBackups") -and (Test-Path "$old/Source/preserve.txt")) 'WhatIf makes no installation changes'
& $installer -RimWorldDir $game -SkipBuild
$backups = @(Get-ChildItem "$game/ModBackups" -Directory)
Assert ($backups.Count -eq 1 -and (Get-Content "$($backups[0].FullName)/Source/preserve.txt") -eq 'original source') 'Whole original directory is backed up outside Mods'
Assert (!(Test-Path "$old/Defs") -and !(Test-Path "$old/Assemblies") -and !(Test-Path "$old/Source")) 'Old root definitions, assemblies and source do not leak into new installation'
Assert ((Get-FileHash "$old/1.7/Assemblies/Companionship Barracks.dll").Hash -eq (Get-FileHash "$repo/1.7/Assemblies/Companionship Barracks.dll").Hash) 'Installed DLL matches build output'
Assert ((@(Get-ChildItem "$game/Mods" -Directory)).Count -eq 1) 'Only one mod copy remains in Mods'

New-Item -ItemType Directory "$fixture/Harmony" | Out-Null
Set-Content "$fixture/Harmony/0Harmony.dll" ''
Set-Content "$fixture/fail-build.ps1" 'exit 7'
$failed = $false
try { & $installer -RimWorldDir $game -HarmonyDir "$fixture/Harmony" -Dotnet "$fixture/fail-build.ps1" }
catch { $failed = $_.Exception.Message -like '构建失败*' }
Assert ($failed -and (@(Get-ChildItem "$game/ModBackups" -Directory)).Count -eq 1 -and (Test-Path "$old/LoadFolders.xml")) 'Build failure preserves existing installation'
Write-Host "Test artifacts: $fixture"
