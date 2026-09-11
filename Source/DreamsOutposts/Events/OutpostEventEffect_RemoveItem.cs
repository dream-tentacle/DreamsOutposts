using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventEffect_RemoveItem : OutpostEventEffect
	{
		public ThingDef thingDef;

		public int count;

		public override void Apply(OutpostEventContext context)
		{
			OutpostStockUtility.TakeFromStock(context?.outpost, thingDef, count);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "-" + count + " " + (thingDef?.LabelCap ?? "unknown item");
		}
	}
}
