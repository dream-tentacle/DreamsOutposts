using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>日光冶炼场的一种可选产物：产出什么，以及每点效率每天产出多少。</summary>
	public class MiliraSolarProductOption
	{
		public ThingDef product;

		public float outputPerCapacity = 1f;

		public IEnumerable<string> ConfigErrors(string owner)
		{
			if (product == null)
			{
				yield return owner + " has no <product>; declare the ThingDef this option should produce.";
			}
			if (float.IsNaN(outputPerCapacity) || float.IsInfinity(outputPerCapacity) || outputPerCapacity <= 0f)
			{
				yield return owner + " needs a finite positive outputPerCapacity.";
			}
		}
	}

	/// <summary>
	/// 日光冶炼场的生产规则。
	/// 效率 = baseEfficiency + Σ（据点内每名米莉拉殖民者 efficiencyPerMilira + 每台米莉安 efficiencyPerMilian）。
	/// 产物在若干选项之间切换、每个选项自带基准产量，所以这里不写 capacityStat。
	/// </summary>
	public class OutpostProductionProperties_MiliraSolar : OutpostProductionProperties
	{
		public float baseEfficiency = 0.5f;

		public float efficiencyPerMilira = 1f;

		public float efficiencyPerMilian = 1f;

		public List<MiliraSolarProductOption> products = new List<MiliraSolarProductOption>();

		public OutpostProductionProperties_MiliraSolar()
		{
			workerClass = typeof(OutpostProductionWorker_MiliraSolar);
		}

		public MiliraSolarProductOption OptionFor(ThingDef productDef)
		{
			if (productDef == null || products == null)
			{
				return null;
			}
			for (int i = 0; i < products.Count; i++)
			{
				MiliraSolarProductOption option = products[i];
				if (option != null && option.product == productDef)
				{
					return option;
				}
			}
			return null;
		}

		public bool IsValidProduct(ThingDef productDef)
		{
			return OptionFor(productDef) != null;
		}

		/// <summary>没有任何已选产物时回落到哪一个：列表里第一个有产物的选项。</summary>
		public ThingDef DefaultProduct
		{
			get
			{
				for (int i = 0; i < (products?.Count ?? 0); i++)
				{
					if (products[i]?.product != null)
					{
						return products[i].product;
					}
				}
				return null;
			}
		}
	}
}
