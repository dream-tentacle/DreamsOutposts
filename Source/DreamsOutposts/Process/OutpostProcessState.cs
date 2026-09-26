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
			// Production states used these names before the shared process system.
			// Check node presence so a valid zero in a newer save wins over old data.
			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				if (Scribe.loader.curXmlParent["processId"] == null)
					Scribe_Values.Look(ref processId, "productionId");
				if (Scribe.loader.curXmlParent["nextProcessTick"] == null)
					Scribe_Values.Look(ref nextProcessTick, "nextProductionTick", 0);
			}
		}

		public override string ToString()
		{
			return processId + "@" + nextProcessTick;
		}
	}
}
