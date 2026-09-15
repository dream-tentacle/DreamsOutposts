using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>精密加工车间的一种可选产物：加工什么，以及每点效率每天加工出多少。</summary>
	public class MechMachiningProductOption
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
	/// 某个机械族 PawnKind 的专属效率。
	/// 命中时它「替换」默认的每机械族效率，而不是叠加：一台禁卫蜈蚣只贡献 2 点效率，不是 0.2 + 2。
	/// </summary>
	public class MechMachiningPawnKindBonus
	{
		public PawnKindDef pawnKind;

		public float efficiency = 0.2f;

		public IEnumerable<string> ConfigErrors(string owner)
		{
			if (pawnKind == null)
			{
				yield return owner + " has no <pawnKind>; declare the PawnKindDef this bonus applies to.";
			}
			if (float.IsNaN(efficiency) || float.IsInfinity(efficiency) || efficiency < 0f)
			{
				yield return owner + " needs a finite, non-negative efficiency.";
			}
		}
	}

	/// <summary>
	/// 某个超重等级（MechWeightClassDef）的机械族效率。
	/// 和 PawnKind 专属效率一样是「替换」语义：超重型机械体 +0.5，而不是 0.2 + 0.5。
	/// 具名的 pawnKindBonuses 优先级更高，所以同属超重级的禁卫蜈蚣仍按它自己的 +2 取值。
	/// </summary>
	public class MechMachiningWeightClassBonus
	{
		public MechWeightClassDef weightClass;

		public float efficiency = 0.5f;

		public IEnumerable<string> ConfigErrors(string owner)
		{
			if (weightClass == null)
			{
				yield return owner + " has no <weightClass>; declare the MechWeightClassDef this bonus applies to.";
			}
			if (float.IsNaN(efficiency) || float.IsInfinity(efficiency) || efficiency < 0f)
			{
				yield return owner + " needs a finite, non-negative efficiency.";
			}
		}
	}

	/// <summary>
	/// 精密加工车间的生产规则。
	/// 效率 = baseEfficiency + Σ（据点内每台玩家机械族按其 PawnKindDef 取值：命中 pawnKindBonuses 用专属效率，否则命中 weightClassBonuses 用该重量档的效率，都没有才用 efficiencyPerMechanoid）。
	/// 产物在若干选项之间切换、每个选项自带基准产量，所以这里不写 capacityStat。
	/// </summary>
	public class OutpostProductionProperties_MechMachining : OutpostProductionProperties
	{
		public float baseEfficiency = 0.5f;

		public float efficiencyPerMechanoid = 0.2f;

		public List<MechMachiningPawnKindBonus> pawnKindBonuses = new List<MechMachiningPawnKindBonus>();

		public List<MechMachiningWeightClassBonus> weightClassBonuses = new List<MechMachiningWeightClassBonus>();

		public List<MechMachiningProductOption> products = new List<MechMachiningProductOption>();

		public OutpostProductionProperties_MechMachining()
		{
			workerClass = typeof(OutpostProductionWorker_MechMachining);
		}

		public MechMachiningProductOption OptionFor(ThingDef productDef)
		{
			if (productDef == null || products == null)
			{
				return null;
			}
			for (int i = 0; i < products.Count; i++)
			{
				MechMachiningProductOption option = products[i];
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

		/// <summary>这台机械族用专属效率吗。返回 true 时用 out 的专属值替换默认的每机械族效率。</summary>
		public bool TryGetPawnKindBonus(PawnKindDef kindDef, out float efficiency)
		{
			efficiency = 0f;
			if (kindDef == null || pawnKindBonuses == null)
			{
				return false;
			}
			for (int i = 0; i < pawnKindBonuses.Count; i++)
			{
				MechMachiningPawnKindBonus bonus = pawnKindBonuses[i];
				if (bonus != null && bonus.pawnKind == kindDef)
				{
					efficiency = bonus.efficiency;
					return true;
				}
			}
			return false;
		}

		/// <summary>这台机械族的超重档位有专属效率吗。优先级低于 TryGetPawnKindBonus。</summary>
		public bool TryGetWeightClassBonus(MechWeightClassDef weightClass, out float efficiency)
		{
			efficiency = 0f;
			if (weightClass == null || weightClassBonuses == null)
			{
				return false;
			}
			for (int i = 0; i < weightClassBonuses.Count; i++)
			{
				MechMachiningWeightClassBonus bonus = weightClassBonuses[i];
				if (bonus != null && bonus.weightClass == weightClass)
				{
					efficiency = bonus.efficiency;
					return true;
				}
			}
			return false;
		}
	}
}
