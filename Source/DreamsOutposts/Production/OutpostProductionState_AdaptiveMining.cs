using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_AdaptiveMining : OutpostProductionState
	{
		public ThingDef selectedMineral;

		public OutpostProductionState_AdaptiveMining()
		{
		}

		public OutpostProductionState_AdaptiveMining(string productionId, int nextProductionTick)
			: base(productionId, nextProductionTick)
		{
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref selectedMineral, "selectedMineral");
		}
	}
}
