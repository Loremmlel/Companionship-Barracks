using System.Linq;
using RimWorld;
using Verse;

namespace CompanionshipBarracks.SharedBed;

public static class SharedBedRules
{
    public static bool Siblings(Pawn a, Pawn b) => a.GetRelations(b)
        .Any(r => r == PawnRelationDefOf.Sibling || r == PawnRelationDefOf.HalfSibling);

    public static bool CloseFamily(Pawn a, Pawn b) => a.GetRelations(b)
        .Any(r => r == PawnRelationDefOf.Parent || r == PawnRelationDefOf.Child
            || r == PawnRelationDefOf.Sibling || r == PawnRelationDefOf.HalfSibling);

    public static Pawn WorstUnrelatedBedOwner(Pawn pawn) => pawn.ownership?.OwnedBed?.OwnersForReading
        .Where(other => other != pawn && !LovePartnerRelationUtility.LovePartnerRelationExists(pawn, other) && !CloseFamily(pawn, other))
        .OrderBy(other => pawn.relations.OpinionOf(other)).FirstOrDefault();

    public static bool HasCloseSiblingBedmate(Pawn pawn) => pawn.ownership?.OwnedBed?.OwnersForReading
        .Any(other => other != pawn && !LovePartnerRelationUtility.LovePartnerRelationExists(pawn, other)
            && Siblings(pawn, other) && pawn.relations.OpinionOf(other) >= 60) == true;

    public static bool IgnoreDisturbance(Pawn pawn, Pawn source)
    {
        // 伴侣仍交给原版；只豁免同一登记床位中的兄弟姐妹，不豁免房间里其他人的噪声。
        if (source == null || source == pawn || LovePartnerRelationUtility.LovePartnerRelationExists(pawn, source)) return false;
        var bed = pawn.ownership?.OwnedBed;
        return bed != null && source.ownership?.OwnedBed == bed && bed.OwnersForReading.Contains(pawn)
            && bed.OwnersForReading.Contains(source) && Siblings(pawn, source);
    }
}

public class ThoughtWorker_CloseSiblingBed : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) =>
        p.RaceProps.Humanlike && SharedBedRules.HasCloseSiblingBedmate(p);
}
