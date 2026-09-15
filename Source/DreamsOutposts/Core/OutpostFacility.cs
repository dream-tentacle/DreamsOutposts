using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacility : IExposable
	{
		public OutpostFacilityDef def;

		public List<OutpostFacilityComp> comps;

		/// <summary>由自动空投机读取；true 时该设施新完成的生产会直接投送到主殖民地。</summary>
		public bool autoAirdropEnabled;

		/// <summary>智能空投时该设施产物希望保留在仓库中的数量。</summary>
		public int intelligentAirdropStockTarget;

		public OutpostFacility()
		{
			comps = new List<OutpostFacilityComp>();
		}

		public OutpostFacility(OutpostFacilityDef def)
			: this()
		{
			this.def = def;
		}

		public static OutpostFacility Create(OutpostFacilityDef def)
		{
			OutpostFacility facility = new OutpostFacility(def);
			facility.InitializeComps();
			return facility;
		}

		private void InitializeComps()
		{
			comps = new List<OutpostFacilityComp>();
			for (int i = 0; i < (def?.comps?.Count ?? 0); i++)
			{
				OutpostFacilityCompProperties properties = def.comps[i];
				if (properties?.compClass == null) continue;
				OutpostFacilityComp comp = (OutpostFacilityComp)System.Activator.CreateInstance(properties.compClass);
				comp.Initialize(this, properties);
				comps.Add(comp);
			}
		}

		public T GetComp<T>() where T : OutpostFacilityComp
		{
			for (int i = 0; i < (comps?.Count ?? 0); i++) if (comps[i] is T result) return result;
			return null;
		}

		public void UpdateComps(Outpost outpost, int delta)
		{
			for (int i = 0; i < (comps?.Count ?? 0); i++) comps[i]?.Update(outpost, delta);
		}

		public void UpdateDisabledComps(Outpost outpost, int delta)
		{
			for (int i = 0; i < (comps?.Count ?? 0); i++) comps[i]?.UpdateDisabled(outpost, delta);
		}

		public void PreRemove(Outpost outpost)
		{
			for (int i = 0; i < (comps?.Count ?? 0); i++) comps[i]?.PreRemove(outpost);
		}

		public void SynchronizeProductionStates()
		{
			GetComp<OutpostFacilityComp_Production>()?.SynchronizeStates();
		}

		public OutpostProductionState GetProductionState(string productionId)
		{
			return string.IsNullOrEmpty(productionId) ? null : GetComp<OutpostFacilityComp_Production>()?.GetState(productionId);
		}

		public bool TryGetProductionState(string productionId, out OutpostProductionState state)
		{
			state = GetProductionState(productionId);
			return state != null;
		}

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref autoAirdropEnabled, "autoAirdropEnabled", defaultValue: false);
			Scribe_Values.Look(ref intelligentAirdropStockTarget, "intelligentAirdropStockTarget", defaultValue: 0);
			Scribe_Collections.Look(ref comps, "comps", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (comps == null) InitializeComps();
				for (int i = 0; i < comps.Count; i++)
				{
					if (i < (def?.comps?.Count ?? 0)) comps[i].Initialize(this, def.comps[i]);
				}
				SynchronizeProductionStates();
			}
		}
	}
}
