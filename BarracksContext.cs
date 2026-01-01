using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Companionship_Barracks
{
	/// <summary>
	/// 封装营房环境信息的上下文
	/// </summary>
	public class BarracksContext
	{
		public Pawn Actor { get; }
		public Room Room { get; }
		public List<Pawn> Cohabitants { get; }
		public int NumCohabitants => Cohabitants.Count;

		public int NumGoodFriends { get; set; }
		public int NumFriends { get; set; }
		public int NumEnemies { get; set; }
		public int NumSiblings { get; set; }
		public int NumParents { get; set; }
		public int NumChildren { get; set; }
		public int NumRelatives { get; set; }
		public int NumFamily { get; private set; }

		public BarracksContext(Pawn actor, Room room)
		{
			Actor = actor;
			Room = room;
			Cohabitants = room.ContainedBeds
				.Where(bed => bed.def.building.bed_humanlike)
				.SelectMany(bed => bed.OwnersForReading)
				.Where(pawn => !pawn.Equals(actor))
				.ToList();
			AnalyzeCohabitants();
		}

		private void AnalyzeCohabitants()
		{
			if (NumCohabitants == 0) return;

			foreach (var cohabitant in Cohabitants)
			{
				var relations = Actor.GetRelations(cohabitant).ToList();
				bool isFamily = relations.Any(r => r.familyByBloodRelation);
				int opinion = Actor.relations.OpinionOf(cohabitant);

				if (isFamily && opinion >= 20)
				{
					bool counted = false;
					foreach (var rel in relations)
					{
						if (rel.defName == PawnRelationDefOf.Sibling.defName) 
						{ 
							NumSiblings++;
							counted = true;
							break; 
						}
						if (rel.defName == PawnRelationDefOf.Parent.defName) 
						{ 
							NumParents++; 
							counted = true; 
							break; 
						}
						if (rel.defName == PawnRelationDefOf.Child.defName) 
						{ 
							NumChildren++;
							counted = true;
							break; 
						}
					}
					if (counted)
					{
						NumRelatives++;
					}
				}
				else
				{
					if (opinion >= 60) NumGoodFriends++;
					else if (opinion >= 20) NumFriends++;
					else if (opinion <= -20) NumEnemies++;
				}
			}
			NumFamily = NumSiblings + NumParents + NumChildren + NumRelatives;
		}
	}
}
