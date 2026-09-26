using System;
using System.Collections.Generic;

namespace DreamsOutposts
{
	internal static class OutpostFacilityCompMigration
	{
		// Reconcile saved runtime types with current definitions before Initialize
		// can evaluate any typed Props getter. Snapshot states before touching comps.
		internal static List<OutpostFacilityComp> Restore(OutpostFacility facility)
		{
			List<OutpostFacilityComp> saved = facility.comps ?? new List<OutpostFacilityComp>();
			List<OutpostProcessState> savedStates = new List<OutpostProcessState>();
			foreach (OutpostFacilityComp comp in saved)
				if (comp is OutpostFacilityComp_Process process && process.states != null)
					savedStates.AddRange(process.states);

			HashSet<OutpostFacilityComp> usedComps = new HashSet<OutpostFacilityComp>();
			HashSet<OutpostProcessState> usedStates = new HashSet<OutpostProcessState>();
			List<OutpostFacilityComp> restored = new List<OutpostFacilityComp>();
			for (int i = 0; i < (facility.def?.comps?.Count ?? 0); i++)
			{
				OutpostFacilityCompProperties properties = facility.def.comps[i];
				if (properties?.compClass == null) continue;
				OutpostFacilityComp comp = saved.Find(candidate => candidate != null &&
					!usedComps.Contains(candidate) && candidate.GetType() == properties.compClass);
				if (comp == null)
					comp = (OutpostFacilityComp)Activator.CreateInstance(properties.compClass);
				usedComps.Add(comp);

				if (comp is OutpostFacilityComp_Process target &&
					properties is OutpostFacilityCompProperties_ProcessBase processProperties)
				{
					List<OutpostProcessState> states = new List<OutpostProcessState>();
					foreach (OutpostProcessProperties rule in processProperties.Processes)
					{
						if (rule == null || string.IsNullOrEmpty(rule.id)) continue;
						OutpostProcessState state = target.states?.Find(candidate => candidate != null &&
							candidate.processId == rule.id && !usedStates.Contains(candidate));
						if (state == null)
							state = savedStates.Find(candidate => candidate != null &&
								candidate.processId == rule.id && !usedStates.Contains(candidate));
						if (state == null) continue;
						usedStates.Add(state);
						// Keep supported subclasses intact, including their chosen products.
						states.Add(rule.Worker.StateClass.IsInstanceOfType(state) ? state :
							rule.Worker.CreateState(rule.id, state.nextProcessTick));
					}
					target.states = states;
				}
				restored.Add(comp);
			}

			// All components must be visible before any Initialize hook runs.
			facility.comps = restored;
			int index = 0;
			for (int i = 0; i < (facility.def?.comps?.Count ?? 0); i++)
			{
				OutpostFacilityCompProperties properties = facility.def.comps[i];
				if (properties?.compClass != null)
					restored[index++].Initialize(facility, properties);
			}
			return restored;
		}
	}
}
