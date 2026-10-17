using RimWorld;

namespace CompanionshipBarracks.SiblingLovin
{
    [DefOf]
    public static class MakeLoveDefOf
    {
        public static ThoughtDef MakeLoveWithSister;
        public static ThoughtDef MakeLoveWithBrother;

        static MakeLoveDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MakeLoveDefOf));
        }
    }
}
