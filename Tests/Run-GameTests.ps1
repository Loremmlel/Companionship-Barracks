#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$RimWorldDir,
    [Parameter(Mandatory)][string]$HarmonyModDir,
    [string]$Dotnet,
    [string[]]$Expansions = @(),
    [int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$game = (Resolve-Path -LiteralPath $RimWorldDir).Path.TrimEnd('\', '/')
$harmony = (Resolve-Path -LiteralPath $HarmonyModDir).Path
if (!(Test-Path -LiteralPath "$game/RimWorld.exe")) { throw 'Invalid game directory' }
if (Get-Process RimWorld -ErrorAction SilentlyContinue) { throw '请先退出 RimWorld，再运行测试。' }
$environment = Join-Path (Split-Path $repo -Parent) 'Use-DevEnvironment.ps1'
if (Test-Path -LiteralPath $environment) { . $environment }
else { $env:DOTNET_CLI_HOME = Join-Path $repo 'obj/dotnet-home' }
if (!$Dotnet) { $Dotnet = 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
& $Dotnet build "$PSScriptRoot/GameTests/GameTests.csproj" -c Release "-p:RimWorldDir=$game" "-p:HarmonyDir=$harmony/1.7/Assemblies" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

function Remove-TestTree([string]$Path, [string]$Parent) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith([IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Cleanup outside test boundary: $Path" }
    if (Test-Path -LiteralPath $resolved) {
        # 不跟随目录链接；先移除链接本身，再删除测试目录。
        Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object { $_.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) } |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$id = [guid]::NewGuid().ToString('N')
$scratch = Join-Path $game ".companionship-test-$id"
$candidate = Join-Path $game "Mods/CompanionshipBarracks-Test-$id"
$testMod = Join-Path $game "Mods/CompanionshipBarracks-Tests-$id"
$data = Join-Path $scratch 'savedata'
$userData = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios'
$userConfig = Join-Path $userData 'Config/ModsConfig.xml'
$logs = Join-Path $userData 'Logs'
$hadLogs = Test-Path -LiteralPath $logs
$hadConfig = Test-Path -LiteralPath $userConfig
$original = @(Get-ChildItem -LiteralPath "$game/Mods" -Directory | Where-Object {
    $about = Join-Path $_.FullName 'About/About.xml'
    (Test-Path -LiteralPath $about) -and ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId -eq 'shikiyuzu.zompanionship_barracks'
})
if ($original.Count -gt 1) { throw '存在重复的本模组，测试前请先整理。' }
foreach ($path in @($scratch, $candidate, $testMod) + @($original.FullName)) {
    if (![IO.Path]::GetFullPath($path).StartsWith($game + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path outside game directory: $path" }
    if ((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "不能临时替换目录链接：$path" }
}
New-Item -ItemType Directory -Path "$data/Config", "$scratch/root-text" | Out-Null
if ($hadConfig) { Copy-Item -LiteralPath $userConfig -Destination "$scratch/original-ModsConfig.xml" }
if ($hadLogs) { Copy-Item -LiteralPath $logs -Destination "$scratch/Logs" -Recurse }
# 引擎或安装中的启动组件可能写根目录文本；测试结束恢复这些文件。
$rootText = @(Get-ChildItem -LiteralPath $game -File | Where-Object Extension -In '.txt', '.log')
foreach ($file in $rootText) { Copy-Item -LiteralPath $file.FullName -Destination "$scratch/root-text" }
$process = $null
$movedOriginal = $false
try {
    if ($original.Count -eq 1) {
        Move-Item -LiteralPath $original[0].FullName -Destination "$scratch/original-mod"
        $movedOriginal = $true
    }
    New-Item -ItemType Directory -Path $candidate, "$testMod/Assemblies" | Out-Null
    Copy-Item -LiteralPath "$repo/About", "$repo/Common", "$repo/1.6", "$repo/1.7", "$repo/LoadFolders.xml" -Destination $candidate -Recurse
    Copy-Item -LiteralPath "$PSScriptRoot/GameTests/About" -Destination $testMod -Recurse
    Copy-Item -LiteralPath "$PSScriptRoot/GameTests/bin/Release/net10.0/CompanionshipBarracks.GameTests.dll" -Destination "$testMod/Assemblies"
    # 使用临时存档/缓存目录，原模组列表有备份但不必覆盖；测试列表写在临时目录中。
    $config = [xml]'<ModsConfigData><version/><activeMods/><knownExpansions/></ModsConfigData>'
    $config.ModsConfigData.version = (Get-Content "$game/Version.txt" -Raw).Trim()
    $known = Get-ChildItem "$game/Data" -Directory | ForEach-Object {
        $about = Join-Path $_.FullName 'About/About.xml'
        if (Test-Path -LiteralPath $about) { ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId }
    }
    foreach ($id in (@('brrainz.harmony', 'ludeon.rimworld') + $Expansions + @('shikiyuzu.zompanionship_barracks', 'shikiyuzu.companionshipbarracks.tests'))) {
        $node = $config.CreateElement('li'); $node.InnerText = $id
        [void]$config.SelectSingleNode('/ModsConfigData/activeMods').AppendChild($node)
    }
    foreach ($id in $known) {
        $node = $config.CreateElement('li'); $node.InnerText = $id.ToLowerInvariant()
        [void]$config.SelectSingleNode('/ModsConfigData/knownExpansions').AppendChild($node)
    }
    $config.Save("$data/Config/ModsConfig.xml")
    $process = Start-Process -FilePath "$game/RimWorld.exe" -WorkingDirectory $game `
        -ArgumentList @("`"-savedatafolder=$data`"", "`"-cb-mod-path=$candidate`"", '-cb-tests', '-batchmode', '-nographics') -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) { throw 'Game test timed out' }
    $result = Join-Path $data 'cb-test-results.txt'
    if (!(Test-Path -LiteralPath $result)) { throw "No test result. Exit=$($process.ExitCode)" }
    Get-Content -LiteralPath $result
    if ($process.ExitCode -ne 0 -or (Select-String -LiteralPath $result -Pattern '^FAIL ' -Quiet)) { throw 'Game tests failed' }
}
finally {
    if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }
    Remove-TestTree $candidate "$game/Mods"
    Remove-TestTree $testMod "$game/Mods"
    if ($movedOriginal) { Move-Item -LiteralPath "$scratch/original-mod" -Destination $original[0].FullName }
    Remove-TestTree $logs $userData
    if ($hadLogs) { Copy-Item -LiteralPath "$scratch/Logs" -Destination $logs -Recurse }
    if ($hadConfig) {
        if (!(Test-Path -LiteralPath $userConfig) -or (Get-FileHash $userConfig).Hash -ne (Get-FileHash "$scratch/original-ModsConfig.xml").Hash) {
            Copy-Item -LiteralPath "$scratch/original-ModsConfig.xml" -Destination $userConfig -Force
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath $game -File | Where-Object Extension -In '.txt', '.log') {
        if ($file.Name -notin $rootText.Name) { Remove-Item -LiteralPath $file.FullName -Force }
    }
    foreach ($file in $rootText) { Copy-Item -LiteralPath "$scratch/root-text/$($file.Name)" -Destination $file.FullName -Force }
    Remove-TestTree $scratch $game
    Write-Host '已恢复原模组、模组列表与日志，测试模组、存档、缓存和临时备份已清理。'
}
