using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CompanionshipBarracks.Barracks;

[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
public static class BedThoughtPatch
{
    public static void Postfix(Pawn actor, Building_Bed bed, bool __runOriginal)
    {
        var memories = actor.needs?.mood?.thoughts?.memories;
        if (!__runOriginal || memories == null) return;

        // 原版本轮会清除旧的房间记忆；自定义记忆必须在相同结算点退出。
        foreach (var memory in memories.Memories.ToArray())
            if (MemoryReplacement.BarracksDefs.ContainsValue(memory.def)) memories.RemoveMemory(memory);

        var vanilla = memories.GetFirstMemoryOfDef(ThoughtDefOf.SleptInBarracks);
        if (vanilla == null) return;
        var room = bed?.GetRoom();
        if (room == null) return;
        var (choice, other) = BarracksContext.Select(actor, room);
        if (choice == BarracksThought.KeepVanilla) return;
        var def = MemoryReplacement.BarracksDefs[choice];
        if (!MemoryReplacement.Valid(def, vanilla.CurStageIndex))
        {
            MemoryReplacement.Warn("stage-" + choice, "候选阶段不可用，保留原版营房记忆：" + choice);
            return;
        }
        var candidate = (Thought_Memory)ThoughtMaker.MakeThought(def, vanilla.CurStageIndex);
        MemoryReplacement.CopyRuntime(vanilla, candidate);
        memories.TryGainMemory(candidate, other);
        bool accepted = memories.Memories.Contains(candidate);
        if (accepted) memories.RemoveMemory(vanilla);
        MemoryReplacement.Trace(actor, $"营房 {choice}, stage={vanilla.CurStageIndex}, accepted={accepted}");
    }
}

[HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.RemovePositiveBedroomThoughts))]
public static class PositiveBedroomCleanupPatch
{
    public static void Postfix(Pawn pawn, bool __runOriginal)
    {
        var memories = pawn.needs?.mood?.thoughts?.memories;
        if (!__runOriginal || memories == null) return;
        foreach (var memory in memories.Memories.ToArray())
            if (MemoryReplacement.BarracksDefs.ContainsValue(memory.def) && memory.MoodOffset() > 0)
                memories.RemoveMemory(memory);
    }
}
