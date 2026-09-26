using System.Collections.Generic;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Production : OutpostFacilityCompProperties_ProcessBase
	{
		public List<OutpostProductionProperties> productions = new List<OutpostProductionProperties>();

		public OutpostFacilityCompProperties_Production()
		{
			compClass = typeof(OutpostFacilityComp_Production);
		}

		public override IEnumerable<OutpostProcessProperties> Processes
		{
			get
			{
				for (int i = 0; i < (productions?.Count ?? 0); i++)
					yield return productions[i];
			}
		}

		protected override string ProcessCollectionName => "productions";
	}

	public class OutpostFacilityComp_Production : OutpostFacilityComp_Process
	{
		public OutpostFacilityCompProperties_Production Props =>
			(OutpostFacilityCompProperties_Production)props;

		protected override OutpostFacilityCompProperties_ProcessBase ProcessProps =>
			Props;

		public new OutpostProductionState GetState(string id)
		{
			return base.GetState(id) as OutpostProductionState;
		}
	}
}
