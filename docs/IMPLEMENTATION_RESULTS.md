# 两组功能修复与 RimWorld 1.7 验收

日期：2026-10-17。设计先提交为 `0b36414`，随后实施。四份 XML 均保持字节一致；1.6 DLL 保留旧版，修复版用于 1.7。

## 已实施

| 范围 | 实际变化 |
| --- | --- |
| 营房选择 | 同房人类床登记主人去重；每人只归入一个关系类别；修复亲属重复/漏计、累计朋友比例及精确 1/3 边界；保留四种已有长幼映射，同龄等组合回到普通规则 |
| 营房生命周期 | 原版结算成功后清旧自定义环境记忆；以传入床、原版实际 stage 和接纳结果决定替换；失败保留原版实例；换房不残留；正面卧室清理同时处理正面的自定义营房效果 |
| 特殊身份事件 | 保留两个已有 Def 的身份方向；显式目标优先，否则读取原记忆目标；保留倍率、moodOffset、sourcePrecept、duration override；保留候选自己的社交默认值；候选不接纳就继续普通事件；不清空既有历史，不给另一方额外补发 |
| 兼容工程 | .NET 10 SDK 工程，引用游戏根目录 Assembly-CSharp/UnityStubs 与 Harmony 1.7；共享 XML 放 Common，LoadFolders 分别路由 1.6/1.7；About 增加 1.7 |
| 诊断 | 默认仅启动消息与一次性定义问题警告；逐次替换日志只在开发者模式输出 |

没有新增计时器、床上即时心情、事件触发器或身份内容。选择正确 Def、恢复原版倍率会改变原先错误的实际结果，但没有修改文案、基础效果、持续时间、堆叠、阶段顺序或过滤配置。

## 实测环境与证据

- 游戏：本地 Steam unstable `1.7.4981 rev136`，build `26001021`。
- 主程序集：`1.7.9786.4087`，目标 `net10.0`；游戏/Harmony 指纹与重构计划第 2 节一致。
- Harmony：mod `3.0.1.0`，实际库 `3.0.0.0-rc.3`，使用 `1.7/Assemblies/0Harmony.dll`。
- 构建：.NET SDK `10.0.402`，Release，0 warnings / 0 errors；游戏/Unity/Harmony 引用不随产物复制。
- 1.7 发布 DLL SHA-256：`116587E28BDDDE242EBFCBE495EC15148DE653E528E9D10C5369173D9B2BCAFC`。
- 1.6 保留 DLL SHA-256：`8E4F460BB9BB4629AFFAEBC4175A8D48126946F95400BA78194F3D915C388715`，Git blob 与 `e82e468` 一致。

测试插件在实际 RimWorld 进程内加载 Def、生成世界和地图、调用游戏方法并执行游戏自己的保存读取。不是只检查编译或模拟 MemoryThoughtHandler。对全部 10 档的测试是将原版 stage 输入补丁进行核对；真实房间计算另外验证，不声称人工建造了十种品质房间。

| 组合 | 结果 |
| --- | --- |
| Core + Harmony + 修复版 + 测试插件 | 95 项通过，退出码 0；见 `verification/core-results.txt` |
| Core + Royalty + Ideology + Biotech + Harmony + 修复版 + 测试插件 | 102 项通过，退出码 0；见 `verification/dlc-results.txt`。与本机当时用户配置中的三个 DLC 一致，并额外启用 Harmony、本模组和测试插件 |

测试记录分别来自 `obj/game-tests-20261017-133708/` 与 `obj/game-tests-20261017-134129/`，保留完整临时运行目录、测试存档和结果。游戏根文件复制到独立 runtime，大资源目录使用 junction；Mods、配置、存档独立。没有把原游戏的整个 Mods 链接进测试目录。

实际进程断言本模组只有一份，且 DLL 来自 `runtime/Mods/Main/1.7/Assemblies/Companionship Barracks.dll`。原安装目录的 `Mods/Companionship Barracks` 未参与最终测试，未被本次部署覆盖。

## 两组独立验收结果

| 检查 | 结果与边界 |
| --- | --- |
| 关系规则 | 好感边界、互斥血亲类别、优先级、零室友、半数与 1/3 比例、同一主人去重均通过 |
| 特殊营房映射 | 四个已有性别/长幼方向通过；同龄、无性别、同性退回普通亲属规则 |
| 原版时序 | 调用实际 ApplyBedRelatedEffects / FinalizeLayingJob，验证期限前不补发、到期产生、离床重结算与单一效果；未用持续真人操作覆盖全部 job 中断情况 |
| 原版资格 | 非自有床、Ascetic、囚犯床、无主医用床、无 mood 通过；遵循实际原版接纳结果 |
| 房间转换 | 自定义到原版、零室友、营房到卧室、地面环境效果通过；卧室本来存在 null stage，此时正确保持无卧室心情 |
| 清理与失败 | 正/零/负效果清理、原方法被另一补丁跳过、候选拒绝、无效 stage 回退通过；拒绝与跳过使用测试 Harmony 补丁注入 |
| 事件映射与回退 | 双方各自提交、未覆盖方向、同龄/无性别、目标仅在原记忆内、缺 Def、过滤拒绝、旧同名记忆不能充当本次接纳均通过 |
| 事件数值 | 倍率 1 / 1.5、moodOffset、duration override、候选社交默认值保留；基础 10 实际得到 10 / 15；三 DLC 场景额外验证非空理念上下文 |
| 事件重复 | 不额外给另一方发放；连续提交保留历史，按 10 层上限淘汰旧记忆，保持 0.6 系数及 3 天持续时间 |
| 联合与存档 | 两组不互相清理；实际保存/读取保留 Def、关联人物、age、stage、倍率与堆叠，睡眠期限和离床标记保留；1 天与 3 天到期独立验证 |

## 内容保持

四份文件从 `Defs/ThoughtDefs` 移至 `Common/Defs/ThoughtDefs`，与实施前 SHA-256 完全一致：

| 文件 | SHA-256 |
| --- | --- |
| Thoughts_Companionship_Barracks.xml | `88E6C7293B93F9DE66748980CBD47601100A5B93CA54DA2624DB1CF793ABD71E` |
| Thoughts_MakeLove.xml | `4C6E919974E073EF9CB8B87058A44D28702A56AC60D090D6F3792D856B9B5124` |
| Thoughts_Memory_Misc.xml | `87090C428F7B8DABC6747952710836ED66F28DB5B16CA8CFEFDFA9250EDA0CAD` |
| Thoughts_SleepWith.xml | `FABD74846FC1E63D981D213E36ED78A3470FB117741F1BDE7F5A4C9FD9A27D14` |

## 日志与尚未覆盖的部分

- 原 packageId `shikiyuzu.zompanionship_barracks` 带下划线，游戏会提示格式警告；为保持已有模组/存档身份，本轮不改 ID。这不是重复加载提示。
- 测试主动制造无效 stage 时会出现预期的候选不可用警告；随后验证原版结果仍在。
- 隔离副本从 Steam 外启动，日志存在 Steam 初始化警告、本机失效 Vulkan layer 路径及引擎初始化/退出提示；它们不等于本模组异常。早期带 steam_appid 的副本还扫描出 Workshop 的 Talos.Mincho.FA 重复项，最终隔离副本不加载这份模组。
- 保存目录参数不改变此版引擎 Player.log 的系统默认位置，因此测试会更新默认引擎日志；用户配置和日常存档未改动。
- 最终三 DLC 场景读档时，原版理念初始化还提示补入缺失的隐藏 Funeral precept；调用栈来自原版理念读档修补，不来自本模组补丁。测试夹具使用快速开局生成理念，此提示单独记录，没有据此宣称原版/DLC 日志完全干净。
- 修复后的真实新存档保存/读取已验证，尚未读取用户既有的大型旧存档或跨 1.6→1.7 的历史存档。Def 身份和原生记忆类型保持不变，不把它当成旧存档已经实测。
- 未覆盖其他第三方房间/事件替换模组、Anomaly/Odyssey、所有奴隶/deathrest 分支、长时间连续游玩及完整动画/job 中断矩阵。两组核心行为通过不等于所有扩展组合通过。
- 1.6 仅保留旧包；没有当前 1.6 环境回归，也没有宣称旧包获得本轮修复。unstable 后续更新需要重验。

## 复现

PowerShell 7，在仓库中执行（需要 .NET 10 SDK 与本地已安装游戏）：

```powershell
./Tests/Run-GameTests.ps1 `
  -RimWorldDir 'E:\SteamLibrary\steamapps\common\RimWorld' `
  -HarmonyModDir 'E:\SteamLibrary\steamapps\workshop\content\294100\2009463077'

# 与本次用户 DLC 配置对应的组合
./Tests/Run-GameTests.ps1 `
  -RimWorldDir 'E:\SteamLibrary\steamapps\common\RimWorld' `
  -HarmonyModDir 'E:\SteamLibrary\steamapps\workshop\content\294100\2009463077' `
  -Expansions @('ludeon.rimworld.royalty', 'ludeon.rimworld.ideology', 'ludeon.rimworld.biotech')
```

脚本每次建立新的 `obj/game-tests-*`，构建测试和发布 DLL，最多等待 240 秒，只结束自己启动的测试进程。失败保留现场。测试源码/断言详见 `Tests/GameTests/GameTests.cs`；不要将测试插件装进日常存档。
