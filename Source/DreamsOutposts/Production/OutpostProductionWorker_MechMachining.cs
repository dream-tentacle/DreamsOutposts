using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 精密加工车间的生产工人。
	/// 效率 = 0.5（基础）+ Σ（据点内每台玩家机械族的加工能力贡献，按优先级取第一条命中的规则：
	/// pawnKindBonuses 的具名效率 → weightClassBonuses 的档位效率（超重型 +0.5）→ efficiencyPerMechanoid 默认值 0.2）。
	/// 具体是：禁卫蜈蚣 +2，炼狱魔王/女王/毒蜂/黑衣憎恶毒蜂 +1.5，其余超重型机械体 +0.5，普通机械族 +0.2。
	/// 所有规则都是「替换」而不是叠加，所以一台禁卫蜈蚣只贡献 2 点效率；不属于玩家阵营的机械族（例如关押的敌方机械族）完全不贡献。
	/// 机械族在这里提供的是加工能力，本身不会被消耗。
	/// 产量 = 效率 × 当前所选产物的基准产量（精密装甲板 8，零部件 9）。
	/// </summary>
	public class OutpostProductionWorker_MechMachining : OutpostProductionWorker
	{
		public const float DefaultBaseEfficiency = 0.5f;

		public const float DefaultEfficiencyPerMechanoid = 0.2f;

		public override bool UsesDynamicProduct => true;

		public override Type StateClass => typeof(OutpostProductionState_MechMachining);

		/// <summary>产量随机械族编成变化，而效率没有对应的 StatDef，所以由 worker 自己声明。</summary>
		public override bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return true;
		}

		public override float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			OutpostProductionProperties_MechMachining props = production as OutpostProductionProperties_MechMachining;
			float capacity = props?.baseEfficiency ?? DefaultBaseEfficiency;
			if (pawns == null)
			{
				return capacity;
			}
			float perMechanoid = props?.efficiencyPerMechanoid ?? DefaultEfficiencyPerMechanoid;
			foreach (Pawn pawn in pawns)
			{
				if (!IsCountedMechanoid(pawn))
				{
					continue;
				}
				float bonus;
				if (props != null && props.TryGetPawnKindBonus(pawn.kindDef, out bonus))
				{
					capacity += bonus;
				}
				else if (props != null && props.TryGetWeightClassBonus(pawn.RaceProps.mechWeightClass, out bonus))
				{
					capacity += bonus;
				}
				else
				{
					capacity += perMechanoid;
				}
			}
			return capacity;
		}

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			MechMachiningProductOption option = OptionFor(production, MachiningState(state)?.selectedProduct);
			if (option?.product == null)
			{
				return 0f;
			}
			return personnelCapacity * option.outputPerCapacity;
		}

		public override ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return MachiningState(state)?.selectedProduct;
		}

		public override bool HasConfiguration(OutpostProductionProperties production)
		{
			return production is OutpostProductionProperties_MechMachining;
		}

		public override void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionState_MechMachining machiningState = MachiningState(state);
			OutpostProductionProperties_MechMachining machiningProps = production as OutpostProductionProperties_MechMachining;
			if (machiningState == null || machiningProps == null || machiningProps.IsValidProduct(machiningState.selectedProduct))
			{
				return;
			}
			ThingDef fallback = machiningProps.DefaultProduct;
			if (fallback == null)
			{
				return;
			}
			machiningState.selectedProduct = fallback;
			ResetProductionTimer(production, machiningState);
		}

		public override void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
			string text = (MachiningState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.MechMachining.NoProduct".Translate()) + ": " + label;
			TooltipHandler.TipRegion(rect, new TipSignal("DreamsOutposts.MechMachining.ChooseProductTip".Translate(), rect.GetHashCode()));
			if (Widgets.ButtonText(rect, text))
			{
				OpenConfiguration(production, state);
			}
		}

		public override void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
			OutpostProductionProperties_MechMachining machiningProps = production as OutpostProductionProperties_MechMachining;
			OutpostProductionState_MechMachining machiningState = MachiningState(state);
			if (machiningProps == null || machiningState == null)
			{
				return;
			}
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			List<MechMachiningProductOption> available = machiningProps.products;
			for (int i = 0; i < (available?.Count ?? 0); i++)
			{
				MechMachiningProductOption option = available[i];
				if (option?.product == null)
				{
					continue;
				}
				ThingDef productDef = option.product;
				string suffix = ((productDef == machiningState.selectedProduct) ? "DreamsOutposts.MechMachining.CurrentSuffix".Translate().ToString() : string.Empty);
				MechMachiningProductOption captured = option;
				options.Add(new FloatMenuOption(productDef.LabelCap + suffix, delegate
				{
					TrySetProduct(machiningProps, machiningState, captured);
					onChanged?.Invoke();
				}, MenuOptionPriority.Default, delegate(Rect optionRect)
				{
					TooltipHandler.TipRegion(optionRect, new TipSignal(ProductTooltip(captured), optionRect.GetHashCode()));
				}));
			}
			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption("DreamsOutposts.MechMachining.NoProductAvailable".Translate(), null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			if (!(production is OutpostProductionProperties_MechMachining))
			{
				return null;
			}
			return "DreamsOutposts.MechMachining.ProductSummary".Translate(MachiningState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.Nothing".Translate());
		}

		public override string ConfigurationTip(OutpostProductionProperties production)
		{
			return "DreamsOutposts.MechMachining.ChooseProductTip".Translate();
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
			{
				yield return error;
			}
			if (!(production is OutpostProductionProperties_MechMachining machiningProps))
			{
				yield return "The MechMachining worker requires OutpostProductionProperties_MechMachining; declare the rule with Class=\"DreamsOutposts.OutpostProductionProperties_MechMachining\".";
				yield break;
			}
			if (float.IsNaN(machiningProps.baseEfficiency) || float.IsInfinity(machiningProps.baseEfficiency) || machiningProps.baseEfficiency < 0f)
			{
				yield return "baseEfficiency must be a finite, non-negative number.";
			}
			if (float.IsNaN(machiningProps.efficiencyPerMechanoid) || float.IsInfinity(machiningProps.efficiencyPerMechanoid) || machiningProps.efficiencyPerMechanoid < 0f)
			{
				yield return "efficiencyPerMechanoid must be a finite, non-negative number.";
			}
			HashSet<PawnKindDef> seenKinds = new HashSet<PawnKindDef>();
			for (int i = 0; i < (machiningProps.pawnKindBonuses?.Count ?? 0); i++)
			{
				MechMachiningPawnKindBonus bonus = machiningProps.pawnKindBonuses[i];
				string owner = "pawnKindBonuses[" + i + "]";
				if (bonus == null)
				{
					yield return owner + " is null.";
					continue;
				}
				foreach (string error in bonus.ConfigErrors(owner))
				{
					yield return error;
				}
				if (bonus.pawnKind != null && !seenKinds.Add(bonus.pawnKind))
				{
					yield return "Duplicate pawnKind " + bonus.pawnKind.defName + " in pawnKindBonuses.";
				}
			}
			HashSet<MechWeightClassDef> seenClasses = new HashSet<MechWeightClassDef>();
			for (int k = 0; k < (machiningProps.weightClassBonuses?.Count ?? 0); k++)
			{
				MechMachiningWeightClassBonus bonus = machiningProps.weightClassBonuses[k];
				string owner = "weightClassBonuses[" + k + "]";
				if (bonus == null)
				{
					yield return owner + " is null.";
					continue;
				}
				foreach (string error in bonus.ConfigErrors(owner))
				{
					yield return error;
				}
				if (bonus.weightClass != null && !seenClasses.Add(bonus.weightClass))
				{
					yield return "Duplicate weightClass " + bonus.weightClass.defName + " in weightClassBonuses.";
				}
			}
			if (machiningProps.products.NullOrEmpty())
			{
				yield return "products is empty, so this production can never produce anything; declare at least one <li> with a <product>.";
				yield break;
			}
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int j = 0; j < machiningProps.products.Count; j++)
			{
				MechMachiningProductOption option = machiningProps.products[j];
				if (option == null)
				{
					yield return "products[" + j + "] is null.";
					continue;
				}
				foreach (string error in option.ConfigErrors("products[" + j + "]"))
				{
					yield return error;
				}
				if (option.product != null && !seen.Add(option.product))
				{
					yield return "Duplicate product " + option.product.defName + " in products.";
				}
			}
		}

		/// <summary>只有据点内、属于玩家阵营、还未死亡的机械族才贡献加工效率。</summary>
		private static bool IsCountedMechanoid(Pawn pawn)
		{
			return pawn != null
				&& !pawn.Dead
				&& pawn.RaceProps != null
				&& pawn.RaceProps.IsMechanoid
				&& pawn.Faction == Faction.OfPlayer;
		}

		private static OutpostProductionState_MechMachining MachiningState(OutpostProductionState state)
		{
			return state as OutpostProductionState_MechMachining;
		}

		private static MechMachiningProductOption OptionFor(OutpostProductionProperties production, ThingDef productDef)
		{
			return (production as OutpostProductionProperties_MechMachining)?.OptionFor(productDef);
		}

		private static void TrySetProduct(OutpostProductionProperties_MechMachining production, OutpostProductionState_MechMachining state, MechMachiningProductOption option)
		{
			if (option?.product == null || state == null)
			{
				return;
			}
			if (state.selectedProduct == option.product)
			{
				return;
			}
			state.selectedProduct = option.product;
			ResetProductionTimer(production, state);
		}

		private static void ResetProductionTimer(OutpostProductionProperties production, OutpostProductionState_MechMachining state)
		{
			state.nextProductionTick = Find.TickManager.TicksGame + production.Worker.GetProductionIntervalTicks(production, state);
		}

		private static string ProductTooltip(MechMachiningProductOption option)
		{
			return "DreamsOutposts.MechMachining.ProductOptionTooltip".Translate(option.product.LabelCap, option.outputPerCapacity.ToString("0.##"));
		}
	}
}
