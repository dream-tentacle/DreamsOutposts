using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 自适应采矿：产物由玩家在若干可采矿物之间自选，产量按白银等值折算。
	/// 产量 = 产能 × dailyMarketValue ÷ 选中矿物的基准市场价，所以不管选哪种矿物，
	/// 每天的产出价值都恒等于 dailyMarketValue × 产能，改选矿物只改变数量、不改变价值。
	/// 没写 capacityStat 的规则（例如自适应采矿机）产能恒为 1，于是每天固定 dailyMarketValue 白银等值。
	/// </summary>
	public class OutpostProductionWorker_AdaptiveMining : OutpostProductionWorker
	{
		public override bool UsesDynamicProduct => true;

		public override Type StateClass => typeof(OutpostProductionState_AdaptiveMining);

		private static OutpostProductionState_AdaptiveMining MiningState(OutpostProductionState state)
		{
			return state as OutpostProductionState_AdaptiveMining;
		}

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			ThingDef mineral = MiningState(state)?.selectedMineral;
			if (!(production is OutpostProductionProperties_AdaptiveMining adaptive) || mineral == null || mineral.BaseMarketValue <= 0f)
			{
				return 0f;
			}
			return personnelCapacity * adaptive.dailyMarketValue / mineral.BaseMarketValue;
		}

		public override ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return MiningState(state)?.selectedMineral;
		}

		public override bool HasConfiguration(OutpostProductionProperties production)
		{
			return production is OutpostProductionProperties_AdaptiveMining;
		}

		public override void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionState_AdaptiveMining miningState = MiningState(state);
			if (miningState == null || !(production is OutpostProductionProperties_AdaptiveMining adaptive) || adaptive.IsMineableProduct(miningState.selectedMineral))
			{
				return;
			}
			ThingDef fallback = adaptive.DefaultProduct;
			if (fallback != null)
			{
				TrySetMineral(production, state, fallback);
			}
		}

		public override void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
			string text = (MiningState(state)?.selectedMineral?.LabelCap ?? "DreamsOutposts.NoMineral".Translate()) + ": " + label;
			TooltipHandler.TipRegion(rect, new TipSignal("DreamsOutposts.ChooseMineralTip".Translate(), rect.GetHashCode()));
			if (Widgets.ButtonText(rect, text))
			{
				OpenConfiguration(production, state);
			}
		}

		public override void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
			if (!(production is OutpostProductionProperties_AdaptiveMining adaptive))
			{
				return;
			}
			List<ThingDef> candidates = adaptive.MineableProducts();
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			ThingDef current = MiningState(state)?.selectedMineral;
			for (int i = 0; i < candidates.Count; i++)
			{
				ThingDef mineral = candidates[i];
				string suffix = mineral == current ? "DreamsOutposts.CurrentMineralSuffix".Translate().ToString() : string.Empty;
				options.Add(new FloatMenuOption(mineral.LabelCap + suffix, delegate
				{
					TrySetMineral(adaptive, state, mineral);
					onChanged?.Invoke();
				}));
			}
			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption("DreamsOutposts.NoMineralAvailable".Translate(), null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			return "DreamsOutposts.MineralSummary".Translate(MiningState(state)?.selectedMineral?.LabelCap ?? "DreamsOutposts.None".Translate());
		}

		public override string ConfigurationTip(OutpostProductionProperties production)
		{
			return "DreamsOutposts.ChooseMineralTip".Translate();
		}

		/// <summary>
		/// 改选矿物时重开当天的开采周期，和农场换作物一样，不把已经在旧矿物上累积的进度带到新矿物上。
		/// 选中的还是原来那种矿物时什么都不做，免得反复点「（当前）」那个选项白等一天。
		/// </summary>
		private static void TrySetMineral(OutpostProductionProperties production, OutpostProductionState state, ThingDef mineral)
		{
			OutpostProductionState_AdaptiveMining miningState = MiningState(state);
			if (miningState == null)
			{
				Log.Error("[DreamsOutposts] Tried to set mineral " + (mineral?.defName ?? "null") + " on production " + (production?.id ?? "null") + ", but its runtime state is " + (state?.GetType().Name ?? "null") + " instead of OutpostProductionState_AdaptiveMining.");
			}
			else if (!(production is OutpostProductionProperties_AdaptiveMining adaptive) || !adaptive.IsMineableProduct(mineral))
			{
				Log.Error("[DreamsOutposts] Tried to set mineral " + (mineral?.defName ?? "null") + " on production " + (production?.id ?? "null") + ", but it is not a mineable mineral.");
			}
			else if (miningState.selectedMineral != mineral)
			{
				miningState.selectedMineral = mineral;
				ResetProductionTimer(production, miningState);
			}
		}

		private static void ResetProductionTimer(OutpostProductionProperties production, OutpostProductionState_AdaptiveMining state)
		{
			state.nextProductionTick = Find.TickManager.TicksGame + production.Worker.GetProductionIntervalTicks(production, state);
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string error in base.ConfigErrors(production))
			{
				yield return error;
			}
			if (!(production is OutpostProductionProperties_AdaptiveMining adaptive))
			{
				yield return "Adaptive mining worker requires OutpostProductionProperties_AdaptiveMining.";
			}
			else if (adaptive.dailyMarketValue <= 0f || float.IsNaN(adaptive.dailyMarketValue) || float.IsInfinity(adaptive.dailyMarketValue))
			{
				yield return "dailyMarketValue must be a finite positive number.";
			}
			else if (adaptive.defaultProduct != null && !adaptive.IsMineableProduct(adaptive.defaultProduct))
			{
				yield return "defaultProduct " + adaptive.defaultProduct.defName + " is not a mineable mineral, so it will be ignored and the first candidate will be used instead.";
			}
		}
	}
}
