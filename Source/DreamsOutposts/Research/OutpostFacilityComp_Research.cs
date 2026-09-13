using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Research : OutpostFacilityCompProperties
	{
		public StatDef capacityStat;
		public int intervalTicks = 30000;
		public float outputPerCapacity;
		public List<string> tags = new List<string> { "Research" };

		public OutpostFacilityCompProperties_Research()
		{
			compClass = typeof(OutpostFacilityComp_Research);
		}

		public OutpostProductionProperties MakeRule()
		{
			return new OutpostProductionProperties { id = "research", capacityStat = capacityStat, intervalTicks = intervalTicks, outputPerCapacity = outputPerCapacity, tags = tags };
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (capacityStat == null) yield return "capacityStat is required.";
			if (intervalTicks <= 0) yield return "intervalTicks must be positive.";
			if (outputPerCapacity <= 0f) yield return "outputPerCapacity must be positive.";
		}
	}

	public class OutpostFacilityComp_Research : OutpostFacilityComp
	{
		public int nextResearchTick;
		public OutpostFacilityCompProperties_Research Props => (OutpostFacilityCompProperties_Research)props;

		public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			base.Initialize(parent, props);
			if (nextResearchTick <= 0) nextResearchTick = Find.TickManager.TicksGame + Props.intervalTicks;
		}

		public override void Tick(Outpost outpost, int delta)
		{
			int now = Find.TickManager.TicksGame;
			while (now >= nextResearchTick)
			{
				ResearchProjectDef project = Find.ResearchManager.GetProject();
				if (project != null)
				{
					OutpostProductionProperties rule = Props.MakeRule();
					float capacity = rule.Worker.CalculatePersonnelCapacity(outpost.Pawns, outpost.outpostTypeDef, rule);
					float output = capacity * Props.outputPerCapacity;
					Find.ResearchManager.AddProgress(project, OutpostProductionUtility.ApplyModifiers(outpost, parent, rule, output));
				}
				nextResearchTick += Props.intervalTicks;
			}
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref nextResearchTick, "nextResearchTick", 0);
		}

		public override void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output)
		{
			OutpostProductionProperties rule = Props.MakeRule();
			float capacity = rule.Worker.CalculatePersonnelCapacity(outpost.Pawns, outpost.outpostTypeDef, rule);
			float amount = OutpostProductionUtility.ApplyModifiers(outpost, parent, rule, capacity * Props.outputPerCapacity);
			int remaining = Mathf.Max(nextResearchTick - Find.TickManager.TicksGame, 0);
			output.Add(new UiFacilitySectionView
			{
				Title = "DreamsOutposts.ResearchPoints".Translate(),
				MainText = "×" + amount.ToString("0.#"),
				LeftText = Find.ResearchManager.GetProject()?.LabelCap ?? "DreamsOutposts.None".Translate(),
				RightText = remaining.ToStringTicksToPeriod().ToString(),
				ShowProgress = true,
				Progress = Mathf.Clamp01(1f - (float)remaining / Props.intervalTicks),
				ProgressKind = UiChipKind.Info
			});
		}
	}
}
