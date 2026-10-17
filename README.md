# Companionship Barracks

陪伴营房：在原版睡眠结算时，根据登记室友的关系替换营房心情；原版提交特殊身份事件记忆时，按已有身份定义替换。保留所有现有文案、基础数值、持续时间和堆叠设置。

## 版本与目录

- `About/`：元信息、预览图；沿用原 packageId。
- `Common/Defs/ThoughtDefs/`：四份原有 XML，内容不变。
- `1.6/Assemblies/`：保留 `e82e468` 的旧版 DLL，未将本轮修复回移到 1.6。
- `1.7/Assemblies/`：本轮修复的 .NET 10 DLL。
- `LoadFolders.xml`：按游戏版本选择共享定义和对应 DLL，避免两版同时加载。
- `Source/CompanionshipBarracks/`：1.7 源码，营房与事件功能分别组织。
- `Tests/GameTests/`：实际游戏进程内的独立测试模组，不随正式模组安装。
- [重构计划](docs/REFACTORING_PLAN.md)与[实施验收记录](docs/IMPLEMENTATION_RESULTS.md)：规则、修复范围和实测边界。

## 构建 1.7

需要 .NET 10 SDK、本地 RimWorld 1.7 和 Harmony 1.7 程序集。在 PowerShell 7 中运行，替换为自己的路径：

```powershell
dotnet build '.\Companionship Barracks.sln' -c Release `
  '-p:RimWorldDir=E:\SteamLibrary\steamapps\common\RimWorld' `
  '-p:HarmonyDir=E:\SteamLibrary\steamapps\workshop\content\294100\2009463077\1.7\Assemblies'
```

输出为 `1.7/Assemblies/Companionship Barracks.dll`。不复制游戏、Unity 或 Harmony DLL；旧版 1.6 源码可从 `e82e468` 获取。

## 安装

将 `About/`、`Common/`、`1.6/`、`1.7/` 和 `LoadFolders.xml` 复制到模组目录，启用 Harmony 后再启用本模组。升级旧安装时移走旧根目录的 `Defs/`、`Assemblies/`，同一 packageId 只保留一份启用的副本。不要将测试模组一起安装到日常游戏。

不要求每次上床立刻产生心情：首次期限前短睡可能没有原版结算；本模组跟随原版时机与资格。营房室友按同房间人类床的登记主人去重统计，不要求当时都在床上。营房记忆按原配置持续 1 天，事件记忆为 3 天，并遵循原版提前清理和堆叠规则。

默认不输出每次结算日志；开发者模式下记录候选和接纳结果。1.7 仍在更新，验证结论仅适用于验收记录中的具体构建，更新后应重新运行测试。

复现游戏测试可使用 `Tests/Run-GameTests.ps1`，参数和已验证组合见[实施验收记录](docs/IMPLEMENTATION_RESULTS.md#复现)。
