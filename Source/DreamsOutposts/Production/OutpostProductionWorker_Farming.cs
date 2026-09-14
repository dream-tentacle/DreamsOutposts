using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionWorker_Farming : OutpostProductionWorker
	{
		public override bool UsesDynamicProduct => true;

		public override Type StateClass => typeof(OutpostProductionState_Farming);

		public override float CalculateOutput(float personnelCapacity, OutpostTypeDef outpostTypeDef, OutpostProductionProperties production, OutpostProductionState state)
		{
			if (production == null)
			{
				throw new ArgumentNullException("production");
			}
			ThingDef plant = FarmingState(state)?.selectedPlant;
			if (plant?.plant == null)
			{
				return 0f;
			}
			PlantProperties plantProperties = plant.plant;
			if (plantProperties.growDays <= 0f)
			{
				return 0f;
			}
			return personnelCapacity * production.outputPerCapacity * plantProperties.harvestYield;
		}

		public override ThingDef GetProduct(OutpostProductionProperties production, OutpostProductionState state)
		{
			return FarmingState(state)?.selectedPlant?.plant?.harvestedThingDef;
		}

		public override int GetProductionIntervalTicks(OutpostProductionProperties production, OutpostProductionState state)
		{
			float growDays = FarmingState(state)?.selectedPlant?.plant?.growDays ?? 0f;
			return growDays > 0f ? Mathf.Max(Mathf.RoundToInt(growDays * GenDate.TicksPerDay), 1) : base.GetProductionIntervalTicks(production, state);
		}

		private static OutpostProductionState_Farming FarmingState(OutpostProductionState state)
		{
			return state as OutpostProductionState_Farming;
		}

		public override bool HasConfiguration(OutpostProductionProperties production)
		{
			return production is OutpostProductionProperties_Farming;
		}

		public override void EnsureConfiguration(OutpostProductionProperties production, OutpostProductionState state)
		{
			OutpostProductionState_Farming farmingState = FarmingState(state);
			if (farmingState != null && production is OutpostProductionProperties_Farming farming && !farming.IsSowable(farmingState.selectedPlant))
			{
				List<ThingDef> candidates = farming.SowablePlants();
				if (candidates.Count != 0)
				{
					farmingState.selectedPlant = candidates[0];
					ResetProductionTimer(production, farmingState);
				}
			}
		}

		public override void DrawConfiguration(Rect rect, string label, OutpostProductionProperties production, OutpostProductionState state)
		{
			if (production is OutpostProductionProperties_Farming farming)
			{
				string text = (FarmingState(state)?.selectedPlant?.LabelCap ?? "DreamsOutposts.NoCrop".Translate()) + ": " + label;
				TooltipHandler.TipRegion(rect, new TipSignal("DreamsOutposts.ChooseCropTip".Translate(), rect.GetHashCode()));
				if (Widgets.ButtonText(rect, text))
				{
					OpenConfiguration(production, state);
				}
			}
		}

		public override void OpenConfiguration(OutpostProductionProperties production, OutpostProductionState state, Action onChanged = null)
		{
			if (production is OutpostProductionProperties_Farming farming)
			{
				OpenPlantMenu(farming, state, onChanged);
			}
		}

		public override string ConfigurationSummary(OutpostProductionProperties production, OutpostProductionState state)
		{
			if (!(production is OutpostProductionProperties_Farming))
			{
				return null;
			}
			return "DreamsOutposts.CropSummary".Translate(FarmingState(state)?.selectedPlant?.LabelCap ?? "DreamsOutposts.None".Translate());
		}

		public override string ConfigurationTip(OutpostProductionProperties production)
		{
			return "DreamsOutposts.ChooseCropTip".Translate();
		}

		private static void OpenPlantMenu(OutpostProductionProperties_Farming farming, OutpostProductionState state, Action onChanged)
		{
			List<ThingDef> candidates = farming.SowablePlants();
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			ThingDef current = FarmingState(state)?.selectedPlant;
			for (int i = 0; i < candidates.Count; i++)
			{
				ThingDef plantDef = candidates[i];
				string suffix = ((plantDef == current) ? "DreamsOutposts.CurrentCropSuffix".Translate().ToString() : string.Empty);
				options.Add(new FloatMenuOption(plantDef.LabelCap + suffix, delegate
				{
					TrySetPlant(farming, state, plantDef);
					onChanged?.Invoke();
				}, MenuOptionPriority.Default, delegate(Rect optionRect)
				{
					TooltipHandler.TipRegion(optionRect, new TipSignal(PlantTooltip(plantDef), optionRect.GetHashCode()));
				}));
			}
			if (options.Count == 0)
			{
				options.Add(new FloatMenuOption("DreamsOutposts.NoCropAvailable".Translate(), null));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private static void TrySetPlant(OutpostProductionProperties_Farming farming, OutpostProductionState state, ThingDef plant)
		{
			OutpostProductionState_Farming farmingState = FarmingState(state);
			if (farmingState == null)
			{
				Log.Error("Tried to set plant " + (plant?.defName ?? "null") + " on production " + farming.id + ", but its runtime state is " + (state?.GetType().Name ?? "null") + " instead of OutpostProductionState_Farming.");
			}
			else if (!farming.IsSowable(plant))
			{
				Log.Error("Tried to set plant " + (plant?.defName ?? "null") + " on production " + farming.id + ", but it is not sowable here.");
			}
			else if (farmingState.selectedPlant != plant)
			{
				farmingState.selectedPlant = plant;
				ResetProductionTimer(farming, farmingState);
			}
		}

		private static void ResetProductionTimer(OutpostProductionProperties production, OutpostProductionState_Farming state)
		{
			state.nextProductionTick = Find.TickManager.TicksGame + production.Worker.GetProductionIntervalTicks(production, state);
		}

		private static string PlantTooltip(ThingDef plant)
		{
			PlantProperties properties = plant.plant;
			string productLabel = properties.harvestedThingDef?.LabelCap ?? "DreamsOutposts.Nothing".Translate();
			return "DreamsOutposts.PlantTooltip".Translate(plant.LabelCap, properties.growDays.ToString("0.##"), properties.harvestYield.ToString("0.##"), productLabel);
		}

		public override IEnumerable<string> ConfigErrors(OutpostProductionProperties production)
		{
			foreach (string item in base.ConfigErrors(production))
			{
				yield return item;
			}
			if (!(production is OutpostProductionProperties_Farming farming))
			{
				yield return "Farming worker requires OutpostProductionProperties_Farming; declare the rule with Class=\"DreamsOutposts.OutpostProductionProperties_Farming\".";
				yield break;
			}
			if (string.IsNullOrEmpty(farming.sowTag))
			{
				yield return "sowTag is empty, so no plant can ever match it; vanilla growing zones use \"Ground\" and hydroponics basins use \"Hydroponic\".";
			}
			else if (!farming.HasAnyPlantWithSowTag())
			{
				yield return "sowTag \"" + farming.sowTag + "\" matches no plant in the loaded mod list; check the spelling against PlantProperties.sowTags.";
			}
			if (production.outputPerCapacity <= 0f)
			{
				yield return "farming production requires a positive outputPerCapacity (it is the abstract farm scale, e.g. 160).";
			}
		}
	}
}
