using RimWorld;

namespace Companionship_Barracks
{
    [DefOf]
    public static class CompanionshipBarracksDefOf
    {
        public static ThoughtDef SleptInBarracks_GoodFriends_NoEnemies;
        public static ThoughtDef SleptInBarracks_Friends_NoEnemies;
        public static ThoughtDef SleptInBarracks_GoodFriends_WithEnemies;
        public static ThoughtDef SleptInBarracks_Friends_WithEnemies;
        public static ThoughtDef SleptInBarracks_SomeEnemies;
        public static ThoughtDef SleptInBarracks_ManyEnemies;

        public static ThoughtDef SleptInBarracks_OnlySiblings;
        public static ThoughtDef SleptInBarracks_OnlyParents;
        public static ThoughtDef SleptInBarracks_OnlyChildren;
        public static ThoughtDef SleptInBarracks_OnlyRelatives;

        public static ThoughtDef SleptInBarracks_BrotherWithSister;
        public static ThoughtDef SleptInBarracks_SisterWithBrother;
        public static ThoughtDef SleptInBarracks_SisterWithYoungerBrother;
        public static ThoughtDef SleptInBarracks_BrotherWithOlderSister;

        static CompanionshipBarracksDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CompanionshipBarracksDefOf));
        }
    }
}