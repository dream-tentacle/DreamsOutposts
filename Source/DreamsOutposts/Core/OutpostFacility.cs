using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacility : IExposable
	{
		public OutpostFacilityDef def;

		public List<OutpostProductionState> productionStates;

		/// <summary>
		/// 上次结算训练经验的 tick。只用于按经过时间换算经验，避免重复或漏算。
		/// </summary>
		public int lastTrainingTick;

		public OutpostFacility()
		{
			productionStates = new List<OutpostProductionState>();
		}

		public OutpostFacility(OutpostFacilityDef def)
			: this()
		{
			this.def = def;
		}

		public static OutpostFacility Create(OutpostFacilityDef def)
		{
			OutpostFacility facility = new OutpostFacility(def);
			facility.SynchronizeProductionStates();
			return facility;
		}

		public void SynchronizeProductionStates()
		{
			if (productionStates == null)
			{
				productionStates = new List<OutpostProductionState>();
			}
			if (def == null)
			{
				Log.Error("Tried to synchronize production states of a facility with no def; keeping existing states untouched.");
				return;
			}
			HashSet<string> seenIds = new HashSet<string>();
			for (int i = 0; i < productionStates.Count; i++)
			{
				OutpostProductionState state = productionStates[i];
				if (state == null || string.IsNullOrEmpty(state.productionId) || def.GetProduction(state.productionId) == null || !seenIds.Add(state.productionId))
				{
					productionStates.RemoveAt(i);
					i--;
				}
			}
			if (def.productions == null)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			for (int j = 0; j < def.productions.Count; j++)
			{
				OutpostProductionProperties production = def.productions[j];
				if (production != null && !string.IsNullOrEmpty(production.id))
				{
					OutpostProductionWorker worker = production.Worker;
					OutpostProductionState state2 = GetProductionState(production.id);
					if (state2 != null && !worker.StateClass.IsInstanceOfType(state2))
					{
						Log.Warning("Production " + production.id + " of " + (def?.defName ?? "null") + " expects state type " + worker.StateClass.Name + " but the saved state is " + state2.GetType().Name + "; replacing it with a fresh state of the expected type.");
						int carriedTick = ((state2.nextProductionTick > 0) ? state2.nextProductionTick : (now + production.intervalTicks));
						productionStates.Remove(state2);
						state2 = worker.CreateState(production.id, carriedTick);
						productionStates.Add(state2);
					}
					if (state2 == null)
					{
						state2 = worker.CreateState(production.id, now + production.intervalTicks);
						productionStates.Add(state2);
					}
					worker.EnsureConfiguration(production, state2);
				}
			}
		}

		public OutpostProductionState GetProductionState(string productionId)
		{
			if (productionStates == null || string.IsNullOrEmpty(productionId))
			{
				return null;
			}
			for (int i = 0; i < productionStates.Count; i++)
			{
				OutpostProductionState state = productionStates[i];
				if (state != null && state.productionId == productionId)
				{
					return state;
				}
			}
			return null;
		}

		public bool TryGetProductionState(string productionId, out OutpostProductionState state)
		{
			state = GetProductionState(productionId);
			return state != null;
		}

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref lastTrainingTick, "lastTrainingTick", 0);
			Scribe_Collections.Look(ref productionStates, "productionStates", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (productionStates == null)
				{
					productionStates = new List<OutpostProductionState>();
				}
				SynchronizeProductionStates();
			}
		}
	}
}
