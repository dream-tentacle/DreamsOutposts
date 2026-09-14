using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostEventInstance : IExposable
	{
		public OutpostEventDef def;

		public int createdTick;

		public int expireTick;

		public OutpostAttackState attack;

		public List<ThingDef> storedThingDefs = new List<ThingDef>();

		public List<int> storedThingCounts = new List<int>();

		public void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref createdTick, "createdTick", 0);
			Scribe_Values.Look(ref expireTick, "expireTick", 0);
			Scribe_Deep.Look(ref attack, "attack");
			Scribe_Collections.Look(ref storedThingDefs, "storedThingDefs", LookMode.Def);
			Scribe_Collections.Look(ref storedThingCounts, "storedThingCounts", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				storedThingDefs = storedThingDefs ?? new List<ThingDef>();
				storedThingCounts = storedThingCounts ?? new List<int>();
			}
		}
	}
}
