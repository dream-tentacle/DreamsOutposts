using Verse;

namespace DreamsOutposts
{
	public class AdventurerOffer : IExposable
	{
		public Pawn pawn;
		public int createdTick;
		public int expireTick;

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_Values.Look(ref createdTick, "createdTick");
			Scribe_Values.Look(ref expireTick, "expireTick");
		}
	}
}
