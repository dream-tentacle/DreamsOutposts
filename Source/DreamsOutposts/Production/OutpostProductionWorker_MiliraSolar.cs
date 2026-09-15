using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 日光冶炼场的生产工人（米莉拉兼容）。
	/// 效率 = 0.5（基础）+ Σ（据点内每名米莉拉殖民者 +1）+ Σ（据点内每台米莉安机械体 +1）。
	/// 米莉拉按 ThingDef Milira_Race 识别；米莉安按身体类型 Milian_Body 且是机械族识别（和米莉拉自己的 MilianUtility.IsMilian 一致），
	/// 因此浮游单元（身体类型 Milira_FloatUnit）以及其它机械族都不贡献效率。
	/// 米莉安的判定和闪毁的 IsCountedMechanoid 一致：只要是玩家阵营、还活着的米莉安就算，
	/// 不检查机械师主控关系（原版 IsColonyMechPlayerControlled 要求 Spawned，据点里的 pawn 没有进入地图，用不了）。
	/// 这里的人和机械都只是提供产能，本身不会被消耗。
	/// 产量 = 效率 × 当前所选产物的基准产量（日盘钢 14，日凌晶 10）。
	/// </summary>
	public class OutpostProductionWorker_MiliraSolar : OutpostProductionWorker
	{
		public const float DefaultBaseEfficiency = 0.5f;

		public const float DefaultEfficiencyPerMilira = 1f;

		public const float DefaultEfficiencyPerMilian = 1f;

		/// <summary>米莉拉种族 ThingDef。</summary>
		private const string MiliraRaceDefName = "Milira_Race";

		/// <summary>米莉安身体类型 BodyDef。</summary>
		private const string MilianBodyDefName = "Milian_Body";

		public override bool UsesDynamicProduct => true;

		public override Type StateClass => typeof(OutpostProductionState_MiliraSolar);

		/// <summary>产量随驻守人员变化，而效率没有对应的 StatDef，所以由 worker 自己声明。</summary>
		public override bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return true;
		}

		public override float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			OutpostProductionProperties_MiliraSolar props = production as OutpostProductionProperties_MiliraSolar;
			float capacity = props?.baseEfficiency ?? DefaultBaseEfficiency;
			if (pawns == null)
			{
				return capacity;
			}
			float perMilira = props?.efficiencyPerMilira ?? DefaultEfficiencyPerMilira;
			float perMilian = props?.efficiencyPerMilian ?? DefaultEfficiencyPerMilian;
			foreach (Pawn pawn in pawns)
			{
				if (IsMiliraColonist(pawn))
				{
					capacity += perMilira;
				}
				else if (IsCountedMilian(pawn))
				{
					capacity += perMilian;
				}
			}
			return capacity;
		}

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			MiliraSolarProductOption option = OptionFor(production, SolarState(state)?.selectedProduct);
			if (option?.product == null)
			{
				return 0f;
			}
			return personnelCapacity * option.outputPerCapacity;
		}

		public override ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return SolarState(state)?.selectedProduct;
		}

		public override bool HasConfiguration(OutpostProductionProperties production)
		{
			return production is OutpostProductionProperties_MiliraSolar;
		}

		public override void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionState_MiliraSolar solarState = SolarState(state);
			OutpostProductionProperties_MiliraSolar solarProps = production as OutpostProductionProperties_MiliraSolar;
			if (solarState == null || solarProps == null || solarProps.IsValidProduct(solarState.selectedProduct))
			{
				return;
			}
			ThingDef fallback = solarProps.DefaultProduct;
			if (fallback == null)
			{
				return;
			}
			solarState.selectedProduct = fallback;
			ResetProductionTimer(production, solarState);
		}

		public override void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
			string text = (SolarState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.MiliraSolar.NoProduct".Translate()) + ": " + label;
			TooltipHandler.TipRegion(rect, new TipSignal("DreamsOutposts.MiliraSolar.ChooseProductTip".Translate(), rect.GetHashCode()));
			if (Widgets.ButtonText(rect, text))
			{
				OpenConfiguration(production, state);
			}
		}

		public override void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
			OutpostProductionProperties_MiliraSolar solarProps = production as OutpostProductionProperties_MiliraSolar;
			OutpostProductionState_MiliraSolar solarState = SolarState(state);
			if (solarProps == null || solarState == null)
			{
				return;
			}
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			List<MiliraSolarProductOption> available = solarProps.products;
			for (int i = 0; i < (available?.Count ?? 0); i++)
			{
				MiliraSolarProductOption option = available[i];
				if (option?.product == null)
				{
					continue;
				}
				ThingDef productDef = option.product;
				string suffix = ((productDef == solarState.selectedProduct) ? "DreamsOutposts.MiliraSolar.CurrentSuffix".Translate().ToString() : string.Empty);
				MiliraSolarProductOption captured = option;
				options.Add(new FloatMenuOption(productDef.LabelCap + suffix, delegate
				{
					TrySetProduct(solarProps, solarState, captured);
					onChanged?.Invoke();
				}, MenuOptionPriority.Default, delegate(Rect optionRect)
				{
					TooltipHandler.TipRegion(optionRect, new TipSignal(ProductTooltip(captured), optionRect.GetHashCode()));
				}));
			}
			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption("DreamsOutposts.MiliraSolar.NoProductAvailable".Translate(), null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			if (!(production is OutpostProductionProperties_MiliraSolar))
			{
				return null;
			}
			return "DreamsOutposts.MiliraSolar.ProductSummary".Translate(SolarState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.Nothing".Translate());
		}

		public override string ConfigurationTip(OutpostProductionProperties production)
		{
			return "DreamsOutposts.MiliraSolar.ChooseProductTip".Translate();
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
			{
				yield return error;
			}
			if (!(production is OutpostProductionProperties_MiliraSolar solarProps))
			{
				yield return "The MiliraSolar worker requires OutpostProductionProperties_MiliraSolar; declare the rule with Class=\"DreamsOutposts.OutpostProductionProperties_MiliraSolar\".";
				yield break;
			}
			if (float.IsNaN(solarProps.baseEfficiency) || float.IsInfinity(solarProps.baseEfficiency) || solarProps.baseEfficiency < 0f)
			{
				yield return "baseEfficiency must be a finite, non-negative number.";
			}
			if (float.IsNaN(solarProps.efficiencyPerMilira) || float.IsInfinity(solarProps.efficiencyPerMilira) || solarProps.efficiencyPerMilira < 0f)
			{
				yield return "efficiencyPerMilira must be a finite, non-negative number.";
			}
			if (float.IsNaN(solarProps.efficiencyPerMilian) || float.IsInfinity(solarProps.efficiencyPerMilian) || solarProps.efficiencyPerMilian < 0f)
			{
				yield return "efficiencyPerMilian must be a finite, non-negative number.";
			}
			if (solarProps.products.NullOrEmpty())
			{
				yield return "products is empty, so this production can never produce anything; declare at least one <li> with a <product>.";
				yield break;
			}
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < solarProps.products.Count; i++)
			{
				MiliraSolarProductOption option = solarProps.products[i];
				if (option == null)
				{
					yield return "products[" + i + "] is null.";
					continue;
				}
				foreach (string error in option.ConfigErrors("products[" + i + "]"))
				{
					yield return error;
				}
				if (option.product != null && !seen.Add(option.product))
				{
					yield return "Duplicate product " + option.product.defName + " in products.";
				}
			}
		}

		/// <summary>据点内、属于玩家阵营、活着的米莉拉殖民者才贡献效率。</summary>
		private static bool IsMiliraColonist(Pawn pawn)
		{
			return pawn != null
				&& !pawn.Dead
				&& pawn.IsColonist
				&& pawn.def != null
				&& pawn.def.defName == MiliraRaceDefName;
		}

		/// <summary>据点内、属于玩家阵营、还活着的米莉安才贡献效率。</summary>
		private static bool IsCountedMilian(Pawn pawn)
		{
			return pawn != null
				&& !pawn.Dead
				&& pawn.RaceProps != null
				&& pawn.RaceProps.IsMechanoid
				&& pawn.RaceProps.body != null
				&& pawn.RaceProps.body.defName == MilianBodyDefName
				&& pawn.Faction == Faction.OfPlayer;
		}

		private static OutpostProductionState_MiliraSolar SolarState(OutpostProductionState state)
		{
			return state as OutpostProductionState_MiliraSolar;
		}

		private static MiliraSolarProductOption OptionFor(OutpostProductionProperties production, ThingDef productDef)
		{
			return (production as OutpostProductionProperties_MiliraSolar)?.OptionFor(productDef);
		}

		private static void TrySetProduct(OutpostProductionProperties_MiliraSolar production, OutpostProductionState_MiliraSolar state, MiliraSolarProductOption option)
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

		private static void ResetProductionTimer(OutpostProductionProperties production, OutpostProductionState_MiliraSolar state)
		{
			state.nextProductionTick = Find.TickManager.TicksGame + production.Worker.GetProductionIntervalTicks(production, state);
		}

		private static string ProductTooltip(MiliraSolarProductOption option)
		{
			return "DreamsOutposts.MiliraSolar.ProductOptionTooltip".Translate(option.product.LabelCap, option.outputPerCapacity.ToString("0.##"));
		}
	}
}
