param(
    [Parameter(Mandatory)][string]$RimWorldDir,
    [Parameter(Mandatory)][string]$HarmonyModDir,
    [string]$Dotnet = 'dotnet',
    [string[]]$Expansions = @(),
    [int]$TimeoutSeconds = 240
)
# PowerShell 7；所有测试内容写入新的 obj 子目录，不覆盖日常模组或存档。
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repo 'obj/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$game = (Resolve-Path -LiteralPath $RimWorldDir).Path
$harmony = (Resolve-Path -LiteralPath $HarmonyModDir).Path
$run = Join-Path $repo ('obj/game-tests-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $run) { throw "Test directory already exists: $run" }
$runtime = Join-Path $run 'runtime'
$data = Join-Path $run 'savedata'
New-Item -ItemType Directory -Path "$runtime/Mods/Main/1.7/Assemblies", "$runtime/Mods/Tests/Assemblies", "$data/Config" | Out-Null
& $Dotnet build "$PSScriptRoot/GameTests/GameTests.csproj" -c Release "-p:RimWorldDir=$game" "-p:HarmonyDir=$harmony/1.7/Assemblies" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

# 复制引擎根文件；大资源目录只读使用 junction。不要递归删除这些 junction 的目标。
Get-ChildItem -LiteralPath $game -File | Where-Object { $_.Extension -in '.dll', '.exe', '.json', '.zip', '.dat' -or $_.Name -eq 'Version.txt' } |
    Copy-Item -Destination $runtime
foreach ($folder in 'Data', 'Rendering', 'vulkan', 'RimWorldWin64_Data') {
    New-Item -ItemType Junction -Path (Join-Path $runtime $folder) -Target (Join-Path $game $folder) | Out-Null
}
New-Item -ItemType Junction -Path "$runtime/Mods/Harmony" -Target $harmony | Out-Null
Copy-Item "$repo/About", "$repo/Common", "$repo/LoadFolders.xml" -Destination "$runtime/Mods/Main" -Recurse
Copy-Item "$repo/1.7/Assemblies/Companionship Barracks.dll" -Destination "$runtime/Mods/Main/1.7/Assemblies"
Copy-Item "$PSScriptRoot/GameTests/About" -Destination "$runtime/Mods/Tests" -Recurse
Copy-Item "$PSScriptRoot/GameTests/bin/Release/net10.0/CompanionshipBarracks.GameTests.dll" -Destination "$runtime/Mods/Tests/Assemblies"

# 登记所有已发现 DLC，避免游戏把新 DLC 自动加入最小组合。
$known = Get-ChildItem "$game/Data" -Directory | ForEach-Object {
    $about = Join-Path $_.FullName 'About/About.xml'
    if (Test-Path -LiteralPath $about) { ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId }
}
$config = [xml]'<ModsConfigData><version/><activeMods/><knownExpansions/></ModsConfigData>'
$config.ModsConfigData.version = (Get-Content "$game/Version.txt" -Raw).Trim()
foreach ($id in (@('brrainz.harmony', 'ludeon.rimworld') + $Expansions + @('shikiyuzu.zompanionship_barracks', 'shikiyuzu.companionshipbarracks.tests'))) {
    $node = $config.CreateElement('li'); $node.InnerText = $id
    [void]$config.SelectSingleNode('/ModsConfigData/activeMods').AppendChild($node)
}
foreach ($id in $known) {
    $node = $config.CreateElement('li'); $node.InnerText = $id.ToLowerInvariant()
    [void]$config.SelectSingleNode('/ModsConfigData/knownExpansions').AppendChild($node)
}
$config.Save("$data/Config/ModsConfig.xml")
$process = Start-Process -FilePath "$runtime/RimWorld.exe" -WorkingDirectory $runtime `
    -ArgumentList @("`"-savedatafolder=$data`"", '-cb-tests', '-batchmode', '-nographics') -WindowStyle Hidden -PassThru
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill()
    throw "Game test timed out. Artifacts: $run"
}
$result = Join-Path $data 'cb-test-results.txt'
if (!(Test-Path -LiteralPath $result)) { throw "No test result. Exit=$($process.ExitCode), artifacts: $run" }
Get-Content -LiteralPath $result
Write-Output "Artifacts: $run"
if ($process.ExitCode -ne 0 -or (Select-String -LiteralPath $result -Pattern '^FAIL ' -Quiet)) { throw 'Game tests failed' }
