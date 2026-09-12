using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
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
			return adaptive.dailyMarketValue / mineral.BaseMarketValue;
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
			if (miningState != null && production is OutpostProductionProperties_AdaptiveMining adaptive && !adaptive.IsMineableProduct(miningState.selectedMineral))
			{
				List<ThingDef> candidates = adaptive.MineableProducts();
				if (candidates.Count > 0)
				{
					miningState.selectedMineral = candidates[0];
				}
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
					OutpostProductionState_AdaptiveMining miningState = MiningState(state);
					if (miningState != null && adaptive.IsMineableProduct(mineral))
					{
						miningState.selectedMineral = mineral;
						onChanged?.Invoke();
					}
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
		}
	}
}
