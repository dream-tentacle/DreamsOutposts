using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties_AdaptiveMining : OutpostProductionProperties
	{
		public float dailyMarketValue = 100f;

		public OutpostProductionProperties_AdaptiveMining()
		{
			workerClass = typeof(OutpostProductionWorker_AdaptiveMining);
		}

		public bool IsMineableProduct(ThingDef productDef)
		{
			if (productDef == null || productDef.category != ThingCategory.Item || productDef.BaseMarketValue <= 0f)
			{
				return false;
			}
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef mineable = allDefs[i];
				if (mineable?.building != null && mineable.building.isResourceRock && mineable.building.mineableThing == productDef)
				{
					return true;
				}
			}
			return false;
		}

		public List<ThingDef> MineableProducts()
		{
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			List<ThingDef> result = new List<ThingDef>();
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef mineable = allDefs[i];
				ThingDef productDef = mineable?.building?.mineableThing;
				if (mineable?.building != null && mineable.building.isResourceRock && productDef != null &&
					productDef.category == ThingCategory.Item && productDef.BaseMarketValue > 0f && seen.Add(productDef))
				{
					result.Add(productDef);
				}
			}
			result.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
			return result;
		}
	}
}
