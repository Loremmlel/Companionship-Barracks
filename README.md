# Companionship Barracks

陪伴营房：在原版睡眠结算时，根据登记室友的关系替换营房心情；原版提交特殊身份事件记忆时，按已有身份定义替换。两组已有文案、基础数值、持续时间和堆叠设置保持不变。另补齐近亲共床规则，见下文。

## 版本与目录

- `About/`：元信息、预览图；沿用原 packageId。
- `Common/Defs/ThoughtDefs/`：营房、事件两份原有 XML，以及共床规则的 `Thoughts_SharedBed.xml`；删除两份未正确接入的旧定义。
- `1.6/Assemblies/`：共用源码编译的 .NET Framework 4.7.2 DLL；仅编译通过，不继续维护或回归旧版本，实际报错时再处理。
- `1.7/Assemblies/`：本轮修复的 .NET 10 DLL。
- `LoadFolders.xml`：按游戏版本选择共享定义和对应 DLL，避免两版同时加载。
- `Source/CompanionshipBarracks/`：共用源码，营房、事件、共床功能分别组织；当前维护与游戏测试针对 1.7。
- `Tests/GameTests/`：实际游戏进程内的独立测试模组，不随正式模组安装。
- [重构计划](docs/REFACTORING_PLAN.md)与[实施验收记录](docs/IMPLEMENTATION_RESULTS.md)：规则、修复范围和实测边界。

## 构建 1.7

需要 .NET 10 SDK、本地 RimWorld 1.7 和 Harmony 1.7 程序集。本工作区的 SDK、NuGet 缓存和反编译资源已集中到上级目录 `.shared/`。在 PowerShell 7 中运行，替换为自己的路径：

```powershell
# 在共享工作区内，先导入上级开发环境；独立克隆时使用自己的 .NET 10 SDK。
if (Test-Path ../Use-DevEnvironment.ps1) { . ../Use-DevEnvironment.ps1 }
dotnet build '.\Companionship Barracks.sln' -c Release `
  '-p:RimWorldDir=E:\SteamLibrary\steamapps\common\RimWorld' `
  '-p:HarmonyDir=E:\SteamLibrary\steamapps\workshop\content\294100\2009463077\1.7\Assemblies'
```

输出为 `1.7/Assemblies/Companionship Barracks.dll`。不复制游戏、Unity 或 Harmony DLL。工程保留 `-p:RimWorldVersion=1.6` 构建入口，使用固定版本的引用包与 Harmony 1.6；没有 1.6 游戏实测结论。

## 安装

安装入口已迁到上级 `RimWorld Mods` 工作区。PowerShell 7 中，在本 Mod 目录运行：

```powershell
../Install-Mod.ps1 -Mod Companionship-Barracks -WhatIf   # 预览识别结果，不构建或安装
../Install-Mod.ps1 -Mod Companionship-Barracks           # Release 构建后安装
../Install-Mod.ps1 -Mod Companionship-Barracks -SkipBuild # 直接安装已构建的 DLL
```

查找各个就绪磁盘根目录、`Program Files`、`Program Files (x86)` 和 `Games` 下名称包含 Steam 的目录，结合注册表和 `libraryfolders.vdf`，再通过 RimWorld manifest 定位游戏；不递归扫描整盘。多个安装时用 `-RimWorldDir` 指定。也可指定 `-HarmonyDir`（Harmony 的 `1.7/Assemblies`）和 `-Dotnet`（SDK 可执行文件）；默认使用上级 `.shared/tools/dotnet/dotnet.exe`，缺少时使用 PATH 中的 dotnet。通用安装参数和共享环境说明见上级 `README.md`。

脚本按 packageId 找到旧副本并替换，保留旧目录名称。旧版整个目录（含原源码）移至游戏的 `ModBackups/`，不会留在 `Mods/` 重复扫描。新目录只含发布内容，构建失败不触碰旧安装。安装前退出游戏；脚本不自动启用模组或修改存档。当前针对 1.7，附带仓库中的 1.6 DLL。

脚本回归检查已迁到上级：`../Tests/Test-InstallMod.ps1`，验证路径发现、多 Mod 安装、备份替换、定义安装、预览无写入和构建失败保留旧包，结束后删除工作区内的夹具。

也可手动安装：

将 `About/`、`Common/`、`1.6/`、`1.7/` 和 `LoadFolders.xml` 复制到模组目录，启用 Harmony 后再启用本模组。升级旧安装时移走旧根目录的 `Defs/`、`Assemblies/`，同一 packageId 只保留一份启用的副本。不要将测试模组一起安装到日常游戏。

不要求每次上床立刻产生心情：首次期限前短睡可能没有原版结算；本模组跟随原版时机与资格。营房室友按同房间人类床的登记主人去重统计，不要求当时都在床上。营房记忆按原配置持续 1 天，事件记忆为 3 天，并遵循原版提前清理和堆叠规则。

默认不输出每次结算日志；开发者模式下记录候选和接纳结果。1.7 仍在更新，验证结论仅适用于验收记录中的具体构建，更新后应重新运行测试。

## 近亲共床

- 按同一张床的登记主人判断，伴侣关系优先，伴侣继续使用原版规则。
- 非伴侣父母、子女、兄弟姐妹共床免除原版共床惩罚；兄弟姐妹包含同父异母、同母异父，祖辈等其他亲属不包含在内。
- 非伴侣兄弟姐妹：接收者对对方好感 ≥60 时获得一项 +3 情境心情，低于 60 只免罚；不按人数叠加，取消共床或关系不再满足后随原版情境心情刷新退出。这不属于有固定持续天数的睡眠记忆。
- 同床非伴侣兄弟姐妹作为打扰来源时，完全不生成「睡眠被打扰」记忆，也不占用该原版冷却。父母、子女或其他人的打扰保持原版行为。
- 多人床若同时登记了非近亲非伴侣，仍按这些人的最低好感计算原版惩罚，不能用一个亲属豁免其他人。

复现游戏测试可使用 `Tests/Run-GameTests.ps1`，直接启动传入安装目录中的游戏，不复制游戏。脚本备份模组列表、临时替换本模组，使用临时存档目录；退出后恢复原模组、配置和日志，删除测试模组、存档、缓存和临时备份。参数见[实施验收记录](docs/IMPLEMENTATION_RESULTS.md#复现)。
