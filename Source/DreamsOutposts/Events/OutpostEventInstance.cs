using Verse;

namespace DreamsOutposts
{
	public class OutpostEventInstance : IExposable
	{
		public OutpostEventDef def;

		public int createdTick;

		public int expireTick;

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref createdTick, "createdTick", 0);
			Scribe_Values.Look(ref expireTick, "expireTick", 0);
		}
	}
}
