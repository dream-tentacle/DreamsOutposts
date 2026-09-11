using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventRequirement_StatValue : OutpostEventRequirement
	{
		public StatDef stat;

		public float minValue;

		public override AcceptanceReport Check(OutpostEventContext context)
		{
			if (stat == null)
			{
				return "Invalid stat requirement.";
			}
			float total = 0f;
			IEnumerable<Pawn> pawns = context?.outpost?.Pawns;
			if (pawns != null)
			{
				foreach (Pawn pawn in pawns)
				{
					if (OutpostStatUtility.IsStatShownFor(stat, pawn))
					{
						total += pawn.GetStatValue(stat);
					}
				}
			}
			if (total <= minValue)
			{
				return "需要总 " + stat.LabelCap + " > " + minValue.ToString("0.##") + "\n当前：" + total.ToString("0.##");
			}
			return true;
		}
	}
}
