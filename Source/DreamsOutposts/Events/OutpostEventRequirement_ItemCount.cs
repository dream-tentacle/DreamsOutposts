using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventRequirement_ItemCount : OutpostEventRequirement
	{
		public ThingDef thingDef;

		public int count;

		public override AcceptanceReport Check(OutpostEventContext context)
		{
			int current = OutpostStockUtility.CountInStock(context?.outpost, thingDef);
			if (thingDef == null || count <= 0)
			{
				return "Invalid item count requirement.";
			}
			if (current < count)
			{
				return "需要 " + thingDef.LabelCap + " ×" + count + "\n当前：" + current;
			}
			return true;
		}
	}
}
