using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	// Deserialization bridge for the former research component. PostLoadInit
	// replaces this with the current Process component while retaining its timer.
	public class OutpostFacilityComp_Research : OutpostFacilityComp_Process
	{
		public override void ExposeData()
		{
			base.ExposeData();
			if (Scribe.mode == LoadSaveMode.LoadingVars &&
				Scribe.loader.curXmlParent["nextResearchTick"] != null)
			{
				int nextResearchTick = 0;
				Scribe_Values.Look(ref nextResearchTick, "nextResearchTick", 0);
				if (states == null) states = new List<OutpostProcessState>();
				if (!states.Exists(state => state?.processId == "research"))
					states.Add(new OutpostProcessState("research", nextResearchTick));
			}
		}
	}
}
