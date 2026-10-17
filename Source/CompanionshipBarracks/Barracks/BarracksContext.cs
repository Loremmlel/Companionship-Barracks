using System.Linq;
using RimWorld;
using Verse;

namespace CompanionshipBarracks.Barracks;

public static class BarracksContext
{
    public static Pawn[] Residents(Pawn actor, Room room) => room.ContainedBeds
        .Where(b => b.def.building.bed_humanlike)
        .SelectMany(b => b.OwnersForReading).Where(p => p != actor).Distinct().ToArray();

    public static Relationship Classify(Pawn actor, Pawn other)
    {
        var relations = actor.GetRelations(other).ToArray();
        return RelationshipRules.Classify(actor.relations.OpinionOf(other), relations.Any(r => r.familyByBloodRelation),
            relations.Contains(PawnRelationDefOf.Sibling), relations.Contains(PawnRelationDefOf.Parent), relations.Contains(PawnRelationDefOf.Child));
    }

    public static (BarracksThought thought, Pawn other) Select(Pawn actor, Room room)
    {
        var residents = Residents(actor, room);
        var relationships = residents.Select(p => Classify(actor, p)).ToArray();
        if (residents.Length == 1 && relationships[0] == Relationship.Sibling && actor.relations.OpinionOf(residents[0]) >= 60)
        {
            var other = residents[0];
            long age = actor.ageTracker.AgeBiologicalTicks, otherAge = other.ageTracker.AgeBiologicalTicks;
            if (age != otherAge && IdentityRules.OppositeSex(actor, other))
            {
                var thought = actor.gender == Gender.Male
                    ? age > otherAge ? BarracksThought.BrotherWithSister : BarracksThought.BrotherWithOlderSister
                    : age > otherAge ? BarracksThought.SisterWithYoungerBrother : BarracksThought.SisterWithBrother;
                return (thought, other);
            }
        }
        return (RelationshipRules.Select(relationships), null);
    }
}
