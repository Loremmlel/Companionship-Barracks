#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param([string]$RimWorldDir, [string]$HarmonyDir, [string]$Dotnet, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'

function Get-SteamLibraries {
    param([string[]]$Roots = @([IO.DriveInfo]::GetDrives() | Where-Object { $_.IsReady -and $_.DriveType -in 'Fixed', 'Removable' } | ForEach-Object { $_.RootDirectory.FullName }))
    $candidates = @(
        foreach ($key in 'HKCU:/Software/Valve/Steam', 'HKLM:/SOFTWARE/WOW6432Node/Valve/Steam') {
            $entry = Get-ItemProperty $key -ErrorAction SilentlyContinue
            $entry.SteamPath
            $entry.InstallPath
        }
        # 只检查常见目录的一层；自定义库位置交给 libraryfolders.vdf。
        foreach ($root in $Roots) {
            foreach ($relative in '', 'Program Files', 'Program Files (x86)', 'Games') {
                $base = if ($relative) { Join-Path $root $relative } else { $root }
                Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue |
                    Where-Object Name -Like '*Steam*' | ForEach-Object { $_.FullName }
            }
        }
    ) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'steamapps')) } | Sort-Object -Unique
    $libraries = @($candidates)
    foreach ($steam in $candidates) {
        $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"((?:\\.|[^"\\])*)"')) {
                $library = $match.Groups[1].Value.Replace('\\', '\')
                if (Test-Path -LiteralPath (Join-Path $library 'steamapps')) { $libraries += $library }
            }
        }
    }
    $libraries | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\', '/') } | Sort-Object -Unique
}

function Find-RimWorld {
    param([string[]]$Libraries)
    foreach ($library in $Libraries) {
        $name = 'RimWorld'
        $manifest = Join-Path $library 'steamapps/appmanifest_294100.acf'
        if ((Test-Path -LiteralPath $manifest) -and (Get-Content -LiteralPath $manifest -Raw) -match '"installdir"\s+"([^"/\\]+)"') { $name = $Matches[1] }
        $candidate = Join-Path $library "steamapps/common/$name"
        if (Test-Path -LiteralPath "$candidate/RimWorld.exe") { $candidate }
    }
}

# 点源导入仅提供查找函数，供回归检查使用。
if ($MyInvocation.InvocationName -eq '.') { return }
$repo = $PSScriptRoot
$libraries = @(Get-SteamLibraries)
if (!$RimWorldDir) {
    $games = @(Find-RimWorld $libraries | Sort-Object -Unique)
    if ($games.Count -ne 1) { throw "找到 $($games.Count) 个安装，请用 -RimWorldDir 指定。候选：$($games -join ', ')" }
    $RimWorldDir = $games[0]
}
$game = (Resolve-Path -LiteralPath $RimWorldDir).Path.TrimEnd('\', '/')
if (!(Test-Path -LiteralPath "$game/RimWorld.exe") -or !(Test-Path -LiteralPath "$game/Version.txt")) { throw "不是 RimWorld 安装目录：$game" }
if ((Get-Content -LiteralPath "$game/Version.txt" -Raw) -notmatch '^1\.7(?:\.|\s)') { throw '此脚本只构建和安装 RimWorld 1.7 包。' }
if (!$SkipBuild -and !$HarmonyDir) {
    $gameLibrary = Split-Path (Split-Path (Split-Path $game -Parent) -Parent) -Parent
    $harmonyCandidates = @(
        foreach ($library in (@($gameLibrary) + $libraries | Select-Object -Unique)) {
            Join-Path $library 'steamapps/workshop/content/294100/2009463077/1.7/Assemblies'
        }
        Get-ChildItem -LiteralPath "$game/Mods" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $about = Join-Path $_.FullName 'About/About.xml'
            if ((Test-Path -LiteralPath $about) -and ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId -eq 'brrainz.harmony') {
                Join-Path $_.FullName '1.7/Assemblies'
            }
        }
    )
    $HarmonyDir = $harmonyCandidates | Where-Object { Test-Path -LiteralPath "$_/0Harmony.dll" } | Select-Object -First 1
}
if (!$SkipBuild -and (!$HarmonyDir -or !(Test-Path -LiteralPath "$HarmonyDir/0Harmony.dll"))) { throw '未找到 Harmony 1.7，请用 -HarmonyDir 指定其 1.7/Assemblies 目录。' }

$packageId = ([xml](Get-Content -LiteralPath "$repo/About/About.xml" -Raw)).ModMetaData.packageId
$mods = Join-Path $game 'Mods'
$existing = @(Get-ChildItem -LiteralPath $mods -Directory -ErrorAction SilentlyContinue | Where-Object {
    $about = Join-Path $_.FullName 'About/About.xml'
    (Test-Path -LiteralPath $about) -and ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId -eq $packageId
})
if ($existing.Count -gt 1) { throw "存在多个本模组副本，请先整理：$($existing.FullName -join ', ')" }
$destination = if ($existing.Count -eq 1) { $existing[0].FullName } else { Join-Path $mods 'Companionship Barracks' }
if ((Test-Path -LiteralPath $destination) -and ($existing.Count -eq 0 -or (Get-Item -LiteralPath $destination).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint))) {
    throw "目标被其他内容占用或是目录链接，不能替换：$destination"
}
$backupRoot = Join-Path $game 'ModBackups'
foreach ($path in $mods, $destination, $backupRoot) {
    if (![IO.Path]::GetFullPath($path).StartsWith($game + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "目标越出游戏目录：$path" }
    if ((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "目标是目录链接：$path" }
}
Write-Host "游戏：$game"
if (!$SkipBuild) { Write-Host "Harmony：$HarmonyDir" }
Write-Host "安装：$destination"
if (!$PSCmdlet.ShouldProcess($destination, '构建/安装模组，旧目录完整备份至游戏目录 ModBackups')) { return }

if (!$SkipBuild) {
    if (!$Dotnet) { $Dotnet = if (Test-Path -LiteralPath "$repo/obj/tools/dotnet/dotnet.exe") { "$repo/obj/tools/dotnet/dotnet.exe" } else { 'dotnet' } }
    & $Dotnet build "$repo/Companionship Barracks.sln" -c Release "-p:RimWorldDir=$game" "-p:HarmonyDir=$HarmonyDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw '构建失败，未更改游戏中的模组。' }
}
foreach ($file in 'About/About.xml', 'LoadFolders.xml', '1.6/Assemblies/Companionship Barracks.dll', '1.7/Assemblies/Companionship Barracks.dll') {
    if (!(Test-Path -LiteralPath "$repo/$file")) { throw "发布文件缺失：$file" }
}
$id = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$stage = Join-Path $backupRoot "staging-$id"
$backup = Join-Path $backupRoot "Companionship Barracks-$id"
foreach ($path in $stage, $backup) {
    if (![IO.Path]::GetFullPath($path).StartsWith($backupRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "备份路径越界：$path" }
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath "$repo/About", "$repo/Common", "$repo/1.6", "$repo/1.7", "$repo/LoadFolders.xml" -Destination $stage -Recurse
New-Item -ItemType Directory -Path $mods -Force | Out-Null
if (Test-Path -LiteralPath $destination) { Move-Item -LiteralPath $destination -Destination $backup }
try { Move-Item -LiteralPath $stage -Destination $destination }
catch {
    if ((Test-Path -LiteralPath $backup) -and !(Test-Path -LiteralPath $destination)) { Move-Item -LiteralPath $backup -Destination $destination }
    throw
}
Write-Host "已安装：$destination"
if (Test-Path -LiteralPath $backup) { Write-Host "旧版完整备份：$backup" }
