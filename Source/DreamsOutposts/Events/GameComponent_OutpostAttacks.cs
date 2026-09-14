using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>One attack clock for the whole game, independent of the number of outposts.</summary>
	public class GameComponent_OutpostAttacks : GameComponent
	{
		private int nextAttackTick;
		public GameComponent_OutpostAttacks(Game game) { }

		public override void ExposeData()
		{
			Scribe_Values.Look(ref nextAttackTick, "nextAttackTick");
		}

		public override void GameComponentTick()
		{
			int now = Find.TickManager.TicksGame;
			if (now % 250 != 0) return;
			var outposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
				.Where(o => !o.Destroyed && o.Faction == Faction.OfPlayer).ToList();
			// Starts and deadlines lie on 250-tick boundaries. Do not depend on the slower outpost tick.
			foreach (Outpost outpost in outposts)
				if (outpost.events != null)
					foreach (OutpostEventInstance instance in outpost.events.Where(e => e?.attack != null && now >= e.expireTick).ToList())
						OutpostEventUtility.ResolveByTimeout(outpost, instance);
			if (nextAttackTick > 0 && now < nextAttackTick) return;
			if (outposts.Count == 0)
			{
				nextAttackTick = 0;
				return;
			}
			if (nextAttackTick <= 0)
			{
				nextAttackTick = now + Rand.RangeInclusive(14 * 60000, 21 * 60000);
				return;
			}
			if (outposts.Any(o => o.events != null && o.events.Any(e => e?.attack != null && !e.attack.resolved)))
			{
				nextAttackTick = now + 250;
				return;
			}
			OutpostEventDef def = DefDatabase<OutpostEventDef>.GetNamedSilentFail("DO_OutpostAttack");
			if (def != null && outposts.RandomElement().AddEvent(def) != null)
				nextAttackTick = now + Rand.RangeInclusive(14 * 60000, 21 * 60000);
			else
				nextAttackTick = now + 60000;
		}
	}
}
