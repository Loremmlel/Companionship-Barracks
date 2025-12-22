using RimWorld;
using Verse;

namespace Companionship_Barracks
{
	public abstract class ThoughtRule
	{
		public abstract bool CanApply(BarracksContext context);
		public abstract ThoughtDef GetThought(BarracksContext context);
	}

	public class SiblingSpecialRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context)
		{
			return context.NumCohabitants == 1 && context.NumSiblings == 1;
		}

		public override ThoughtDef GetThought(BarracksContext context)
		{
			var sibling = context.Cohabitants[0];
			int opinion = context.Actor.relations.OpinionOf(sibling);
			if (opinion < 80 || context.Actor.gender == sibling.gender || sibling.gender == Gender.None)
			{
				return null; // 不满足特殊条件，退回至其他规则
			}

			bool isOlder = context.Actor.ageTracker.AgeBiologicalYears > sibling.ageTracker.AgeBiologicalYears;
			switch (context.Actor.gender)
			{
				case Gender.Male:
					return isOlder ? CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithSister : CompanionshipBarracksDefOf.SleptInBarracks_BrotherWithOlderSister;
				// 女性
				case Gender.Female:
					return isOlder ? CompanionshipBarracksDefOf.SleptInBarracks_SisterWithYoungerBrother : CompanionshipBarracksDefOf.SleptInBarracks_SisterWithBrother;
				case Gender.None:
				default:
					return null;
			}
		}
	}

	public class FamilyOnlyRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context)
		{
			return context.NumFamily > 0 && context.NumFamily == context.NumCohabitants;
		}

		public override ThoughtDef GetThought(BarracksContext context)
		{
			if (context.NumSiblings == context.NumCohabitants) return CompanionshipBarracksDefOf.SleptInBarracks_OnlySiblings;
			if (context.NumParents == context.NumCohabitants) return CompanionshipBarracksDefOf.SleptInBarracks_OnlyParents;
			if (context.NumChildren == context.NumCohabitants) return CompanionshipBarracksDefOf.SleptInBarracks_OnlyChildren;
			return CompanionshipBarracksDefOf.SleptInBarracks_OnlyRelatives;
		}
	}

	public class MixedCompanyRule : ThoughtRule
	{
		public override bool CanApply(BarracksContext context) => true;

		public override ThoughtDef GetThought(BarracksContext context)
		{
			// 有亲戚也有非亲戚同住时，将亲戚计入好朋友
			int effectiveGoodFriends = context.NumGoodFriends + context.NumFamily;

			float goodFriendRatio = (float)effectiveGoodFriends / context.NumCohabitants;
			float friendRatio = (float)context.NumFriends / context.NumCohabitants;
			float enemyRatio = (float)context.NumEnemies / context.NumCohabitants;

			if (goodFriendRatio >= 0.5f && context.NumEnemies == 0) return CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_NoEnemies;
			if (friendRatio >= 0.5f && context.NumEnemies == 0) return CompanionshipBarracksDefOf.SleptInBarracks_Friends_NoEnemies;
			if (goodFriendRatio >= 0.5f && context.NumEnemies > 0) return CompanionshipBarracksDefOf.SleptInBarracks_GoodFriends_WithEnemies;
			if (friendRatio >= 0.5f && context.NumEnemies > 0) return CompanionshipBarracksDefOf.SleptInBarracks_Friends_WithEnemies;
			if (enemyRatio > 0 && enemyRatio <= 0.33f) return CompanionshipBarracksDefOf.SleptInBarracks_SomeEnemies;
			if (enemyRatio > 0.33f) return CompanionshipBarracksDefOf.SleptInBarracks_ManyEnemies;

			return ThoughtDefOf.SleptInBarracks; // 默认营房想法
		}
	}
}
