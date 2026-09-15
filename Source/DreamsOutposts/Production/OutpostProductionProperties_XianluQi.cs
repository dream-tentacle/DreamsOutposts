using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>聚灵法坛的一种可选产物：产出什么，以及每点效率每天产出多少。</summary>
	public class XianluQiProductOption
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
	/// 聚灵法坛的生产规则：产物在若干选项之间切换，每个选项自带基准产量。
	/// 效率由 OutpostProductionWorker_XianluQi 按修仙境界计算，所以这里不写 capacityStat。
	/// </summary>
	public class OutpostProductionProperties_XianluQi : OutpostProductionProperties
	{
		public List<XianluQiProductOption> products = new List<XianluQiProductOption>();

		public OutpostProductionProperties_XianluQi()
		{
			workerClass = typeof(OutpostProductionWorker_XianluQi);
		}

		public XianluQiProductOption OptionFor(ThingDef productDef)
		{
			if (productDef == null || products == null)
			{
				return null;
			}
			for (int i = 0; i < products.Count; i++)
			{
				XianluQiProductOption option = products[i];
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
