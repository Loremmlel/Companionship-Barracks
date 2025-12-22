using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Companionship_Barracks
{
	[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
	public static class Toils_LayDown_Patch
	{
		private static readonly List<ThoughtRule> _thoughtRules = new List<ThoughtRule>
		{
			new SiblingSpecialRule(),
			new FamilyOnlyRule(),
			new MixedCompanyRule()
		};

		private static readonly List<ThoughtDef> _modThoughts = new List<ThoughtDef>
		{
			CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_NoEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_Friends_NoEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_WithEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_Friends_WithEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_SomeEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_ManyEnemies,
			CompanionshipBarracksDefOf.SleptInBarracks_OnlySiblings,
			CompanionshipBarracksDefOf.SleptInBarracks_OnlyParents,
			CompanionshipBarracksDefOf.SleptInBarracks_OnlyChildren,
			CompanionshipBarracksDefOf.SleptInBarracks_OnlyRelatives,
			CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithSister,
			CompanionshipBarracksDefOf.SleptInBarracks_SisterWithBrother,
			CompanionshipBarracksDefOf.SleptInBarracks_SisterWithYoungerBrother,
			CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithOlderSister
		};

		static bool Prefix(Pawn actor)
		{
			RemoveModThoughts(actor);
			return true;
		}

		static void Postfix(Pawn actor)
		{
			if (!actor.IsColonist || actor.needs?.mood == null || actor.story.traits.HasTrait(TraitDefOf.Ascetic))
			{
				return;
			}

			var room = actor.CurrentBed()?.GetRoom();
			if (room == null || room.Role != RoomRoleDefOf.Barracks)
			{
				return;
			}

			var context = new BarracksContext(actor, room);
			if (context.NumCohabitants == 0)
			{
				return;
			}
			Log.Message($"[陪伴营房] {actor.Name.ToStringShort} 在营房内与 {context.NumCohabitants} 人同住。开始应用想法规则。");

			ThoughtDef finalThoughtDef = null;
			foreach (var rule in _thoughtRules.Where(rule => rule.CanApply(context)))
			{
				finalThoughtDef = rule.GetThought(context);
				if (finalThoughtDef != null)
				{
					break;
				}
			}
			if (finalThoughtDef == null)
			{
				return;
			}

			int stageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(room.GetStat(RoomStatDefOf.Impressiveness));
			actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
			var thought = ThoughtMaker.MakeThought(finalThoughtDef, stageIndex);
			actor.needs.mood.thoughts.memories.TryGainMemory(thought, null);
			Log.Message($"[陪伴营房] {actor.Name.ToStringShort} 获得想法：{finalThoughtDef.defName}（等级{stageIndex}）。");
		}

		private static void RemoveModThoughts(Pawn pawn)
		{
			if (pawn.needs?.mood == null)
			{
				return;
			}

			foreach (var thoughtDef in _modThoughts)
			{
				pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thoughtDef);
			}
		}
	}
}
