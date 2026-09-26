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

		public virtual void Update(Outpost outpost, int delta) { }
		public virtual void UpdateDisabled(Outpost outpost, int delta) { }
		public virtual void PreRemove(Outpost outpost) { }
		/// <summary>
		/// 输出风格无关的设施语义信息。现代/原版 UI 只负责把这些信息投影成各自的视觉样式。
		/// </summary>
		public virtual void BuildUiInfo(Outpost outpost, UiFacilityInfoModel output) { }

		protected UiFacilityInfoGroup NewUiInfoGroup(string title, ThingDef iconThing = null, string tooltip = null)
		{
			return new UiFacilityInfoGroup
			{
				Title = title,
				IconThing = iconThing,
				Tooltip = tooltip,
				SourceCompType = GetType()
			};
		}
		public virtual void ExposeData() { }
	}
}
