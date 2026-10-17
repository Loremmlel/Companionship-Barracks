# Companionship Barracks

陪伴营房：使殖民者之间的关系影响营房心情。当前模组面向 RimWorld 1.6，需要启用 Harmony。

## 仓库内容

- `About/`：模组元信息与预览图，保留原有 packageId，兼容现有模组配置。
- `Defs/ThoughtDefs/`：从原模组导入的完整 Thought 定义。
- `Assemblies/Companionship Barracks.dll`：与当前源码对应的 Release 程序集。
- 根目录 C# 工程与 `Properties/`：源码。
- `docs/REFACTORING_PLAN.md`：后续 v2 重构设计稿，不代表当前已实现。

## 构建

需要 Visual Studio 2022 / Build Tools 的 MSBuild，以及 .NET Framework 4.7.2 Developer Pack。游戏与 Harmony 的程序集使用本地安装文件，不随仓库分发。

在 PowerShell 7 中运行（将以下示例路径替换为自己的安装路径）：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  '.\Companionship Barracks.sln' /t:Rebuild /p:Configuration=Release `
  '/p:RimWorldManagedDir=E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed' `
  '/p:HarmonyDir=E:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies'
```

Debug 和 Release 都输出至仓库内的 `Assemblies/`。提交或发布 DLL 时使用 Release 构建。

## 安装

将 `About/`、`Defs/` 和 `Assemblies/` 一起复制到 RimWorld 的 `Mods/Companionship Barracks/` 下，在模组列表中先启用 Harmony，再启用本模组。同一 packageId 只保留一份启用的副本。

仓库补齐时保留了原模组的全部 XML 定义与文本，尚未实施 v2 玩法重构。营房同住者仍按房间内床位登记的主人统计。
