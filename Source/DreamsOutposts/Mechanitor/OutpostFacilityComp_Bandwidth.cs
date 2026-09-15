using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Bandwidth : OutpostFacilityCompProperties
	{
		public int bandwidth = 1;
		public int retuneTicks = 60000;
		public OutpostFacilityCompProperties_Bandwidth() { compClass = typeof(OutpostFacilityComp_Bandwidth); }
	}

	public class OutpostFacilityComp_Bandwidth : OutpostFacilityComp
	{
		public Pawn tunedTo;
		public Pawn tuningTo;
		public int retuneTicksLeft;
		private bool hasBeenTuned;
		public OutpostFacilityCompProperties_Bandwidth Props => (OutpostFacilityCompProperties_Bandwidth)props;
		public bool IsTuning => tuningTo != null && retuneTicksLeft > 0;

		public void StartTuning(Pawn pawn)
		{
			if (pawn == null || (!IsTuning && pawn == tunedTo)) return;
			if (!hasBeenTuned)
			{
				Pawn old = tunedTo;
				tunedTo = pawn;
				tuningTo = null;
				retuneTicksLeft = 0;
				hasBeenTuned = true;
				NotifyBandwidthChanged(old, tunedTo);
				return;
			}
			tuningTo = pawn;
			retuneTicksLeft = Props.retuneTicks;
			NotifyBandwidthChanged(tunedTo, null);
		}

		public override void Update(Outpost outpost, int delta)
		{
			if (!IsTuning || !OutpostBandwidthUtility.HasOperator(outpost)) return;
			retuneTicksLeft -= delta;
			if (retuneTicksLeft > 0) return;
			Pawn old = tunedTo;
			tunedTo = tuningTo;
			tuningTo = null;
			retuneTicksLeft = 0;
			NotifyBandwidthChanged(old, tunedTo);
		}

		public override void PreRemove(Outpost outpost) { NotifyBandwidthChanged(tunedTo, tuningTo); }

		public override void ExposeData()
		{
			Scribe_References.Look(ref tunedTo, "tunedTo");
			Scribe_References.Look(ref tuningTo, "tuningTo");
			Scribe_Values.Look(ref retuneTicksLeft, "retuneTicksLeft", 0);
			Scribe_Values.Look(ref hasBeenTuned, "hasBeenTuned", false);
		}

		private static void NotifyBandwidthChanged(params Pawn[] pawns)
		{
			foreach (Pawn pawn in pawns.Where(p => p?.mechanitor != null).Distinct()) pawn.mechanitor.Notify_BandwidthChanged();
		}
	}

	public static class OutpostBandwidthUtility
	{
		public static bool HasOperator(Outpost outpost)
		{
			return outpost?.Colonists.Any(p => p?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level >= 8) == true;
		}

		public static IEnumerable<OutpostFacilityComp_Bandwidth> Nodes(Outpost outpost, bool operationalOnly = false)
		{
			IEnumerable<OutpostFacility> facilities = operationalOnly ? outpost?.OperationalFacilities : outpost?.Facilities;
			if (facilities == null) yield break;
			foreach (OutpostFacility facility in facilities)
			{
				OutpostFacilityComp_Bandwidth comp = facility?.GetComp<OutpostFacilityComp_Bandwidth>();
				if (comp != null) yield return comp;
			}
		}

		public static int BonusFor(Pawn pawn)
		{
			if (pawn == null || pawn.Faction != Faction.OfPlayer || !MechanitorUtility.IsMechanitor(pawn)) return 0;
			int total = 0;
			foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
			{
				if (outpost.Faction != Faction.OfPlayer || !HasOperator(outpost)) continue;
				foreach (OutpostFacilityComp_Bandwidth node in Nodes(outpost, true))
					if (!node.IsTuning && node.tunedTo == pawn) total += node.Props.bandwidth;
			}
			return total;
		}
	}
}
