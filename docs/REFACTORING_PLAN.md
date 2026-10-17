# Companionship Barracks 重构计划

> 状态：设计稿 / 待实施；本轮只更新文档
>
> 修订日期：2026-10-17
>
> 主要目标：RimWorld 1.7，当前核查版本为 1.7.4981 rev136（Steam unstable）
>
> 仓库基线：`e82e468`；默认分支：`master`
>
> 本文区分「本机静态核实」「v2 设计决定」「待游戏验证」，不把计划视为已经实现。

## 1. 目标与现状

本轮按原始需求修复两组并列功能，两组都必须有独立验收结果：

| 功能 | 触发与目标 | 修复范围 |
| --- | --- | --- |
| 营房关系心情 | 原版实际结算并接纳 `SleptInBarracks` 时，根据登记室友关系和房间等级选用现有心情及文案 | 关系分类、规则优先级、替换可靠性、阶段对应、旧效果清理 |
| 特殊身份事件心情（Sibling Lovin） | 原版尝试加入 `GotSomeLovin` 时，符合已有身份映射则选用对应特殊记忆，否则保留普通结果 | 身份映射与双方视角、原版运行时修正保留、候选接纳失败回退、重复事件与堆叠 |

用户已澄清「不要求刚躺下立即获得，替代原版睡眠心情即可」。不另加躺床触发器，不强制每次入睡都发记忆；原版尚未结算与已经结算却漏掉应有替换必须分开诊断。

营房保留「长期室友 / 床位主人」语义，不追踪昨晚实际同时睡眠的人。两组分别组织判断与 Harmony 适配，但特殊身份事件不再只是目录隔离或推迟处理的附属任务。

**内容冻结：** 保留 `e82e468` 中现有文案和数值，包括 label/description、各阶段 `baseMoodEffect` / `baseOpinionOffset`、持续时间、堆叠上限与衰减系数；保留 Def 身份、顺序和已有过滤条件。本轮修复「什么时候选、选哪份、能否可靠加入」，不重写文案、不重新平衡，不新增特殊身份内容。规则纠错使原本选错的 Def 改为正确 Def、恢复原版运行时倍率，仍可能改变实际显示的心情，这是修复效果，不是修改配置数值。

基线 `e82e468` 已补齐 About、预览图、全部 4 份 Thought XML 和对应 Release DLL；已将游戏/Harmony 引用目录参数化、输出移入仓库，补充构建安装说明并同步原源目录的年龄精度修正。当时的 1.6 引用环境下 Release 编译与 XML 静态检查已通过。

这些工作不等于已经兼容 1.7。当前工程仍面向 .NET Framework 4.7.2，引用 `UnityEngine.CoreModule`；About 只声明 1.6。新的本机安装已没有旧的 `RimWorldWin64_Data/Managed` 目录。

本轮未修改 C#、工程配置、About、Defs 或已提交 DLL，未启动游戏、未执行 1.7 构建，也未验证历史黄字的实际来源。

## 2. 核查依据与版本边界

### 2.1 本机证据

读取安装文件，并使用 ICSharpCode.Decompiler 11.1.0.9782 对合法安装的程序集做静态反编译核查，没有运行其中的游戏代码。临时分析输出位于 Git 忽略的 `obj/lifecycle-audit/`，不作为模组源码提交。复核应以以下版本、方法及文件指纹为准。

| 项目 | 结果 |
| --- | --- |
| 游戏目录 | `E:\SteamLibrary\steamapps\common\RimWorld` |
| `Version.txt` | `1.7.4981 rev136` |
| Steam manifest | `buildid=26001021`，`BetaKey=unstable` |
| 主程序集 | 游戏根目录 `Assembly-CSharp.dll` |
| 程序集版本 | `1.7.9786.4087`，与游戏显示版本不是同一编号 |
| 主程序集目标框架 | `.NETCoreApp,Version=v10.0` |
| 运行时配置 | `RimWorld.runtimeconfig.json`：`net10.0`，`10.0.13-waylithclr.dev` |
| Unity 兼容类型 | 根目录 `UnityStubs.dll`；运行时配置包含 Unity 程序集名称重定向 |
| Harmony 元信息 | 检查结束时 `modVersion=3.0.1.0`，声明支持 1.7 |
| Harmony 1.7 路由 | `LoadFolders.xml` 的 `v1.7` 选择 `/` 与 `1.7` |
| Harmony DLL | Workshop `2009463077/1.7/Assemblies/0Harmony.dll`；AssemblyVersion `3.0.0.0`；ProductVersion `3.0.0.0-rc.3+3c0e32cd3fd20e4ae27c47cb8c888844766f9f1a` |
| SDK | 核查时 `dotnet --list-sdks` 无输出；安装运行时不等于具备 .NET 10 SDK |

检查期间 Harmony 文件发生更新，开始时看到的 `Current/Assemblies` 与 2.x 元信息已失效。后续构建应重新读取最终加载目录，不能沿用旧路径或仅凭模组展示版本判断 DLL 版本。

SHA-256：

| 文件 | SHA-256 |
| --- | --- |
| `Assembly-CSharp.dll` | `EB0826B85AF430D13962E5C097EF53D869CF6A031656FB6A3673168A761C9F23` |
| `UnityStubs.dll` | `B9A47E34D975A6B41FDF2AF7A2EFFD8080A543D34D0153EE5D8614EFBB019270` |
| Harmony `1.7/Assemblies/0Harmony.dll` | `C8EF7856B2BE3CF24B345C9B5C2F197F68FCF92649635064FCE7B3CCC8F77CB0` |
| Core `Defs/ThoughtDefs/Thoughts_Memory_RoomStats.xml` | `6AEFED8A4414B899FED0F6799944031C7285E086BCE587F9A0D93BC3AA857E8E` |
| Core `Defs/Rooms/RoomStats.xml` | `5BAC24103C894719DC665F4843AFD7E7DBE2128D5D049B1BE47E878CD7F85447` |

### 2.2 外部依据及适用范围

- [Ludeon 2026-10-12 公告](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/)说明 1.7 改用 Waylith，当时通过 unstable 提供，许多旧模组可直接运行；开发者应测试后再声明 1.7 兼容。这不是本模组已兼容的证明。
- [Harmony 参数注入文档](https://harmony.pardeike.net/v2/articles/patching-injections.html)说明 `__runOriginal` 可判断原方法是否执行，`__state` 可在 Prefix/Postfix 间传递状态。此为 v2 文档，1.7 使用的 Harmony 3 实际补丁绑定仍须启动验证。
- [HarmonyRimWorld 作者仓库](https://github.com/pardeike/HarmonyRimWorld)用于核对分发信息；实际加载路径以安装包的 `LoadFolders.xml` 与运行日志为准。

下面的生命周期结论适用于所核查的 1.7 二进制。没有当前 1.6 程序集对照，不将结论扩展为「1.6 与 1.7 完全相同」。unstable 更新后先检查指纹，再复核相关方法。

## 3. 原版记忆生命周期：已静态核实

### 3.1 触发时机不是每次起床

核查方法：`Toils_LayDown.LayDown`、`ApplyBedRelatedEffects`、`FinalizeLayingJob`，以及 `Pawn_MindState.MindStateTickInterval`、`ExposeData`。

1. `LayDown` 初始化时，仅当 `applyBedThoughtsTick == 0` 才设置首次期限：当前游戏 tick 加 `Rand.Range(2500, 10000)`，并将 `applyBedThoughtsOnLeave` 置为 false。
2. 躺卧期间 `ApplyBedRelatedEffects` 检查非零期限是否到达；到达时调用 `ApplyBedThoughts(pawn, bed)`，期限增加 60000 tick，并把离床标志置为 true。
3. 此期限判断不以 `asleep` 为条件，不能解释为累计睡着一定时长。
4. `FinalizeLayingJob` 只在离床标志为 true 时再次调用 `ApplyBedThoughts(pawn, bed)`。
5. `Pawn_MindState` 的间隔更新在 Pawn 不再躺卧时把期限清零。快速离床再躺卧可能受尚未清零的状态影响，不能假定每个 job 都重新计时。
6. 期限与离床标志都写入存档；读取后不是重新抽取首次期限。

首次期限之前被叫醒可能完全不结算；达到期限后既可能在床上结算，也可能离床时再次结算。Mod 不增加计时器，不改上述字段，不强制短睡产生效果。

### 3.2 `ApplyBedThoughts` 先清旧记忆，再生成新记忆

实际签名：`private static void ApplyBedThoughts(Pawn actor, Building_Bed bed)`。

1. `actor.needs.mood == null` 时直接返回。
2. 清除六种原版旧记忆：`SleptInBedroom`、`SleptInBarracks`、`SleptOutside`、`SleptOnGround`、`SleptInCold`、`SleptInHeat`。
3. 根据环境尝试加入寒冷、炎热、户外、地面等记忆。
4. 仅当传入床位非空、等于 Pawn 的 `OwnedBed`、不是囚犯床、Pawn 不是 Ascetic 时，进入卧室/营房分支。
5. 使用传入床位的房间：Bedroom 对应 `SleptInBedroom`，Barracks 对应 `SleptInBarracks`；按房间 Impressiveness 阶段创建记忆，调用 `TryGainMemory`。
6. 最后调用 `actor.Notify_AddBedThoughts()`，通知 Pawn 的组件与 Ideo。

这里没有本模组现有的 `IsOfColony()` 条件。囚犯床被明确排除；其他 Pawn 是否实际获得记忆还受通用 Thought 接纳逻辑影响。v2 不复制资格判断。

**对旧审阅意见的修正：** 原版正常执行，且没有其他补丁或通知回调改变流程时，Postfix 中的 `SleptInBarracks` 不会是调用前残留的旧实例，因为旧实例已在方法开头删除。此基线下无需仅为识别旧实例添加 Prefix 快照。

但其他 Harmony Prefix 跳过原方法后，Postfix 仍可能执行。因此不能只检查记忆存在，还要确认原方法实际执行。其他模组在通知回调或 Postfix 中增删记忆，属于具体兼容测试范围，不宣称检测到的记忆必然来自未经改动的原版调用。

### 3.3 `TryGainMemory` 不保证加入成功

核查 `MemoryThoughtHandler.TryGainMemory(Thought_Memory, Pawn)`：先用 `ThoughtUtility.CanGetThought` 检查年龄阶段、特质、基因等限制；Social memory 还要求关联 Pawn 并检查目标年龄阶段。通过后设置 `pawn`、`otherPawn`，尝试合并，否则加入列表，再处理堆叠上限、后续记忆与替换关系。

该方法返回 void，「调用返回」不等于「候选已进入列表」。普通 `Thought_Memory` 同组判定包含 Def、阶段及关联 Pawn 规则，达到上限可能只刷新旧实例年龄。不同 Def 不会因同属营房效果自动替换：`stackLimit=1` 不能阻止朋友营房与仇人营房并存。

原版卧室、营房 XML 都是 `durationDays=1`、`stackLimit=1`。普通记忆每次 `ThoughtInterval` 增加 150 tick 年龄，非永久记忆在 `age > DurationTicks` 时可被清除。记忆列表、年龄、强制阶段和关联 Pawn 都参与保存读取。

### 3.4 结算之外的正面清理入口

`ThoughtUtility.RemovePositiveBedroomThoughts(Pawn)` 清除原版卧室、营房中实际 `MoodOffset() > 0` 的记忆，保留零值和负值。至少两个调用位置已核实：

- `Pawn_Ownership.OwnedBed` 值改变时，包括认领、取消或更换床位。
- `LayDown` 初始化时，当前床不等于自有床，且不是 deathrest。

它只识别两个原版 Def，不认识本模组的 14 个营房 Def。因此仅替换 `ApplyBedThoughts` 不足以保持生命周期一致，v2 需要跟随此清理入口。

```text
原版躺卧计时 / 离床条件
  → ApplyBedThoughts：清旧原版记忆 → 尝试加入新记忆 → 通知
  → Mod：清旧自定义营房记忆 → 有新原版营房结果时才尝试替换

床位归属变化 / 开始躺非自有床
  → RemovePositiveBedroomThoughts
  → Mod：同步清除 MoodOffset() > 0 的自定义营房记忆
```

房间角色或室友关系变化本身不触发实时重算，在原版下次结算时反映。床位归属变化触发的正面清理仍立即跟随原版。到期清理与存档恢复交给游戏。

### 3.5 特殊身份事件的原版入口与当前缺口

同一 1.7 程序集中，`JobDriver_Lovin.MakeNewToils` 在躺卧 toil 的 finish action 中创建 `GotSomeLovin`，按现有条件设置 `moodPowerFactor=1.5`，有 mood 时调用 `TryGainMemory(thought, Partner)`。这是事件记忆来源，不是营房结算来源。finish action 不能直接等同于「仅成功完成时才执行」；中断行为须在游戏内核对，本模组以原版实际提交该记忆为边界，不自行按动画或 job 状态补发。

现有特殊定义与本机原版事件定义都是 3 天、最多 10 层、`stackedEffectMultiplier=0.6`。两个现有特殊 Def 的基础 mood 均为 10、基础 opinion 均为 8，使用 `Thought_MemorySocial` 与 Adult 过滤。全部保持现状。普通 `Thought_Memory` 的合并刷新不能直接套用到这里：已核查 `Thought_MemorySocial.TryMergeWithExistingMemory` 返回 false，实际列表加入、按 Def 的上限淘汰与 UI 分组仍由游戏管理。

当前 `MemoryThoughtHandler_Patch` 仅覆盖一组特定身份方向；营房的四种特殊关系不等于事件已有四份对应定义。代码重新通过 Def 创建候选，没有转移原始记忆的 `moodPowerFactor`，并在嵌套 `TryGainMemory` 返回后无条件阻止原版继续执行。前者是确定的修正信息丢失，后者存在候选被拒绝而原版也被吞掉的风险；尚无证据把它们认定为用户历史每次漏发的唯一原因。

## 4. 两组功能的问题与修复范围

| 问题 | 当前证据 / v2 处理 |
| --- | --- |
| 近亲重复计数 | `BarracksContext` 中近亲增加专属计数后，又在 `counted == true` 时增加 `NumRelatives`；改为互斥分类 |
| 其他血亲漏计 | 好感达标的其他血亲进入 family 分支但 `counted == false`，既不增加亲属也不进入朋友分类；明确归 OtherFamily |
| 关系升级退回原版 | 朋友与好朋友分别计算过半；改为累计「至少是朋友」 |
| 三分之一边界错误 | `0.33f` 导致 1/3 被判为 ManyEnemies；改用整数比例比较 |
| 重复资格判断 | 移除重复的 `IsOfColony`、Ascetic、房间角色等 eligibility 判断，消费原版实际接纳的结果 |
| 床位来源不一致 | 当前使用 `actor.CurrentBed()`；改用原方法传入的 `bed`，覆盖离床回调 |
| 替换前先删原版 | 候选无法创建或被拒绝时可能丢失有效结果；确认候选接纳后才删除本次原版实例 |
| 清理遗漏 | 跟随 `RemovePositiveBedroomThoughts` 同步自定义正面效果 |
| 正常日志刷屏 | 默认只留初始化与明确异常，详细规则链放入调试开关 |
| 事件身份覆盖与预期混淆 | 逐条核对现有两个事件 Def 的称谓、年龄方向与双方视角；已有映射漏判要修，没有定义的组合保留原版，不挪用文案扩展身份 |
| 事件原版倍率丢失 | 保留传入原版记忆上的适用运行时修正，不重新创建一个丢失倍率的默认实例 |
| 事件候选拒绝后吞掉原版 | 特殊记忆未实际接纳时允许原始事件记忆按游戏规则继续处理；不能无条件 `return false` |
| 事件重复/堆叠判断不清 | 每次原版提交只处理该 Pawn 的一次结果；不同时发普通与特殊两份，不把 UI 未增加行数视为漏发 |

偶发黄字尚无对应日志与存档复现，不把任一问题直接认定为黄字根因。

## 5. v2 规则：明确决策

### 5.1 室友与互斥分类

- 从传入床位所在房间的 humanlike beds 的 `OwnersForReading` 收集室友，排除自己，按 Pawn 身份去重。
- 不要求室友当晚在床上或仍位于地图；不追踪实际同夜睡眠。
- opinion 使用 Actor 对室友的好感，可以是单向的。
- 每位室友恰好属于一个类别：Sibling、Parent、Child、OtherFamily、GoodFriend、Friend、Neutral、Enemy。
- 有血亲关系且 opinion >= 20：依次判断 Sibling、Parent、Child，其余归 OtherFamily；优先级不依赖关系枚举顺序。
- 未作为有效亲属时：opinion >= 60 为 GoodFriend，>= 20 为 Friend，<= -20 为 Enemy，其余 Neutral。
- Family 仍指 `familyByBloodRelation`，配偶等不因重构自动扩充为血亲类别。

阈值集中定义。实施规则时同步修正 `各种情况.md` 的旧 80/40 阈值，不保留两份冲突规则。所有主要类别人数之和等于去重后的室友人数；零室友直接 KeepVanilla，不进行除法。

### 5.2 优先级 A：唯一兄弟姐妹特殊营房

唯一室友分类为 Sibling、opinion >= 60，且双方分别为 Male/Female 时，按 Actor 性别与生理年龄选择已有四种特殊营房 Def。

使用已确认存在的 `AgeBiologicalTicks` 比较年龄，不取整岁数。生理年龄 ticks 完全相同、任一方 Gender.None、同性或好感不足时，继续普通亲属规则，不把年龄相等自动解释为弟弟或妹妹。

### 5.3 优先级 B：全员有效亲属

室友非空且全部属于四个亲属类别：全 Sibling → OnlySiblings，全 Child → OnlyChildren，全 Parent → OnlyParents，其余组合 → OnlyRelatives。Sibling + Parent 属于混合亲属，不属于 OnlySiblings。

### 5.4 优先级 C：其余混住情况

设 N 为全部室友人数，F 为四个亲属类别人数之和：

```text
G = GoodFriend + F
P = Friend + GoodFriend + F       // 至少是朋友
E = Enemy
```

主要分类互斥；G 与 P 是有意嵌套的派生统计，不将二者相加，各自不超过 N。

| 顺序 | 条件 | 结果（省略 SleptInBarracks_ 前缀） |
| --- | --- | --- |
| 1 | `2 * G >= N` 且 `E == 0` | GoodFriends_NoEnemies |
| 2 | `2 * P >= N` 且 `E == 0` | Friends_NoEnemies |
| 3 | `2 * G >= N` 且 `E > 0` | GoodFriends_WithEnemies |
| 4 | `2 * P >= N` 且 `E > 0` | Friends_WithEnemies |
| 5 | `E > 0` 且 `3 * E <= N` | SomeEnemies |
| 6 | `3 * E > N` | ManyEnemies |
| 7 | 其余 | KeepVanilla |

第一条命中即返回。精确 1/3 包含在 SomeEnemies 内；朋友规则优先于单独仇人规则，例如一个朋友加一个仇人命中 Friends_WithEnemies。

累计 P 是明确的玩法修正：3 Friend + 2 Neutral 时命中 Friends_NoEnemies；一个朋友升级后，2 Friend + 1 GoodFriend + 2 Neutral 仍命中该规则。这只修复分类导致的退步，不宣称所有特殊规则和 XML 数值满足全局心情单调性。

### 5.5 最小业务结构

Context 收集事实和唯一分类，统计只读；Evaluator 用按顺序的普通条件计算，不写记忆；Result 明确 KeepVanilla / Replace，并携带目标 Def、可选关联 Pawn、RuleId。小枚举或结果结构足够，不建立插件式规则注册系统。规则测试直接输入分类和必要属性，不要求启动完整 Verse 世界。

## 6. 两组功能的记忆更新与 Harmony 合约

### 6.1 主结算入口

首选 `ApplyBedThoughts(Pawn actor, Building_Bed bed)` Postfix，精确指定参数类型绑定目标：

1. 确认 `__runOriginal` 为 true，且有 mood/memories；原方法被跳过时不清除、不生成自定义记忆。
2. 原版正常结算已清旧环境记忆。此时清除本模组 14 个营房 Def 的旧记忆，必须先于「是否存在新原版营房记忆」判断：搬到卧室或原版不再给予营房效果，也要清旧自定义效果。
3. 获取当前原版 `SleptInBarracks` 实例；没有则结束，不自行补造，不按殖民者/囚犯/奴隶身份强行加入。
4. 用传入 bed 的房间生成 Context。缺必要上下文、零室友或 KeepVanilla 时，保留刚生成的原版实例。
5. 使用原版实例的 `CurStageIndex`，不重新算房间阶段。先校验候选 Def、记忆类型、阶段和关联 Pawn 条件。
6. 创建候选并调用游戏 `TryGainMemory`，确认候选实例实际进入列表。旧自定义营房记忆已清空，基线中无需依赖刷新旧实例判断成功；不能把 void 返回当作成功。
7. 仅在确认接纳后删除第 3 步的原版实例。候选被拒绝或配置不兼容时保留原版，并给出有界诊断。

旧自定义记忆在真实结算时清除是在跟随原版，不是失败后应恢复的历史状态；要保住的是本次原版已生成的有效结果。

此合约不保证任意其他模组副作用的事务回滚。其他补丁改变记忆对象或接纳流程时，单独复现并定义兼容策略，不为假想冲突添加回滚框架。API 拒绝与异常不能混为一谈，不用大范围吞异常伪装成功。

### 6.2 正面清理入口

在 `ThoughtUtility.RemovePositiveBedroomThoughts(Pawn pawn)` 加小型 Postfix：原方法实际执行且有 memories 时，按原版相同的 `MoodOffset() > 0` 条件清除本模组营房记忆。

这里不重新评估关系、不生成记忆、不删零值或负值，不修改 Sibling Lovin 或其他睡眠效果。使用实际偏移而非 XML `baseMoodEffect`，跟随 nullification 等游戏行为。

### 6.3 补丁形态与兼容边界

当前证据支持「一个结算 Postfix + 一个正面清理 Postfix」。删除旧无条件清理 Prefix 时，必须同时迁移清理责任，不能只删调用。验收不再限定「只能有一个 Patch」。未来版本确需前后状态时允许轻量 Prefix + `__state`，但不预先添加；暂不使用 Transpiler、编译器生成 lambda Patch 或新睡眠 session。

`Notify_AddBedThoughts` 在主 Postfix 之前执行，组件和 Ideo 回调可能先看到原版效果。不能承诺所有观察者只看到最终自定义效果；涉及该通知的模组须作为具体案例测试。

### 6.4 状态转换

| 事件 | 预期 |
| --- | --- |
| 朋友营房变仇人营房后新结算 | 清旧效果，生成至多一种新效果 |
| 新结算为 Bedroom / 不满足营房条件 | 清旧自定义营房记忆，保留其他原版环境结果 |
| 新结算仍为营房但无规则命中 | 清旧自定义记忆，保留本次原版实例 |
| 更换/取消床位归属 | 原版正面清理入口触发时同步清正值，零/负值不额外清除 |
| 首次期限前短睡结束，无结算 | 不新增效果；开始躺非自有床若触发正面清理则只同步清理 |
| 房间角色/关系改变但尚无结算 | 不实时重算，保留历史记忆直到原版清理或到期 |
| 候选无效/阶段不兼容/被拒绝 | 保留本次原版营房记忆，不 clamp 伪造可用效果 |
| 原方法被另一 Prefix 跳过 | 不把旧记忆当本次结算，不清理、不替换 |
| 保存/读取 | 由游戏保存年龄、stage、关联 Pawn 和期限，不重复补发 |

### 6.5 特殊身份事件的身份与上下文

- 只处理原版 `GotSomeLovin` 输入；普通睡眠、其他事件和已转换的特殊 Def 直接交回原流程。
- 以接收记忆的 Pawn 为视角，分别判断双方各自应选的已有 Def。不要在一方 Patch 内额外为另一方发放；另一方交由其自己的原版调用处理。
- 逐条固定现有两个 Def 的身份映射：保留其已有称谓与年龄方向，不把反向年龄组合或其他亲属自动套入现有文案。没有对应定义的组合保留原版，这是配置覆盖范围，不是可随意补文案的 bug。
- 使用生理年龄 ticks 明确比较长幼；完全同龄、Gender.None、非目标身份不强行命中。与营房的特殊规则分开，不顺手加入营房的好感门槛或房间条件。
- 关联 Pawn 按游戏重载语义取得：显式 `otherPawn` 优先，否则使用原记忆已携带的 `otherPawn`。无法取得有效目标时保留原调用及其诊断，不凭空构造对象。
- 不绕过原版对原事件的接纳资格，也不放宽候选既有 Adult、特质等过滤；没有 mood 或原版本来拒绝的情况，不强行补发。

### 6.6 特殊身份事件的替换与失败处理

在现有 `TryGainMemory(Thought_Memory, Pawn)` 语义边界修复，不更改事件概率、持续过程、冷却或原版其他副作用。具体补丁形态以最小实现为准，但必须满足：

1. 未命中现有身份映射，直接保留传入的原版实例和参数。
2. 命中时先检查候选 Def 与目标，使用原有特殊文案及基础值创建候选，保留原始事件上下文。至少保留 `moodPowerFactor`，并审查 `moodOffset`、`sourcePrecept`、有效的持续时间覆盖等实际传入修正；不得把候选的 Def、阶段或 social opinion 初始化值错误覆盖成原版值。
3. 候选通过游戏接纳逻辑处理；只有确认本次候选已接纳，才抑制本次普通事件记忆。不先删除同 Def 的历史事件，历史层数按游戏规则处理。
4. 候选因配置缺失、身份不匹配或接纳过滤被拒绝时，保留未改动的原实例，继续原版流程。回退不是保证强行获得普通心情：若原版也拒绝，应保持游戏原本结果。
5. 若沿用嵌套调用，特殊 Def 必须立即绕过本转换分支，避免递归转换；不能仅凭 void 返回或「已有同名旧记忆」判定本次成功。基线中以本次候选实例的接纳为证据，其他模组改变实例/合并逻辑时单独复现。
6. 不把普通异常当作可静默忽略的拒绝；其他模组改写接纳流程时不承诺通用事务回滚。

这会恢复原版运行时倍率。例如既有基础 mood=10 的特殊记忆，在原版传入 1.5 倍且没有其他修正时应体现为 15；XML 仍为 10。禁止为保持旧错误显示而把基础值改小，或把倍率硬编码进 XML。

### 6.7 两组功能隔离与持续时间

| 项目 | 营房关系心情 | 特殊身份事件心情 |
| --- | --- | --- |
| 触发 | 原版实际结算并接纳营房记忆 | 原版提交事件记忆，按原资格和已有映射转换 |
| 定义持续时间 | 现有 1 天 | 现有 3 天 |
| 重复处理 | 新结算清旧营房效果，至多一种；无新结算不补发 | 每次原版提交至多一个结果，按已有 10 层上限和 0.6 系数处理 |
| 清理 | 营房重新结算、原版正面床位清理、自然到期 | 原版事件记忆到期、上限等机制；不受营房清理影响 |
| UI 核查 | Def、stage、实际值和年龄 | Def、关联 Pawn、倍率、年龄和堆叠；不能只数显示行 |

营房换床、换房或室友变化不得删除事件历史；事件转换也不得覆盖营房记忆。两组均不在保存/读取时另行发放。

## 7. XML、诊断与存档兼容

当前静态核查：原版 Impressiveness、原版营房记忆与本模组 14 个营房 Def 均为 10 阶段。「仓库缺 XML、可能数量不同」不再是当前事实，也不能据此把黄字归因于阶段越界。

实施时校验 DefOf 完整、Def 名唯一、阶段数量和顺序语义一致、所需阶段非空、Def 能作为 memory。现有文案、数值、阶段顺序和过滤条件冻结；不以修 schema 为名改写它们。若实际发现现有定义无法满足目标版本，记录具体冲突并保留原版回退，单独提出必要修改，不擅自调整内容或通过 clamp 映射到错误档位。

加载时输出一次明确配置诊断，运行时同一问题不每次结算刷屏。营房调试记录包含 Actor、传入床位、分类、G/P/E/N、RuleId、原版 stage、接纳结果与清理原因；事件记录包含 Actor/目标、身份方向、原/目标 Def、输入倍率、本次候选接纳证据与回退原因。使用 DevMode 或简单开关，不先制作设置 UI 或独立日志框架。

两组都区分「原版未提交」「身份/规则未命中」「候选被拒绝」「原版也拒绝」「已接纳但 UI 分组/堆叠」及其他模组影响。未看到新的 UI 行不能直接报告为漏发；同样，也不能仅凭输出了一条成功日志就声称实际获得记忆。

保持 `defName`、packageId `shikiyuzu.zompanionship_barracks`、Harmony ID `shikiyuzu.companionship_barracks` 与程序集名 `Companionship Barracks` 稳定。packageId 的历史拼写不顺手更名。统一 namespace 时审查 XML 类名引用和存档序列化类型。

现有营房 Def 未指定 social thoughtClass，按所核查 `ThoughtDef.ThoughtClass` 使用普通 `Thought_Memory`；填写 `baseOpinionOffset` 不会自动成为 social memory。本次规则修复不擅自扩充社会好感效果；如要落实这类 XML 意图，另审关联 Pawn 与 social 类型。

Sibling Lovin 是本轮正式功能修复项，必须完成第 6.5–6.7 节和独立验收；方法签名存在或目录已隔离不算完成。未由 C# 直接引用的旧 XML 不直接删除；四份现有 XML 中的文案和数值均纳入内容保持检查。

## 8. 1.7 兼容与构建决策

### 8.1 区分运行、构建与功能

1. **旧 DLL 能否运行：** Waylith 有兼容机制，不能凭游戏使用 .NET 10 就断言 net472 DLL 必然失效；用当前基线包做最小加载验证。
2. **源码重新构建：** 当前主程序集目标是 net10.0，旧 Managed 与 UnityEngine.CoreModule 引用失效。拟将 1.7 构建转为 SDK-style `net10.0`，显式引用当前根目录的 `Assembly-CSharp.dll`、实际所需的 `UnityStubs.dll`，以及 Harmony 1.7 分支 DLL。其他 Waylith 依赖仅在真实编译需求出现时加入。
3. **功能兼容：** 编译或 Patch 注册成功不等于时序、清理、XML 与旧存档正确，必须通过第 10 节矩阵。

SDK-style 在这里有框架迁移理由，不只是外观现代化。实施前定位/安装 .NET 10 SDK；游戏自带 runtime 不替代 SDK。本次文档任务不安装 SDK、不改工程、不覆盖 DLL。

构建参数拟使用 `RimWorldDir` 指向 1.7 根目录，`HarmonyDir` 指向选中版本的 Assemblies。引用不复制到发布包，不提交游戏、UnityStubs、Harmony 或 .NET 运行时。同步 README 命令及实际成功构建的版本记录。

### 8.2 1.6 保留边界与发布布局

`e82e468` 保留为既有 1.6 代码/产物基线；v2 主要开发验收针对 1.7，不宣称新规则已回移到 1.6。若同包保留旧版入口，旧 net472 与新 net10.0 DLL 必须隔离路由，不能只加 supportedVersions 就宣布同一新 DLL 支持两版。当前没有 1.6 安装用于回归，修改共享 XML 时不得向旧版静默灌入未验证的新行为。

拟采用最小布局：

```text
About/
Common/Defs/                  # 仅放两版确认可共用的原有定义
1.6/Assemblies/               # 保留旧版产物
1.7/Assemblies/               # 新版独立产物
LoadFolders.xml               # 1.6: Common + 1.6；1.7: Common + 1.7
Source/CompanionshipBarracks/  # 单工程，按 Barracks / SiblingLovin 组织
docs/
README.md
```

引入 1.7 产物时再实施路由，不提前复制多套 XML。若定义存在版本差异，将差异放入对应版本目录。每版只加载一套同名 Def 和一个模组程序集；移走根目录旧 Assemblies 后验证加载路径，不依赖重名覆盖或改 DLL 名规避误加载。

不额外建多 assembly、DI 或通用 Infrastructure 层。Bootstrap、DefOf、日志、校验按规模组织，小 helper 可与使用处同文件。

About 的 1.7 声明与实际通过的验收一起交付；本轮保留当前 About，不提前宣布兼容。

## 9. 实施顺序与每步产出

### Phase 0：资产恢复与原版审计（已完成的部分）

资产恢复和旧环境编译见 `e82e468`；1.7 生命周期、版本、XML 静态核查见本文。没有 1.7 游戏内验证。旧计划的资产找回不重复执行。

### Phase 1：建立 1.7 最小兼容基线

- 重新记录游戏/Harmony 指纹，确认安装稳定；游戏二进制变化则复核第 3 节。
- 在 Core + 当前 Harmony + 本模组的最小组合中检查旧包加载结果，保留日志；不把已有规则 bug 当作新框架问题。
- 准备 .NET 10 SDK，调整必要工程引用和版本输出路由，保持玩法代码不变。
- 核对 `ApplyBedThoughts(Pawn, Building_Bed)`、`TryGainMemory(Thought_Memory, Pawn)` 与正面清理方法可绑定，验证 Harmony 3 注入和一次性注册。
- 产出独立的工程/加载适配变更及构建、启动证据，区分编译、加载、Patch 与规则错误。

### Phase 2：固定两组选择规则及内容基线

- 实现去重、互斥分类、只读统计及第 5 节规则。
- 用小型表驱动测试覆盖第 10.1 节，不依赖完整世界；另固定第 10.3 节的事件身份映射、双方视角和未覆盖组合。
- 同步旧规则说明，明确累计朋友、精确 1/3 是行为修正。
- 固定四份 XML 的现有文本、阶段、基础值、持续时间、堆叠与过滤条件作为比较基线，不新增身份或重新调平衡。

### Phase 3：分别实现两组替换修复与必要 XML 检查

- 迁移旧 Prefix 清理责任，落实第 6 节两个语义入口。
- 使用传入 bed 和原版 stage，确认接纳候选后再移除原版实例。
- XML/schema 审计在接入替换之前完成，不推迟到主逻辑完成后补救。
- 验证无候选、拒绝、原方法跳过、房间转换与正面清理。
- 独立落实事件身份映射、原版倍率/上下文保留、候选拒绝回退和递归退出；不把这组推迟成可选收尾任务。
- 两组可分开提交与验证，但都通过各自矩阵及互不清理对方记忆的联合检查后，才算功能阶段完成。
- 加入原因可追踪的调试记录，关闭正常结算刷屏。

### Phase 4：小规模目录整理与兼容收口

- 源码移入 Source，按两组功能组织并统一 namespace，保持 Def/ID 稳定。
- 保留简单单工程结构，不先大规模搬家再修 bug。
- 整理后重验两组入口和存档类型引用，不能用目录整理替代任一组功能验收。
- 核查版本加载路由、发布内容和文档命令，避免混装两版 DLL。

### Phase 5：游戏内与存档验收后交付

- 分层验证最小组合、用户实际模组组合、新场景和旧存档副本。
- 对照第 10.4 节核实所有既有文案、数值与定义设置未变；两组各有通过/未通过记录，不用营房通过代替事件通过。
- 失败记录场景、版本、原始日志、其他补丁参与情况；历史黄字复现且修复验证后才标记解决。
- 通过后更新 supportedVersions、发布 DLL 和说明。
- 如另需 v2 行为同步支持 1.6，补齐旧环境与独立回归，不从 1.7 推断。

## 10. 验证矩阵与完成条件

### 10.1 营房规则级验证

| 场景 | 预期 |
| --- | --- |
| 零室友 | KeepVanilla，无除零 |
| 采集结果重复出现同一床位主人 | 去重，只计一次 |
| 单个达标 Sibling / Parent / Child | FamilyCount=1，不是 2 |
| 单个达标其他血亲 | OtherFamily=1，不漏计 |
| 血亲 opinion=19 / 20 | 普通好感分类 / 有效亲属 |
| 非亲属 opinion=-21/-20/-19/19/20/59/60 | Enemy/Enemy/Neutral/Neutral/Friend/Friend/GoodFriend |
| 唯一兄弟姐妹满足特殊条件 | 四个性别/长幼分支各自命中已有 Def |
| 年龄 ticks 相同、Gender.None、同性或 opinion<60 | 不命中特殊长幼规则，继续普通规则 |
| 全部同类亲属 / Sibling+Parent | 对应单类 / OnlyRelatives |
| 1 GoodFriend / 1 Friend | GoodFriends_NoEnemies / Friends_NoEnemies |
| 3 Friend+2 Neutral → 2 Friend+1 GoodFriend+2 Neutral | 均为 Friends_NoEnemies |
| 1 Family+1 Friend+1 Neutral | Friends_NoEnemies，Family 计入派生 G 和 P |
| 1 GoodFriend+1 Enemy | GoodFriends_WithEnemies |
| 1 Friend+1 Enemy | Friends_WithEnemies |
| 1 Enemy+2 Neutral | SomeEnemies，精确包含 1/3 |
| 1 Enemy+1 Neutral | ManyEnemies |
| 全 Neutral | KeepVanilla |

每例检查类别总和等于 N，`0 <= G <= P <= N`、`0 <= E <= N`；另覆盖达到/未达到一半的边界。

### 10.2 营房与通用 1.7 游戏集成验证

| 类别 | 必测内容 |
| --- | --- |
| 加载 | 游戏/Harmony/模组 DLL 实际路径与版本；DefOf 完整；预期 Patch 注册一次；无重复 DLL |
| 时序 | 期限前短睡、到期在床结算、带离床标志结束、连续躺卧、快速离床再躺卧；不自行补发 |
| 资格 | 殖民者、囚犯床、奴隶、Ascetic、非自有床、医用床、无 mood；跟随原版实际结果 |
| 房间 | Bedroom/Barracks 转换、换床、零室友、室友增减与离图；使用传入 bed |
| 转换 | 自定义 A→B、A→原版、A→卧室、无新营房结果；不误删寒热/户外等记忆 |
| 正面清理 | 认领/取消/换床、开始躺非自有床；正值清除，零/负值保留；deathrest 按原版调用行为 |
| 接纳失败 | 无效候选、阶段不匹配、CanGetThought 拒绝时保留原版实例 |
| Harmony 组合 | 原方法被跳过时不动作；其他营房/bed/room replacement 模组单独记录顺序和通知回调影响 |
| 阶段 | 10 档 Impressiveness 的 stage、数值和描述正确，无静默 clamp |
| 存档 | 已有自定义记忆的旧存档副本；期限前/后保存读取；年龄、stage、关联 Pawn 正常，无重复补发 |

可用 DevMode 构造场景，但静态方法存在不能替代真实调用与存档验证。

### 10.3 特殊身份事件独立验收

| 场景 | 预期与证据 |
| --- | --- |
| 两个已有 Def 的合法身份映射 | 分别验证接收者视角、目标、年龄方向及最终 Def；显示该 Def 的现有文案和基础效果 |
| 未配置方向/身份、完全同龄、Gender.None | 不强行套用特殊文案，原版事件照常处理 |
| 目标仅在原记忆 `otherPawn` 中 | 遵循游戏重载语义取得目标；不因显式参数为空错误漏判 |
| 普通事件未命中 | 保留同一原版输入及其上下文，不改变原版数值或堆叠 |
| 原版实际未提交事件记忆 | 不因为看到动画或 job 开始而自行补发；中断场景记录原版是否调用 |
| 原版倍率为 1 / 1.5 | 候选保留相同倍率；没有其他修正时基础 10 对应实际 10 / 15，XML 不变 |
| 其他实际传入修正 | 检查 moodOffset、sourcePrecept、有效 duration override 等不会被无意清空；保留候选自身 Def/阶段/social 初始化值 |
| 候选 Def 缺失或接纳被拒绝 | 不吞原版；原实例继续正常接纳。若原版也拒绝则保持无记忆，不绕过资格 |
| 存在同名旧特殊记忆，但本次拒绝 | 不能把旧实例当作本次成功，仍按失败回退 |
| 双方各自提交一次原版记忆 | 各自至多一个普通或特殊结果，不由一方额外给另一方重复发放 |
| 嵌套 TryGainMemory / 已转换 Def | 直接绕过转换分支，无递归循环、无普通+特殊双发 |
| 连续事件及达到上限后再发生 | 不清空历史；按已有 3 天、10 层、0.6 系数由游戏处理；核对本次对象、年龄与淘汰，不能只数 UI 行 |
| 有无 mood、原版或候选过滤不满足 | 不强行补发，不放宽既有过滤，诊断区分拒绝发生在哪一步 |
| 保存/读取已有事件记忆 | Def、关联 Pawn、年龄、倍率与堆叠保存正常，不因载入补发 |
| 其他事件替换模组 | 记录实际入口、补丁顺序、输入 Def 和接纳结果；未验证组合明确标注 |

### 10.4 内容保持与两组联合验收

- 将实施前四份 `Defs/ThoughtDefs/*.xml` 与 `e82e468` 对照；实施后所有现有 label/description、各阶段基础 mood/opinion、阶段顺序、duration、stackLimit、stackedEffectMultiplier、过滤条件、Def 身份均须一致。路径迁移不构成内容修改理由。
- 房间每一档使用原有对应文案与数值，不改文字、不合并等级；事件使用原有对应文案，不复用错误称谓扩展覆盖面。
- 区分「配置值保持」与「正确命中/倍率恢复后的实际值变化」，验收记录原 Def/目标 Def、基础值和运行时修正。
- 同一 Pawn 同时具有营房与特殊事件记忆时，换房/换床/下一次睡眠结算只处理营房；下一次事件只处理事件，不相互清空。
- 分别验证营房 1 天与事件 3 天的定义及正常到期，保留原版其他合法提前清理机制；不得统一成同一时长或同一堆叠策略。
- 保持原版事件概率、过程、冷却以及非记忆副作用不变；加载/日志/文案检查不能替代两组实际调用与存档验证。

### 10.5 完成标准

- 1.7 构建、加载、行为矩阵各有实际证据，未完成项明确标注。
- 分类不重不漏，规则顺序、阈值与文档一致。
- 原版决定结算和资格，自定义记忆跟随完整清理生命周期。
- 正常结算后至多一种本模组营房效果，候选失败保住本次原版结果。
- 每次原版事件提交至多一个普通或特殊结果；已有身份映射正确，倍率与上下文保留，候选拒绝不吞掉原版流程，重复事件与存档通过独立验收。
- 两组现有文案、基础数值、持续时间、堆叠和过滤设置保持不变，互不清除对方记忆。两组之一未通过时，不报告本轮功能修复已完成。
- 默认日志不刷屏；验收组合中无本模组导致的未解释异常，不承诺其他模组永远无黄字。
- 发布包正确路由版本，旧存档 Def 身份稳定，支持声明与实测范围一致。

## 11. 明确不做

- 不自建睡眠 session，不修改原版计时字段，不强制每次醒来结算。
- 不重写现有文案、不重新平衡数值、不新增特殊身份内容，不更改原版事件概率或过程。
- 不重新定义殖民者/囚犯/奴隶资格，不为凑一个 Patch 遗漏已确认的清理入口。
- 不使用 Transpiler、复杂 DI、多 assembly、通用规则插件系统或假设中的全模组兼容层。
- 不把目录搬迁、SDK-style、supportedVersions 当成功能修复与兼容证明。
- 不在本次文档修订中改代码、改 XML、覆盖 DLL 或运行游戏。
