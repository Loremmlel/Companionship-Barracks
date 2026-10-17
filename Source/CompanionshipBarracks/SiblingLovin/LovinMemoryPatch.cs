using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CompanionshipBarracks.SiblingLovin;

[HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
public static class LovinMemoryPatch
{
    public static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
    {
        if (newThought?.def != ThoughtDefOf.GotSomeLovin) return true;
        var actor = __instance.pawn;
        var other = otherPawn ?? newThought.otherPawn;
        if (actor == null || other == null || !ThoughtUtility.CanGetThought(actor, newThought.def)
            || !newThought.def.socialTargetDevelopmentalStageFilter.Has(other.DevelopmentalStage)) return true;
        var def = Select(actor, other);
        if (!MemoryReplacement.Valid(def, 0)) return true;

        var candidate = (Thought_Memory)ThoughtMaker.MakeThought(def);
        MemoryReplacement.CopyRuntime(newThought, candidate);
        // 再走原版接纳/过滤/堆叠流程；特殊 Def 不会再次进入替换分支。
        __instance.TryGainMemory(candidate, other);
        bool accepted = __instance.Memories.Contains(candidate);
        MemoryReplacement.Trace(actor, $"事件 {def.defName}, accepted={accepted}");
        return !accepted;
    }

    public static ThoughtDef Select(Pawn actor, Pawn other)
    {
        if (actor.DevelopmentalStage != DevelopmentalStage.Adult || other.DevelopmentalStage != DevelopmentalStage.Adult
            || !IdentityRules.OppositeSex(actor, other) || !actor.GetRelations(other).Contains(PawnRelationDefOf.Sibling)) return null;
        long age = actor.ageTracker.AgeBiologicalTicks, otherAge = other.ageTracker.AgeBiologicalTicks;
        if (age == otherAge) return null;
        if (actor.gender == Gender.Male && age > otherAge) return MakeLoveDefOf.MakeLoveWithSister;
        if (actor.gender == Gender.Female && age < otherAge) return MakeLoveDefOf.MakeLoveWithBrother;
        return null;
    }
}
