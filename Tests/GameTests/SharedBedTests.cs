using CompanionshipBarracks.SharedBed;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CompanionshipBarracks.GameTests;

public static partial class GameTests
{
    private static Pawn OpinionActor;
    private static readonly Dictionary<Pawn, int> BedOpinions = new();
    private static void BedOpinion(Pawn_RelationsTracker __instance, Pawn other, ref int __result)
    {
        if (__instance == OpinionActor?.relations && BedOpinions.TryGetValue(other, out int opinion)) __result = opinion;
    }

    private static void SharedBedTests(Map map)
    {
        var siblingDef = DefDatabase<ThoughtDef>.GetNamed("SharedBedWithCloseSibling");
        var originalDef = DefDatabase<ThoughtDef>.GetNamed("SharedBed");
        Check(!siblingDef.IsMemory && siblingDef.stages.Single().baseMoodEffect == 3, "Sibling bed comfort is one situational +3 effect");
        foreach (var obsolete in new[] { "SleepWithSister", "SleepWithBrother", "SisterSleepDisturbed", "BrotherSleepDisturbed" })
            Check(DefDatabase<ThoughtDef>.GetNamedSilentFail(obsolete) == null, "Obsolete definition removed: " + obsolete);
        var worker = AccessTools.Method(typeof(ThoughtWorker_SharedBed), "CurrentStateInternal");
        var value = AccessTools.PropertyGetter(typeof(Thought_SharedBed), "BaseMoodOffset");
        var disturbed = AccessTools.Method(typeof(Pawn), "CheckForDisturbedSleep");
        foreach (var method in new[] { worker, value, disturbed })
            Check(Harmony.GetPatchInfo(method)?.Owners.Contains("shikiyuzu.companionship_barracks") == true, "Shared-bed patch registered: " + method.Name);

        var a = NewPawn(map, new IntVec3(65, 0, 65), Gender.Female, 25);
        var b = NewPawn(map, new IntVec3(66, 0, 65), Gender.Male, 25);
        var mother = NewPawn(map, new IntVec3(67, 0, 65), Gender.Female, 50);
        var father = NewPawn(map, new IntVec3(68, 0, 65), Gender.Male, 50);
        var stranger = NewPawn(map, new IntVec3(69, 0, 65), Gender.Female, 30);
        foreach (var p in new[] { a, b, mother, father, stranger }) p.relations.ClearAllRelations();
        a.SetMother(mother); b.SetMother(mother); a.SetFather(father); b.SetFather(father);
        var bed = (Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("DoubleBed"), ThingDefOf.WoodLog);
        bed.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(bed, a.Position, map);
        a.ownership.ClaimBedIfNonMedical(bed); b.ownership.ClaimBedIfNonMedical(bed);
        Check(a.ownership.OwnedBed == bed && b.ownership.OwnedBed == bed, "Non-partner siblings own the same double bed");
        bool Penalty(Pawn p) => ((ThoughtState)worker.Invoke(originalDef.Worker, [p])).Active;
        bool Comfort(Pawn p) => ((ThoughtState)AccessTools.Method(typeof(ThoughtWorker_CloseSiblingBed), "CurrentStateInternal").Invoke(siblingDef.Worker, [p])).Active;
        float Offset(Pawn p) { var t = ThoughtMaker.MakeThought(originalDef); t.pawn = p; return (float)value.Invoke(t, null); }
        var harness = new Harmony("companionship.tests.sharedbed");
        OpinionActor = a;
        harness.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionOf)),
            postfix: new HarmonyMethod(typeof(GameTests), nameof(BedOpinion)));
        try
        {
            foreach (int opinion in new[] { -100, 59, 60, 100 })
            {
                BedOpinions[b] = opinion;
                Check(!Penalty(a) && Offset(a) == 0 && Comfort(a) == (opinion >= 60), "Sibling shared-bed boundary " + opinion);
            }
            OpinionActor = b; BedOpinions[a] = 59;
            Check(!Comfort(b), "Sibling comfort uses receiver's own opinion");
            OpinionActor = a;
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
            Check(!Penalty(a) && !Comfort(a) && !SharedBedRules.IgnoreDisturbance(a, b), "Partner takes precedence over sibling-specific rules");
            a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b);
            b.SetFather(null);
            Check(SharedBedRules.Siblings(a, b) && !Penalty(a) && Comfort(a), "Half-sibling shares the sibling rules");
            b.SetFather(father);
            b.ownership.UnclaimBed(); mother.ownership.ClaimBedIfNonMedical(bed);
            Check(!Penalty(a) && !Penalty(mother) && !Comfort(a), "Parent and child are exempt in both directions, without sibling bonus");
            mother.ownership.UnclaimBed(); stranger.ownership.ClaimBedIfNonMedical(bed);
            BedOpinions[stranger] = -20;
            Check(Penalty(a) && Offset(a) == -6 && !Comfort(a), "Unrelated owner retains vanilla -6 at opinion -20");
            // 将母亲改为另一方的子女，得到祖辈；祖辈不属于本规则的近亲范围。
            mother.SetMother(stranger);
            Check(!SharedBedRules.CloseFamily(a, stranger) && Penalty(a), "Grandparent remains outside close-family exemption");
            mother.SetMother(null);
            stranger.ownership.UnclaimBed(); b.ownership.ClaimBedIfNonMedical(bed);
            bed.OwnersForReading.Add(stranger);
            try { Check(Penalty(a) && Offset(a) == -6 && Comfort(a), "Multi-owner bed still penalizes unrelated owner alongside sibling comfort"); }
            finally { bed.OwnersForReading.Remove(stranger); }

            a.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced);
            a.jobs.curDriver.asleep = true;
            Check(!a.Awake(), "Disturbance fixture is genuinely asleep");
            var last = AccessTools.Field(typeof(Pawn), "lastSleepDisturbedTick");
            var memories = a.needs.mood.thoughts.memories;
            void Noise(Pawn source)
            {
                memories.Memories.Clear(); last.SetValue(a, 0);
                disturbed.Invoke(a, [source]);
            }
            Noise(b);
            Check(memories.Memories.Count == 0 && (int)last.GetValue(a) == 0, "Same-bed sibling creates no memory and consumes no disturbance cooldown");
            b.ownership.UnclaimBed();
            Noise(b);
            Check(memories.GetFirstMemoryOfDef(ThoughtDefOf.SleepDisturbed) != null, "Sibling outside shared bed can still disturb sleep");
            mother.ownership.ClaimBedIfNonMedical(bed);
            Noise(mother);
            Check(memories.GetFirstMemoryOfDef(ThoughtDefOf.SleepDisturbed) != null, "Same-bed parent keeps vanilla disturbance behavior");
            Noise(stranger);
            Check(memories.GetFirstMemoryOfDef(ThoughtDefOf.SleepDisturbed) != null, "Other person's noise is not suppressed");
            mother.ownership.UnclaimBed(); b.ownership.ClaimBedIfNonMedical(bed);
            a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
            Noise(b);
            Check(memories.Memories.Count == 0, "Partner's vanilla disturbance exemption still applies");
            a.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, b);
            a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            b.ownership.UnclaimBed();
            Check(!Comfort(a), "Sibling comfort ends when shared-bed ownership ends");
        }
        finally
        {
            harness.UnpatchAll("companionship.tests.sharedbed");
            OpinionActor = null; BedOpinions.Clear();
        }
    }
}
