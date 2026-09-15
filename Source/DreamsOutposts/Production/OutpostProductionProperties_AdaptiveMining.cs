using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties_AdaptiveMining : OutpostProductionProperties
	{
		/// <summary>每天每点产能产出的等值白银。实际产量 = 产能 × 该值 ÷ 选中矿物的基准市场价。</summary>
		public float dailyMarketValue = 100f;

		/// <summary>
		/// 还没选矿物、或者选中的矿物已不再可采时回落到哪种矿物。
		/// 留空则用候选列表的第一项（按标签排序），所以核心设施最好显式指定。
		/// </summary>
		public ThingDef defaultProduct;

		public OutpostProductionProperties_AdaptiveMining()
		{
			workerClass = typeof(OutpostProductionWorker_AdaptiveMining);
		}

		/// <summary>没有已选矿物时的回落目标：defaultProduct 仍可采就用它，否则用候选列表的第一项。</summary>
		public ThingDef DefaultProduct
		{
			get
			{
				if (IsMineableProduct(defaultProduct))
				{
					return defaultProduct;
				}
				List<ThingDef> candidates = MineableProducts();
				return candidates.Count > 0 ? candidates[0] : null;
			}
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
