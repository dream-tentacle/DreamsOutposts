using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 聚灵法坛的生产工人。
	/// 效率 = 0.5（基础）+ Σ（每名驻守的、带灵芽的殖民者 +1，且其修仙境界每高于「蓬絮」一级再 +1）。
	/// 没有灵芽的普通殖民者不贡献效率；边缘仙路拿不到境界数据时所有人都按凡人算，效率就只剩基础值。
	/// 产量 = 效率 × 当前所选资源的基准产量（灵石 5，柔玉 1.2）。
	/// </summary>
	public class OutpostProductionWorker_XianluQi : OutpostProductionWorker
	{
		public const float BaseEfficiency = 0.5f;

		public const float EfficiencyPerCultivator = 1f;

		public const float EfficiencyPerRealmLevel = 1f;

		public override bool UsesDynamicProduct => true;

		public override Type StateClass => typeof(OutpostProductionState_XianluQi);

		/// <summary>产量随修仙效率变化，而效率没有对应的 StatDef，所以由 worker 自己声明。</summary>
		public override bool UsesPersonnelCapacity(OutpostProductionProperties production)
		{
			return true;
		}

		public override float CalculatePersonnelCapacity(IEnumerable<Pawn> pawns, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production)
		{
			float capacity = BaseEfficiency;
			if (pawns == null)
			{
				return capacity;
			}
			foreach (Pawn pawn in pawns)
			{
				if (pawn == null || !pawn.IsColonist)
				{
					continue;
				}
				int realmLevel;
				if (!XianluCultivationUtility.TryGetRealmLevel(pawn, out realmLevel))
				{
					continue;
				}
				capacity += EfficiencyPerCultivator + EfficiencyPerRealmLevel * Mathf.Max(realmLevel, 0);
			}
			return capacity;
		}

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			XianluQiProductOption option = OptionFor(production, QiState(state)?.selectedProduct);
			if (option?.product == null)
			{
				return 0f;
			}
			return personnelCapacity * option.outputPerCapacity;
		}

		public override ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return QiState(state)?.selectedProduct;
		}

		public override bool HasConfiguration(OutpostProductionProperties production)
		{
			return production is OutpostProductionProperties_XianluQi;
		}

		public override void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionState_XianluQi qiState = QiState(state);
			OutpostProductionProperties_XianluQi qiProps = production as OutpostProductionProperties_XianluQi;
			if (qiState == null || qiProps == null || qiProps.IsValidProduct(qiState.selectedProduct))
			{
				return;
			}
			ThingDef fallback = qiProps.DefaultProduct;
			if (fallback == null)
			{
				return;
			}
			qiState.selectedProduct = fallback;
			ResetProductionTimer(production, qiState);
		}

		public override void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
			string text = (QiState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.XianluQi.NoProduct".Translate()) + ": " + label;
			TooltipHandler.TipRegion(rect, new TipSignal("DreamsOutposts.XianluQi.ChooseProductTip".Translate(), rect.GetHashCode()));
			if (Widgets.ButtonText(rect, text))
			{
				OpenConfiguration(production, state);
			}
		}

		public override void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
			OutpostProductionProperties_XianluQi qiProps = production as OutpostProductionProperties_XianluQi;
			OutpostProductionState_XianluQi qiState = QiState(state);
			if (qiProps == null || qiState == null)
			{
				return;
			}
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			List<XianluQiProductOption> available = qiProps.products;
			for (int i = 0; i < (available?.Count ?? 0); i++)
			{
				XianluQiProductOption option = available[i];
				if (option?.product == null)
				{
					continue;
				}
				ThingDef productDef = option.product;
				string suffix = ((productDef == qiState.selectedProduct) ? "DreamsOutposts.XianluQi.CurrentSuffix".Translate().ToString() : string.Empty);
				XianluQiProductOption captured = option;
				options.Add(new FloatMenuOption(productDef.LabelCap + suffix, delegate
				{
					TrySetProduct(qiProps, qiState, captured);
					onChanged?.Invoke();
				}, MenuOptionPriority.Default, delegate(Rect optionRect)
				{
					TooltipHandler.TipRegion(optionRect, new TipSignal(ProductTooltip(captured), optionRect.GetHashCode()));
				}));
			}
			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption("DreamsOutposts.XianluQi.NoProductAvailable".Translate(), null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			if (!(production is OutpostProductionProperties_XianluQi))
			{
				return null;
			}
			return "DreamsOutposts.XianluQi.ProductSummary".Translate(QiState(state)?.selectedProduct?.LabelCap ?? "DreamsOutposts.Nothing".Translate());
		}

		public override string ConfigurationTip(OutpostProductionProperties production)
		{
			return "DreamsOutposts.XianluQi.ChooseProductTip".Translate();
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
			{
				yield return error;
			}
			if (!(production is OutpostProductionProperties_XianluQi qiProps))
			{
				yield return "The XianluQi worker requires OutpostProductionProperties_XianluQi; declare the rule with Class=\"DreamsOutposts.OutpostProductionProperties_XianluQi\".";
				yield break;
			}
			if (qiProps.products.NullOrEmpty())
			{
				yield return "products is empty, so this production can never produce anything; declare at least one <li> with a <product>.";
				yield break;
			}
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < qiProps.products.Count; i++)
			{
				XianluQiProductOption option = qiProps.products[i];
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

		private static OutpostProductionState_XianluQi QiState(OutpostProductionState state)
		{
			return state as OutpostProductionState_XianluQi;
		}

		private static XianluQiProductOption OptionFor(OutpostProductionProperties production, ThingDef productDef)
		{
			return (production as OutpostProductionProperties_XianluQi)?.OptionFor(productDef);
		}

		private static void TrySetProduct(OutpostProductionProperties_XianluQi production, OutpostProductionState_XianluQi state, XianluQiProductOption option)
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

		private static void ResetProductionTimer(OutpostProductionProperties production, OutpostProductionState_XianluQi state)
		{
			state.nextProductionTick = Find.TickManager.TicksGame + production.Worker.GetProductionIntervalTicks(production, state);
		}

		private static string ProductTooltip(XianluQiProductOption option)
		{
			return "DreamsOutposts.XianluQi.ProductOptionTooltip".Translate(option.product.LabelCap, option.outputPerCapacity.ToString("0.##"));
		}
	}
}
