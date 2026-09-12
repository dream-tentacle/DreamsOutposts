using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState_Power : OutpostProductionState
	{
		public int poweredUntilTick;
		public Building linkedReceiver;

		public OutpostProductionState_Power()
		{
		}

		public OutpostProductionState_Power(string productionId, int nextProductionTick)
			: base(productionId, nextProductionTick)
		{
		}

		public bool IsPoweredNow => poweredUntilTick > Find.TickManager.TicksGame;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref poweredUntilTick, "poweredUntilTick", 0);
			Scribe_References.Look(ref linkedReceiver, "linkedReceiver");
		}
	}
}
