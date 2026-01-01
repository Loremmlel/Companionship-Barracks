using RimWorld;
using Verse;

namespace Companionship_Barracks
{
	public abstract class ThoughtRule
	{
		public abstract bool               CanApply(BarracksContext   context);
		public abstract (ThoughtDef, Pawn) GetThought(BarracksContext context);
	}

	public class SiblingSpecialRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context)
		{
			return context.NumCohabitants == 1 && context.NumSiblings == 1;
		}

		public override (ThoughtDef, Pawn) GetThought(BarracksContext context)
		{
			var sibling = context.Cohabitants[0];
			int opinion = context.Actor.relations.OpinionOf(sibling);
			if (opinion < 60 || context.Actor.gender == sibling.gender || sibling.gender == Gender.None)
			{
				return (null, null); // 不满足特殊条件，退回至其他规则
			}

			bool isOlder = context.Actor.ageTracker.AgeBiologicalYears > sibling.ageTracker.AgeBiologicalYears;
			switch (context.Actor.gender)
			{
				case Gender.Male:
					return isOlder
						? (CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithSister, sibling)
						: (CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithOlderSister, sibling);
				// 女性
				case Gender.Female:
					return isOlder
						? (CompanionshipBarracksDefOf.SleptInBarracks_SisterWithYoungerBrother, sibling)
						: (CompanionshipBarracksDefOf.SleptInBarracks_SisterWithBrother , sibling);
				case Gender.None:
				default:
					return (null, null);
			}
		}
	}

	public class FamilyOnlyRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context)
		{
			return context.NumFamily > 0 && context.NumFamily == context.NumCohabitants;
		}

		public override (ThoughtDef, Pawn) GetThought(BarracksContext context)
		{
			if (context.NumSiblings == context.NumCohabitants) return (CompanionshipBarracksDefOf.SleptInBarracks_OnlySiblings, null);
			if (context.NumParents == context.NumCohabitants) return (CompanionshipBarracksDefOf.SleptInBarracks_OnlyParents, null);
			if (context.NumChildren == context.NumCohabitants) return (CompanionshipBarracksDefOf.SleptInBarracks_OnlyChildren, null);
			return (CompanionshipBarracksDefOf.SleptInBarracks_OnlyRelatives, null);
		}
	}

	public class MixedCompanyRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context) => true;

		public override (ThoughtDef, Pawn) GetThought(BarracksContext context)
		{
			// 有亲戚也有非亲戚同住时，将亲戚计入好朋友
			int effectiveGoodFriends = context.NumGoodFriends + context.NumFamily;

			float goodFriendRatio = (float)effectiveGoodFriends / context.NumCohabitants;
			float friendRatio = (float)context.NumFriends / context.NumCohabitants;
			float enemyRatio = (float)context.NumEnemies / context.NumCohabitants;

			if (goodFriendRatio >= 0.5f && context.NumEnemies == 0) return (CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_NoEnemies, null);
			if (friendRatio >= 0.5f && context.NumEnemies == 0) return (CompanionshipBarracksDefOf.SleptInBarracks_Friends_NoEnemies, null);
			if (goodFriendRatio >= 0.5f && context.NumEnemies > 0) return (CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_WithEnemies, null);
			if (friendRatio >= 0.5f && context.NumEnemies > 0) return (CompanionshipBarracksDefOf.SleptInBarracks_Friends_WithEnemies, null);
			if (enemyRatio > 0 && enemyRatio <= 0.33f) return (CompanionshipBarracksDefOf.SleptInBarracks_SomeEnemies, null);
			if (enemyRatio > 0.33f) return (CompanionshipBarracksDefOf.SleptInBarracks_ManyEnemies, null);

			return (ThoughtDefOf.SleptInBarracks, null); // 默认营房想法
		}
	}
}
