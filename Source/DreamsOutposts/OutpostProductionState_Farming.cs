using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_Farming : OutpostProductionState
	{
		public ThingDef selectedPlant;

		public OutpostProductionState_Farming()
		{
		}

		public OutpostProductionState_Farming(string productionId, int nextProductionTick)
			: base(productionId, nextProductionTick)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref selectedPlant, "selectedPlant");
		}
	}
}
