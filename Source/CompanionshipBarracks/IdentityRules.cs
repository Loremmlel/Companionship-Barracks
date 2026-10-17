using Verse;

namespace CompanionshipBarracks;

internal static class IdentityRules
{
    internal static bool OppositeSex(Pawn a, Pawn b) =>
        (a.gender == Gender.Male && b.gender == Gender.Female) || (a.gender == Gender.Female && b.gender == Gender.Male);
}
