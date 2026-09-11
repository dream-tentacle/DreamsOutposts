using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionContext
	{
		public readonly Outpost Outpost;

		public readonly OutpostFacility Facility;

		public readonly OutpostProductionProperties Production;

		public readonly OutpostProductionState State;

		public readonly int Now;

		public float BaseOutput;

		public float ModifiedOutput;

		public int WantedAmount;

		public int ActualAmount;

		public ThingDef Product;

		public List<Thing> Products = new List<Thing>();

		public int NextProductionInterval;

		public OutpostProductionOutcome Outcome = OutpostProductionOutcome.Idle;

		public string FailureReason;

		public OutpostProductionWorker Worker => Production?.Worker;

		public bool Succeeded => Outcome == OutpostProductionOutcome.Completed;

		public int EffectiveInterval => (NextProductionInterval <= 0) ? 1 : NextProductionInterval;

		public string RuleLabel => OutpostProductionUtility.RuleLabel(Facility, Production);

		public OutpostProductionContext(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production, OutpostProductionState state, int now)
		{
			Outpost = outpost;
			Facility = facility;
			Production = production;
			State = state;
			Now = now;
			NextProductionInterval = production?.intervalTicks ?? 0;
		}
	}
}
