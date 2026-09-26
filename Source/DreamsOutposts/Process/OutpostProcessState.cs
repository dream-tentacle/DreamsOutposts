using Verse;

namespace DreamsOutposts
{
	public class OutpostProcessState : IExposable
	{
		public string processId;
		public int nextProcessTick;

		public OutpostProcessState()
		{
		}

		public OutpostProcessState(string processId, int nextProcessTick)
		{
			this.processId = processId;
			this.nextProcessTick = nextProcessTick;
		}

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref processId, "processId");
			Scribe_Values.Look(ref nextProcessTick, "nextProcessTick", 0);
		}

		public override string ToString()
		{
			return processId + "@" + nextProcessTick;
		}
	}
}
