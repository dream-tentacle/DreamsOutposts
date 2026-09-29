using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Bandwidth : OutpostFacilityCompProperties
	{
		public int bandwidth = 1;
		public int bandwidthPerOutpostLevel;
		public int retuneTicks = 60000;
		public OutpostFacilityCompProperties_Bandwidth() { compClass = typeof(OutpostFacilityComp_Bandwidth); }

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (bandwidth < 0) yield return "bandwidth cannot be negative.";
			if (bandwidthPerOutpostLevel < 0) yield return "bandwidthPerOutpostLevel cannot be negative.";
		}
	}

	public class OutpostFacilityComp_Bandwidth : OutpostFacilityComp
	{
		public Pawn tunedTo;
		public Pawn tuningTo;
		public int retuneTicksLeft;
		private bool hasBeenTuned;
		public OutpostFacilityCompProperties_Bandwidth Props => (OutpostFacilityCompProperties_Bandwidth)props;
		public bool IsTuning => tuningTo != null && retuneTicksLeft > 0;

		public int BandwidthFor(Outpost outpost)
		{
			int level = outpost?.level ?? 1;
			return Props.bandwidth + (level > 1 ? level - 1 : 0) * Props.bandwidthPerOutpostLevel;
		}

		public override void BuildUiInfo(Outpost outpost, UiFacilityInfoModel output)
		{
			int bandwidth = BandwidthFor(outpost);
			output.AddFact(new UiFacilityInfoItem
			{
				Id = "bandwidth.output",
				Kind = UiFacilityInfoKind.Value,
				CardPlacement = UiFacilityCardPlacement.Chip,
				Importance = UiFacilityInfoImportance.Compact,
				Label = "DreamsOutposts.Ui.Fact.Bandwidth".Translate().ToString(),
				Value = "+" + bandwidth,
				CompactText = "DreamsOutposts.Ui.Fact.BandwidthCompact".Translate(bandwidth).ToString(),
				Tone = UiChipKind.Info
			});
		}

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
			if (!IsTuning) return;
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
				if (outpost.Faction != Faction.OfPlayer) continue;
				foreach (OutpostFacilityComp_Bandwidth node in Nodes(outpost, true))
					if (!node.IsTuning && node.tunedTo == pawn) total += node.BandwidthFor(outpost);
			}
			return total;
		}

		public static void NotifyBandwidthChanged(Outpost outpost)
		{
			if (outpost == null) return;
			foreach (Pawn pawn in Nodes(outpost)
				.SelectMany(node => new[] { node.tunedTo, node.tuningTo })
				.Where(pawn => pawn?.mechanitor != null)
				.Distinct())
			{
				pawn.mechanitor.Notify_BandwidthChanged();
			}
		}
	}
}
