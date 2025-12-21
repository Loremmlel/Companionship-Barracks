using HarmonyLib;
using RimWorld;
using Verse;

namespace Companionship_Barracks
{
	[HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
	public static class MemoryThoughtHandler_Patch
	{
		public static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
		{
			if (newThought.def != ThoughtDefOf.GotSomeLovin || otherPawn == null)
			{
				return true;
			}
			var pawn = __instance.pawn;
		}
	}
}
