using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionState : IExposable
	{
		public string productionId;

		public int nextProductionTick;

		public OutpostProductionState()
		{
		}

		public OutpostProductionState(string productionId, int nextProductionTick)
		{
			this.productionId = productionId;
			this.nextProductionTick = nextProductionTick;
		}

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref productionId, "productionId");
			Scribe_Values.Look(ref nextProductionTick, "nextProductionTick", 0);
		}

		public override string ToString()
		{
			return productionId + "@" + nextProductionTick;
		}
	}
}
