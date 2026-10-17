using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CompanionshipBarracks.SharedBed;

[HarmonyPatch(typeof(ThoughtWorker_SharedBed), "CurrentStateInternal")]
public static class SharedBedPenaltyWorkerPatch
{
    public static void Postfix(Pawn p, ref ThoughtState __result, bool __runOriginal)
    {
        if (__runOriginal && __result.Active && SharedBedRules.WorstUnrelatedBedOwner(p) == null)
            __result = false;
    }
}

[HarmonyPatch(typeof(Thought_SharedBed), "BaseMoodOffset", MethodType.Getter)]
public static class SharedBedPenaltyValuePatch
{
    public static void Postfix(Thought_SharedBed __instance, ref float __result, bool __runOriginal)
    {
        if (!__runOriginal) return;
        var other = SharedBedRules.WorstUnrelatedBedOwner(__instance.pawn);
        // 多人床仍保留对非近亲的原版惩罚；不能因为床上有亲属就一并豁免其他人。
        __result = other == null ? 0f : Math.Min(0.05f * __instance.pawn.relations.OpinionOf(other) - 5f, 0f);
    }
}

[HarmonyPatch(typeof(Pawn), "CheckForDisturbedSleep")]
public static class SiblingSleepDisturbancePatch
{
    public static bool Prefix(Pawn __instance, Pawn source) => !SharedBedRules.IgnoreDisturbance(__instance, source);
}
