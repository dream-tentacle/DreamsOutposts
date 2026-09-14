using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostAttackState : IExposable
	{
		public Faction faction;
		public Map homeMap;
		public float raidPoints;
		public float strength;
		public bool resolved;

		public void ExposeData()
		{
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref homeMap, "homeMap");
			Scribe_Values.Look(ref raidPoints, "raidPoints");
			Scribe_Values.Look(ref strength, "strength");
			Scribe_Values.Look(ref resolved, "resolved");
		}
	}
}
