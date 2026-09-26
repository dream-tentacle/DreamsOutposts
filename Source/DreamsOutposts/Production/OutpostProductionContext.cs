using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionContext : OutpostProcessContext
	{
		public int WantedAmount;
		public int ActualAmount;
		public ThingDef Product;
		public List<Thing> Products = new List<Thing>();

		public OutpostProductionProperties Production => (OutpostProductionProperties)Process;
		public new OutpostProductionState State => (OutpostProductionState)base.State;
		public new OutpostProductionWorker Worker => (OutpostProductionWorker)base.Worker;

		public OutpostProductionContext(
			Outpost outpost,
			OutpostFacility facility,
			OutpostProductionProperties production,
			OutpostProductionState state,
			int now)
			: base(outpost, facility, production, state, now)
		{
		}
	}
}
