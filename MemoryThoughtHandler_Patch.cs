using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace Companionship_Barracks
{
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), typeof(Thought_Memory),
        typeof(Pawn))]
    public static class MemoryThoughtHandler_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            if (newThought.def != ThoughtDefOf.GotSomeLovin || otherPawn == null)
            {
                return true;
            }

            var pawn = __instance.pawn; // 获取当前Pawn
            if (pawn == null)
            {
                return true;
            }

            var  relations        = pawn.GetRelations(otherPawn);
            bool isSibling        = relations.Any(r => r.defName == PawnRelationDefOf.Sibling.defName);
            bool isOppositeGender = pawn.gender                        != otherPawn.gender;
            bool isOlder          = pawn.ageTracker.AgeBiologicalYears > otherPawn.ageTracker.AgeBiologicalYears;
            bool isBrotherWithYoungerSister = isSibling && isOppositeGender &&
                                              (pawn.gender == Gender.Male   && isOlder ||
                                               pawn.gender == Gender.Female && !isOlder);
            if (!isBrotherWithYoungerSister)
            {
                return true;
            }

            var myThought = isOlder ? MakeLoveDefOf.MakeLoveWithSister : MakeLoveDefOf.MadeLoveWithBrother;
            __instance.TryGainMemory(myThought, otherPawn);
            return false;
        }
    }
}