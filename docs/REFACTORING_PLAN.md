# Companionship Barracks 重构计划

> 状态：设计稿 / 待实施  
> 目标版本：v2  
> 当前仓库默认分支：`master`

## 1. 背景

Companionship Barracks（陪伴营房）的核心目标，是修正 RimWorld 原版 `SleptInBarracks` 过于简单的人际模型：Pawn 不应仅因为“与其他人共享睡眠空间”就固定产生负面心情，而应根据同住者与自己的关系——朋友、家人、仇人等——获得不同的营房体验。

当前版本已经实现了这一核心想法，但代码是在初次接触 Harmony / RimWorld Mod 开发时逐步拼接完成的，长期实际游玩中出现了以下问题：

- 有时睡眠后正常获得自定义营房 Thought，有时没有；
- 偶发黄色日志 / 警告，难以定位来源；
- Patch 切入点与 Vanilla 生命周期之间的关系没有明确设计；
- 营房资格判断被 Mod 自己重复实现，与 Vanilla 可能发生漂移；
- 家人、朋友等关系统计存在确定的重复计数 bug；
- `Barracks` 与 `Sibling Lovin` 两个不同功能混在同一层目录和同一套基础代码中；
- 仓库仅保存了 C# 工程的一部分，不是一个完整、可直接识别的 RimWorld Mod 根目录；
- 自定义 Thought XML、About 信息、编译产物与源码之间的关系没有在仓库结构中表达；
- 设计文档中的阈值与实际代码已经发生漂移。

因此 v2 的目标不是继续在现有代码上打补丁，而是在保持 Mod 体量很小的前提下，重新明确边界和不变量，使它成为一个可理解、可调试、可升级的正常 RimWorld Mod 工程。

---

## 2. 重构原则

### 2.1 Vanilla 决定“是否产生营房 Thought”，本 Mod 只决定“产生哪一种”

本 Mod 不重新实现 RimWorld 的完整睡眠资格判断。

Vanilla 已经负责处理：

- Pawn 是否需要 mood；
- 当前是否在有效床位；
- 房间是否为 Bedroom / Barracks；
- Ascetic 等特殊情况；
- prisoner / slave / colonist 等原版差异；
- 睡眠 Thought 的实际结算时机；
- 房间 Impressiveness 对 Thought stage 的影响。

因此 v2 应尽量把 Companionship Barracks 定位为：

> **对 Vanilla 已经决定产生的 `SleptInBarracks` 进行语义替换。**

而不是：

> 再写一套“我认为现在应该产生营房 Thought”的判断。

这样可以最大程度跟随 RimWorld 原版本身的行为，也减少与其他 Mod 冲突的面积。

### 2.2 Harmony Patch 越薄越好

Harmony Patch 只负责：

1. 接收 Vanilla 调用；
2. 确认本次确实产生了 `SleptInBarracks`；
3. 构造纯业务上下文；
4. 调用 evaluator；
5. 将 Vanilla Thought 替换为自定义 Thought；
6. 在 Debug 模式下输出诊断信息。

关系分类、规则优先级、阈值判断都不应写在 Patch 内。

优先选择：

- Postfix；
- 明确的方法级 Patch；
- 少量稳定的公共/内部 API。

尽量避免：

- Transpiler；
- 修改 compiler-generated lambda；
- 在全局入口里塞入大量业务逻辑；
- 为了“精确”而 Patch 多个睡眠 Job 的生命周期细节。

### 2.3 业务规则应当可以脱离 RimWorld 睡眠流程独立验证

营房规则的核心输入应尽量简化成：

- 当前 Pawn；
- 当前房间；
- 同住者列表；
- 每个同住者的关系分类；
- 房间 impressiveness stage。

Evaluator 输出：

- 是否保持 Vanilla；
- 要替换成哪个 `ThoughtDef`；
- 是否有关联 Pawn；
- 命中的规则名称；
- 可选诊断信息。

这样业务逻辑即使仍然依赖 RimWorld 类型，也不依赖 Harmony Patch 的调用方式。

---

## 3. 已确认的当前问题

### 3.1 家人重复计数

当前 `BarracksContext.AnalyzeCohabitants()` 中，兄弟姐妹、父母、子女会先增加各自分类计数，又在 `counted == true` 时增加 `NumRelatives`。

随后：

```csharp
NumFamily = NumSiblings + NumParents + NumChildren + NumRelatives;
```

因此一个兄弟姐妹实际上会被算两次。

例如房间中只有 Pawn A 与其妹妹 Pawn B：

```text
NumCohabitants = 1
NumSiblings    = 1
NumRelatives   = 1
NumFamily      = 2
```

这会导致：

```csharp
NumFamily == NumCohabitants
```

错误地变成 `false`，从而使 `FamilyOnlyRule` 失效。

同时 `MixedCompanyRule` 又会使用：

```csharp
effectiveGoodFriends = NumGoodFriends + NumFamily;
```

于是理论上可能得到 `2 / 1 = 200%` 的“好朋友占比”。

#### v2 修复

每个同住者必须被分类到**唯一一个主要关系类别**：

```text
Sibling
Parent
Child
OtherFamily
GoodFriend
Friend
Neutral
Enemy
```

核心不变量：

```text
所有主要关系类别数量之和 == Cohabitants.Count
```

禁止通过多个相互重叠的整数计数器间接推导总人数。

---

### 3.2 Mod 重复实现 Vanilla eligibility

当前 `Toils_LayDown_Patch.Postfix` 会再次判断：

- `actor.IsOfColony()`；
- `actor.needs?.mood`；
- Ascetic；
- 当前房间；
- `RoomRoleDefOf.Barracks`。

这意味着 Vanilla 与 Mod 同时维护两套“什么时候该得到营房 Thought”的逻辑。

当 RimWorld 版本更新、特殊 Pawn 类型变化、其他 Mod 修改房间/床位行为时，两套逻辑可能产生分歧。

#### v2 修复

Postfix 应以 **Vanilla 本次是否实际生成 `ThoughtDefOf.SleptInBarracks`** 作为替换信号。

建议流程：

```text
Vanilla ApplyBedThoughts
        ↓
Postfix
        ↓
检查这次结算是否存在 Vanilla SleptInBarracks
        ├─ 否 → 立即返回
        └─ 是
             ↓
          构造 BarracksContext
             ↓
          BarracksEvaluator.Evaluate
             ↓
          删除 Vanilla SleptInBarracks
             ↓
          添加对应自定义 Thought
```

Mod 不重新定义“哪些 Pawn 有资格获得睡眠环境 Thought”。

---

### 3.3 `ApplyBedThoughts` 的触发时机被误认为“每次起床”

当前 Patch 选择 `Toils_LayDown.ApplyBedThoughts` 本身并不是明显错误。

这个方法是 Vanilla 集中结算多种睡眠环境 Thought 的语义位置，因此对于“替换 `SleptInBarracks`”而言是合适的 Patch 点。

需要明确的是：

> `ApplyBedThoughts` 的 Vanilla 生命周期并不等价于“每次睡醒必定调用一次”。

因此玩家观察到的“短睡一觉却没有新的营房 Thought”未必都是 Mod bug，也可能是 Vanilla 的结算时机所致。

#### v2 决策

Companionship Barracks **跟随 Vanilla 的睡眠 Thought 生命周期**。

不额外实现 sleep session，不强制每一次离床都产生新的 Thought。

理由：

- 本 Mod 的职责是改写 `SleptInBarracks` 的内容，而不是改写 RimWorld 的睡眠结算频率；
- 避免 Patch `JobDriver_LayDown` 等更脆弱的生命周期逻辑；
- 与其他改变睡眠 Job 的 Mod 更容易共存。

若未来确实需要“每晚固定结算一次”，应作为独立功能设计，而不是顺手混入本次重构。

---

### 3.4 “同住者”的定义当前并不清晰

当前实现通过房间所有 humanlike bed 的 `OwnersForReading` 获得 Cohabitants。

因此它表示的是：

> **登记住在同一个 Barracks 中的人。**

而不是：

> **昨晚实际同时睡在这个房间里的人。**

例如某个床位 Owner 正在商队中、住院、临时离开房间，仍可能被视作室友。

#### v2 决策

默认保留“长期室友 / 床位 Owner”语义。

理由：

- 更符合“Companionship Barracks / 陪伴营房”的长期共同生活概念；
- 不会因 Pawn 某晚临时值夜班、外出或被打断睡眠而反复改变房间的人际属性；
- 实现稳定，不需要追踪每个 Pawn 的当晚睡眠状态。

同时必须在 README / 设计文档中明确这一点。

若后续需要“实际同夜睡眠者”玩法，可作为设置项或 v3 设计。

---

### 3.5 设计文档与代码阈值漂移

旧文档中存在类似：

```text
好朋友：opinion > 80
朋友：opinion ∈ [40, 80]
```

而当前实现已经变为更低的阈值。

v2 不应继续让阈值散落在业务代码和 Markdown 中。

#### v2 修复

关系阈值集中定义，例如：

```csharp
internal static class RelationshipThresholds
{
    public const int GoodFriend = 60;
    public const int Friend = 20;
    public const int Enemy = -20;
}
```

最终数值在实施前重新确认一次，然后 README / 设计文档引用同一组定义。

如果以后需要 Mod Settings，再将这些常量迁移为设置值。

---

## 4. Patch 设计

### 4.1 Barracks Patch

保留：

```text
Toils_LayDown.ApplyBedThoughts
```

目标实现：**单一 Postfix**。

不再保留当前用于提前清除自定义 Thought 的 Prefix。

推荐职责：

```csharp
[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
internal static class ApplyBedThoughtsPatch
{
    [HarmonyPostfix]
    private static void Postfix(Pawn actor)
    {
        // 1. 最小防御性检查
        // 2. 确认 Vanilla SleptInBarracks 本次实际存在
        // 3. 构造 Context
        // 4. Evaluate
        // 5. Replace memory
        // 6. Debug log
    }
}
```

具体方法名是否可通过 `nameof` 表达，需要以目标 RimWorld 版本的程序集可见性为准；不要为了消除字符串而引入更复杂的反射封装。

### 4.2 不使用 Transpiler 替换 Vanilla `TryGainMemory`

虽然 Transpiler 可以做到“只替换 Vanilla 添加 `SleptInBarracks` 的那一条 IL”，但本 Mod 没有必要承担这种版本脆弱性。

Postfix + 最小 replacement 足够表达功能。

### 4.3 Sibling Lovin Patch 独立处理

当前 `MemoryThoughtHandler.TryGainMemory(Thought_Memory, Pawn)` Patch 用于拦截 `GotSomeLovin`，它与营房功能不是同一个业务领域。

该 Patch 的特点：

- 切入点非常全局；
- 但开头可以通过 `newThought.def != ThoughtDefOf.GotSomeLovin` 快速退出；
- 相比 Patch compiler-generated lovin lambda，方法名可能更稳定；
- 仍需审查递归、其他 Mod replacement、memory stack 与版本兼容行为。

v2 中先进行**结构隔离**，不要在 Barracks 重构时顺手大改：

```text
Features/
├─ Barracks/
└─ SiblingLovin/
```

完成 Barracks 稳定化后，再单独决定 Sibling Lovin 是否：

1. 保持当前 global memory interception；
2. 换用更局部 Patch；
3. 提供兼容层；
4. 从本 Mod 拆分成独立 Mod。

---

## 5. 业务模型设计

### 5.1 CohabitantRelationship

建议引入显式枚举：

```csharp
internal enum CohabitantRelationship
{
    Sibling,
    Parent,
    Child,
    OtherFamily,
    GoodFriend,
    Friend,
    Neutral,
    Enemy,
}
```

分类必须遵循固定优先级。

建议：

1. 先判断 blood family；
2. family 且满足最低 opinion 要求时，进入 Sibling / Parent / Child / OtherFamily；
3. 不作为有效 family 时，再根据 opinion 进入 GoodFriend / Friend / Enemy / Neutral。

这里要避免一个 Pawn 同时被算作 Family + Friend。

### 5.2 CohabitantInfo

可选结构：

```csharp
internal sealed class CohabitantInfo
{
    public Pawn Pawn { get; }
    public CohabitantRelationship Relationship { get; }
    public int Opinion { get; }
}
```

Context 保存 `IReadOnlyList<CohabitantInfo>`，所有数量从列表派生。

不再保存大量可变 `NumXxx { get; set; }`。

### 5.3 BarracksContext

建议只承担“收集事实”，不承担“决定 Thought”：

```text
Actor
Room
Cohabitants
NumCohabitants
```

并可提供只读派生属性：

```text
SiblingCount
ParentCount
ChildCount
OtherFamilyCount
FamilyCount
GoodFriendCount
FriendCount
EnemyCount
```

### 5.4 BarracksThoughtResult

Evaluator 不直接往 Pawn memory 写入内容，而返回结果：

```csharp
internal sealed class BarracksThoughtResult
{
    public ThoughtDef ThoughtDef { get; }
    public Pawn RelatedPawn { get; }
    public string RuleId { get; }
}
```

若选择保持 Vanilla，可以返回 `null` 或明确的 `KeepVanilla` 结果；推荐明确表达，避免 `null` 同时承担“无结果”和“保留原版”两种语义。

---

## 6. 规则优先级

v2 保持现有玩法的大方向，但重新用明确优先级实现。

### 6.1 特殊单人兄弟姐妹规则

当唯一同住者为有效兄弟姐妹，并满足特殊关系条件时，使用专门 Thought。

目前存在四类：

- BrotherWithSister；
- BrotherWithOlderSister；
- SisterWithBrother；
- SisterWithYoungerBrother。

年龄相同、`Gender.None`、关系不足等情况必须有明确 fallback。

### 6.2 Pure Family

如果所有同住者都是有效 family：

1. 全部为 siblings；
2. 全部为 children；
3. 全部为 parents；
4. 其他纯 family 组合。

注意“兄弟姐妹 + 父母”属于 `OnlyRelatives / MixedFamily` 一类，而不是 `OnlySiblings`。

### 6.3 Mixed Company

亲属与非亲属混住时，有效亲属可以按设计折算为 GoodFriend。

然后计算：

```text
goodFriendRatio
friendRatio
enemyRatio
```

比例统一使用 `NumCohabitants` 作为分母。

所有 Count 必须来自互斥分类，禁止出现大于 100% 的比例。

### 6.4 Vanilla fallback

如果没有任何自定义规则命中，保留 Vanilla `SleptInBarracks`，不要“删除以后又重新创建一个 Vanilla Thought”。

---

## 7. Thought stage 与 XML 安全性

当前代码直接使用：

```csharp
RoomStatDefOf.Impressiveness.GetScoreStageIndex(...)
```

得到的 stage index 创建所有自定义 Thought。

这要求自定义 `ThoughtDef` 的 stages 与 Vanilla `SleptInBarracks` 的 impressiveness stage 数量和索引语义兼容。

当前 GitHub 仓库缺少自定义 Thought XML，因此无法确认偶发黄字是否与 stage 越界、Def 配置或加载错误有关。

### v2 要求

将全部 Thought XML 纳入仓库，并建立显式校验：

- 所有 Barracks ThoughtDef 必须拥有与 Vanilla impressiveness stages 兼容的 stage 数量；
- 或者由统一 helper 对 stage index 做安全 clamp；
- 不允许各 XML 文件自行维护不同数量的 stages；
- DevMode 启动时可以执行一次 Def sanity check，发现不兼容时输出一条明确错误，而不是等 Pawn 睡觉时偶发爆黄字。

推荐优先选择：

> **所有 Barracks ThoughtDef 保持相同 stage schema。**

这样 mood 数值和描述可以不同，但 room impressiveness 的 stage 映射一致。

---

## 8. 日志与可诊断性

正常游玩时不应每次睡觉都 `Log.Message`。

当前日志：

```text
[陪伴营房] X 在营房内与 N 人同住。开始应用想法规则。
[陪伴营房] X 获得想法：...
```

长期游玩会产生大量噪音。

### v2 日志分级

默认：

- 仅输出 Mod 初始化信息；
- 仅异常情况使用 `Log.Warning` / `Log.Error`。

Debug 模式：

输出一次完整规则链，例如：

```text
[CompanionshipBarracks]
Actor: Alice
Room: Barracks #12
Cohabitants:
  - Bob: Sibling, opinion=83
  - Carol: GoodFriend, opinion=72
Counts: sibling=1, goodFriend=1, enemy=0, total=2
Rule: MixedCompany.GoodFriendsNoEnemies
Vanilla: SleptInBarracks stage=5
Replacement: SleptInBarracks_GoodFriends_NoEnemies stage=5
```

这样以后遇到“为什么这个 Pawn 得到了奇怪 Thought”，无需依赖猜测。

Debug 开关第一阶段可以是编译常量或静态布尔值；后续再考虑 Mod Settings UI。

---

## 9. 推荐仓库结构

目标：**仓库本身就是完整 RimWorld Mod 根目录。**

```text
Companionship-Barracks/
│
├─ About/
│  ├─ About.xml
│  └─ Preview.png
│
├─ 1.6/
│  ├─ Assemblies/
│  │  └─ CompanionshipBarracks.dll
│  │
│  └─ Defs/
│     └─ ThoughtDefs/
│        ├─ BarracksThoughts.xml
│        └─ SiblingLovinThoughts.xml
│
├─ Source/
│  └─ CompanionshipBarracks/
│     ├─ CompanionshipBarracks.csproj
│     │
│     ├─ Bootstrap/
│     │  └─ HarmonyBootstrap.cs
│     │
│     ├─ Defs/
│     │  ├─ CompanionshipBarracksDefOf.cs
│     │  └─ SiblingLovinDefOf.cs
│     │
│     ├─ Features/
│     │  ├─ Barracks/
│     │  │  ├─ BarracksContext.cs
│     │  │  ├─ CohabitantInfo.cs
│     │  │  ├─ CohabitantRelationship.cs
│     │  │  ├─ BarracksEvaluator.cs
│     │  │  ├─ BarracksThoughtResult.cs
│     │  │  └─ ApplyBedThoughtsPatch.cs
│     │  │
│     │  └─ SiblingLovin/
│     │     └─ GotSomeLovinPatch.cs
│     │
│     └─ Infrastructure/
│        ├─ ModLog.cs
│        └─ DefSanityCheck.cs
│
├─ docs/
│  ├─ REFACTORING_PLAN.md
│  └─ RULES.md
│
├─ README.md
├─ .gitignore
└─ CompanionshipBarracks.sln
```

如果需要支持多个 RimWorld 版本，可以进一步使用：

```text
Common/
1.5/
1.6/
```

但不应在没有实际多版本需求时提前复杂化。

---

## 10. `.csproj` 重构

当前工程使用大量相对路径直接指向本机 RimWorld 安装目录和 Workshop Harmony DLL，这不利于仓库迁移和重新构建。

v2 建议：

- 使用现代 SDK-style `.csproj`（如果目标 RimWorld / 编译环境允许）；
- 将 RimWorld 安装路径通过本地 `Directory.Build.props`、环境变量或未提交的用户配置指定；
- 不把个人 Steam 路径硬编码进仓库；
- Release 输出明确指向仓库内 `1.6/Assemblies/`；
- Debug 是否直接覆盖 Mod DLL 可作为本地开发配置；
- 仓库内记录“如何配置 RimWorldManagedDir / HarmonyDir”。

示例概念：

```xml
<PropertyGroup>
  <RimWorldDir Condition="'$(RimWorldDir)' == ''">...</RimWorldDir>
</PropertyGroup>
```

实际写法在实施阶段根据目标 .NET / RimWorld 版本确定。

---

## 11. Namespace 与命名

当前存在：

```text
Companionship_Barracks
CompanionshipBarracks
```

两种 namespace。

v2 统一为：

```text
CompanionshipBarracks
CompanionshipBarracks.Features.Barracks
CompanionshipBarracks.Features.SiblingLovin
```

Harmony ID 保持稳定：

```text
shikiyuzu.companionship_barracks
```

除非有明确兼容理由，不随重构修改 Harmony ID。

Patch 类采用语义名称：

```text
ApplyBedThoughtsPatch
GotSomeLovinPatch
```

而不是大量 `XXX_Patch` 与业务类混在同一目录。

---

## 12. 测试策略

RimWorld Mod 很难依赖传统单元测试完整覆盖 Verse / RimWorld 类型，因此采用三层验证。

### 12.1 规则级测试 / 可验证用例

至少为 evaluator 固定以下用例：

| 场景 | 预期 |
| --- | --- |
| 1 个 GoodFriend，无 Enemy | GoodFriends_NoEnemies |
| 1 个 Friend，无 Enemy | Friends_NoEnemies |
| 多人中 GoodFriend >= 50%，无 Enemy | GoodFriends_NoEnemies |
| Friend >= 50%，无 Enemy | Friends_NoEnemies |
| GoodFriend >= 50%，有 Enemy | GoodFriends_WithEnemies |
| Friend >= 50%，有 Enemy | Friends_WithEnemies |
| 少量 Enemy | SomeEnemies |
| 大量 Enemy | ManyEnemies |
| 全是 Sibling | OnlySiblings |
| 全是 Parent | OnlyParents |
| 全是 Child | OnlyChildren |
| 多种 Family 混合 | OnlyRelatives |
| Family + Friend | Family 按 GoodFriend 折算 |
| 无规则命中 | 保留 Vanilla |

并固定不变量：

```text
category count sum == cohabitant count
0 <= any ratio <= 1
```

### 12.2 DevMode 手工矩阵

至少验证：

- 普通殖民者；
- prisoner；
- slave；
- Ascetic；
- 单人 Bedroom；
- 双人 / 多人 Barracks；
- Pawn 中途被叫醒；
- 临时换床；
- 室友离开 Map；
- 房间 impressiveness 从 awful 到 wondrous；
- 保存 / 读取后 Thought 正常；
- 其他 Mod 修改 bed / room 时无异常日志。

### 12.3 日志验收

正常模式：

- 无持续刷屏；
- 无黄色日志；
- 无 Thought stage 越界；
- 无 null pawn / null room 异常。

Debug 模式：

- 任意一次 replacement 都能从日志看出分类和命中规则。

---

## 13. 分阶段实施

### Phase 0：恢复完整 Mod 资产

目标：让 GitHub 仓库成为完整来源。

任务：

- 找回实际游玩中的 `About.xml`；
- 找回所有自定义 ThoughtDef XML；
- 找回 Languages / Patch XML（若有）；
- 确认当前主要目标 RimWorld 版本；
- 将缺失资产提交至仓库。

在 XML 找回前，不进行最终 Thought stage 修复判断。

### Phase 1：只整理工程，不改变玩法

任务：

- 重组目录；
- 统一 namespace；
- 整理 `.csproj`；
- 提取 Bootstrap / DefOf / Features；
- 保持现有行为尽可能不变。

目标：先降低工程认知成本。

### Phase 2：重写 Barracks Context 与 Evaluator

任务：

- 引入互斥关系分类；
- 消除所有重复计数；
- 固定阈值；
- 引入 `BarracksThoughtResult`；
- 把规则从 Harmony Patch 中完全剥离；
- 增加规则级验证用例。

### Phase 3：缩薄 Harmony Patch

任务：

- 删除现有 Prefix；
- 让 Vanilla eligibility 成为唯一来源；
- 仅在 Vanilla `SleptInBarracks` 实际存在时替换；
- 保留 Vanilla 睡眠 Thought 生命周期；
- 增加 Debug trace。

### Phase 4：XML / stage 审计

任务：

- 审查所有 ThoughtDef stages；
- 与 Room Impressiveness stage 对齐；
- 增加 Def sanity check；
- 修复现有黄字。

### Phase 5：Sibling Lovin 独立审计

任务：

- 检查 `MemoryThoughtHandler.TryGainMemory` Patch 的递归安全；
- 检查与其他 replacement Thought Mod 的兼容；
- 决定保留、换 Patch 点或拆 Mod；
- 不影响 Barracks 主功能稳定性。

### Phase 6：实际游玩验收

使用真实存档连续游玩验证：

- 多次睡眠 Thought 结算；
- 室友增减；
- 房间角色变化；
- 加载存档；
- 长时间无黄色日志；
- 自定义 Thought 不重复、不残留、不错误覆盖 Vanilla 其他睡眠 Thought。

---

## 14. 明确不做的事情

本次重构不做：

- 为了“现代化”引入复杂 DI / service container；
- 将一个几百行的小 Mod 拆成大量 assembly；
- 使用 Transpiler 只为减少几行 replacement 代码；
- 自己实现一套完整睡眠 session 系统；
- 重新定义 Vanilla 的 prisoner / slave / colonist 睡眠资格；
- 一开始就制作复杂 Mod Settings UI；
- 为尚未支持的 RimWorld 旧版本提前写兼容层；
- 在 Barracks 重构过程中顺手重写 Sibling Lovin 的所有行为。

目标仍然是一个**小而清楚**的 Mod。

---

## 15. v2 验收标准

重构完成后应满足：

1. 仓库 clone 后能清楚看到完整 Mod 结构，而不是只有孤立的 Visual Studio 源码；
2. C# 源码全部位于 `Source/`；
3. 自定义 Def XML 全部纳入版本控制；
4. namespace 统一；
5. Barracks 只有一个主要 Harmony Patch；
6. Patch 不自行重复实现 Vanilla eligibility；
7. 一个 Cohabitant 只属于一个主要关系类别；
8. 所有比例均处于 `[0, 1]`；
9. Family-only 等规则可以稳定命中；
10. 自定义 Thought stage 与 room impressiveness 安全对应；
11. 默认日志不刷屏；
12. Debug 日志能够解释任意一次规则命中；
13. 短睡等情况下是否结算 Thought 与 Vanilla 保持一致；
14. Sibling Lovin 与 Barracks 在代码结构上完全隔离；
15. 连续实际游玩不再出现当前已知的随机规则异常和黄色日志。

---

## 16. 最终架构一句话说明

> **RimWorld 决定什么时候产生 `SleptInBarracks`；Companionship Barracks 读取室友关系，用一个可独立验证的 evaluator 决定应该把它替换成什么。**

只要后续实现始终守住这条边界，Mod 的 Harmony 层就会保持很薄，规则也不会再和游戏生命周期缠在一起。