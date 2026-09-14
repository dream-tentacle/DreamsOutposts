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
			OutpostItemRewardUtility.Add(context, thingDef, count);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "+" + count + " " + (thingDef?.LabelCap ?? "unknown item");
		}
	}

	public class OutpostEventEffect_AddRandomItemCount : OutpostEventEffect
	{
		public ThingDef thingDef;
		public IntRange countRange;

		public override void Apply(OutpostEventContext context)
		{
			if (context?.outpost == null || thingDef == null || countRange.TrueMax <= 0) return;
			OutpostItemRewardUtility.Add(context, thingDef, countRange.RandomInRange);
		}

		public override string GetPreview(OutpostEventContext context)
		{
			return "+" + countRange + " " + (thingDef?.LabelCap ?? "unknown item");
		}
	}
}
