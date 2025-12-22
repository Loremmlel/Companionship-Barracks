using RimWorld;

namespace Companionship_Barracks
{
    [DefOf]
    public static class MakeLoveDefOf
    {
        public static ThoughtDef MakeLoveWithSister;
        public static ThoughtDef MadeLoveWithBrother;
        
        static MakeLoveDefOf()
        {   
            DefOfHelper.EnsureInitializedInCtor(typeof(MakeLoveDefOf));
        }
    }
}