using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public abstract class OutpostFacilityCompProperties
	{
		public Type compClass;

		public virtual IEnumerable<string> ConfigErrors()
		{
			if (compClass == null || !typeof(OutpostFacilityComp).IsAssignableFrom(compClass) || compClass.IsAbstract || compClass.GetConstructor(Type.EmptyTypes) == null)
				yield return "compClass must be a concrete OutpostFacilityComp with a public parameterless constructor.";
		}
	}

	public abstract class OutpostFacilityComp : IExposable
	{
		public OutpostFacility parent;
		public OutpostFacilityCompProperties props;

		public virtual void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
		{
			this.parent = parent;
			this.props = props;
		}

		public virtual void Tick(Outpost outpost, int delta) { }
		public virtual void TickDisabled(Outpost outpost, int delta) { }
		public virtual void PreRemove(Outpost outpost) { }
		public virtual void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output) { }
		public virtual void ExposeData() { }
	}
}
