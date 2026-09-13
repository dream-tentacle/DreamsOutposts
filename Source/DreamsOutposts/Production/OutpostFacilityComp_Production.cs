using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Production : OutpostFacilityCompProperties
	{
		public List<OutpostProductionProperties> productions = new List<OutpostProductionProperties>();

		public OutpostFacilityCompProperties_Production()
		{
			compClass = typeof(OutpostFacilityComp_Production);
		}
	}

	public class OutpostFacilityComp_Production : OutpostFacilityComp
	{
		public List<OutpostProductionState> states = new List<OutpostProductionState>();
		public OutpostFacilityCompProperties_Production Props => (OutpostFacilityCompProperties_Production)props;

		public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			base.Initialize(parent, props);
			SynchronizeStates();
		}

		public void SynchronizeStates()
		{
			if (states == null) states = new List<OutpostProductionState>();
			HashSet<string> seen = new HashSet<string>();
			for (int i = states.Count - 1; i >= 0; i--)
				if (states[i] == null || string.IsNullOrEmpty(states[i].productionId) || parent.def.GetProduction(states[i].productionId) == null || !seen.Add(states[i].productionId)) states.RemoveAt(i);
			int now = Find.TickManager.TicksGame;
			foreach (OutpostProductionProperties production in Props.productions)
			{
				if (production == null || string.IsNullOrEmpty(production.id)) continue;
				OutpostProductionState state = GetState(production.id);
				if (state == null || !production.Worker.StateClass.IsInstanceOfType(state))
				{
					if (state != null) states.Remove(state);
					state = production.Worker.CreateState(production.id, now + production.intervalTicks);
					states.Add(state);
				}
				production.Worker.EnsureConfiguration(production, state);
			}
		}

		public OutpostProductionState GetState(string id)
		{
			for (int i = 0; i < (states?.Count ?? 0); i++) if (states[i]?.productionId == id) return states[i];
			return null;
		}

		public override void Tick(Outpost outpost, int delta)
		{
			OutpostProductionUtility.TickFacility(outpost, parent);
		}

		public override void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output)
		{
			foreach (OutpostProductionProperties production in Props.productions)
			{
				if (production == null) continue;
				OutpostProductionState state = GetState(production.id);
				OutpostProductionUtility.TryGetProductionProduct(parent, production, out ThingDef product);
				float amount = 0f;
				OutpostProductionUtility.TryCalculateExpectedOutput(outpost, parent, production, out amount);
				bool hasProgress = OutpostProductionUtility.TryGetCycleProgress(parent, production, out float progress, out int remaining);
				string label = product != null ? product.LabelCap.ToString() : (!string.IsNullOrEmpty(production.outputLabelKey) ? production.outputLabelKey.Translate().ToString() : production.id);
				UiFacilitySectionView section = new UiFacilitySectionView
				{
					Title = label,
					IconThing = product,
					MainText = amount > 0f ? "×" + amount.ToString("0.#") : string.Empty,
					LeftText = hasProgress ? "DreamsOutposts.ProductionRemaining".Translate(label, remaining.ToStringTicksToPeriod()).ToString() : label,
					RightText = "DreamsOutposts.Ui.ProductionEvery".Translate(production.intervalTicks.ToStringTicksToPeriod()).ToString(),
					ShowProgress = true,
					Progress = hasProgress ? Mathf.Clamp01(progress) : 0f,
					ProgressKind = UiChipKind.Info
				};
				if (production.Worker.HasConfiguration(production))
				{
					section.ActionLabel = production.Worker.ConfigurationSummary(production, state);
					section.ActionTooltip = production.Worker.ConfigurationTip(production);
					section.Action = () => production.Worker.OpenConfiguration(production, state);
				}
				foreach (OutpostProductionModifierSource source in OutpostProductionUtility.MatchingModifiers(outpost, parent, production))
					section.Chips.Add(new UiChipView("×" + source.Modifier.factor.ToString("0.##"), UiChipKind.Good));
				output.Add(section);
			}
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref states, "states", LookMode.Deep);
		}
	}
}
