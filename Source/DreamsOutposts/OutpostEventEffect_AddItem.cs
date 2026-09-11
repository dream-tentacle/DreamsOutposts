using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_AddItem : OutpostEventEffect
	{
		public ThingDef thingDef;

		public int count;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || thingDef == null || count <= 0)
			{
				return;
			}
			OutpostStockUtility.AddToStock(context.outpost, thingDef, count);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "+" + count + " " + (thingDef?.LabelCap ?? "unknown item");
		}
	}
}
