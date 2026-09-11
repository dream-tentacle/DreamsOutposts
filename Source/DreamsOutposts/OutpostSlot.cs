using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostSlot : IExposable
	{
		public OutpostFacility facility;

		public bool IsEmpty => facility == null;

		public AcceptanceReport CanInstall(OutpostFacilityDef def, Outpost outpost)
		{
			if (!IsEmpty)
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.SlotOccupied".Translate(facility.def?.LabelCap ?? ((TaggedString)"null")));
			}
			if (def == null)
			{
				return new AcceptanceReport("the facility def is null");
			}
			if (outpost == null)
			{
				return new AcceptanceReport("there is no outpost to check type restrictions and build cost against");
			}
			if (!def.installableAsExtension)
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.NotInstallable".Translate());
			}
			if (!def.IsAllowedIn(outpost.outpostTypeDef))
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.WrongOutpostType".Translate(outpost.outpostTypeDef?.LabelCap ?? ((TaggedString)"null")));
			}
			if (!def.IsResearchUnlocked)
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.ResearchMissing".Translate(def.FirstMissingResearch?.LabelCap ?? ((TaggedString)"null")));
			}
			if (OutpostUtility.IsInstallLimitReached(outpost, def))
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.LimitReached".Translate(def.maxPerOutpost));
			}
			List<ThingDefCountClass> missing = new List<ThingDefCountClass>();
			if (!OutpostBuildUtility.CanAfford(outpost, def, missing))
			{
				return new AcceptanceReport("DreamsOutposts.InstallFail.CannotAfford".Translate(OutpostBuildUtility.CostLabel(missing)));
			}
			return AcceptanceReport.WasAccepted;
		}

		public bool TryInstall(OutpostFacilityDef def, Outpost outpost, out AcceptanceReport report)
		{
			report = CanInstall(def, outpost);
			if (!report.Accepted)
			{
				Log.Error("Tried to install " + (def?.defName ?? "null") + " into outpost " + (outpost?.Label ?? "null") + ": " + report.Reason);
				return false;
			}
			facility = OutpostFacility.Create(def);
			if (!OutpostBuildUtility.TryPay(outpost, def))
			{
				Log.Error("Installed " + def.defName + " in outpost " + outpost.Label + " but failed to pay its build cost; rolling the installation back.");
				facility = null;
				report = new AcceptanceReport("the build cost could not be paid");
				return false;
			}
			return true;
		}

		public bool TryInstall(OutpostFacilityDef def, Outpost outpost)
		{
			AcceptanceReport report;
			return TryInstall(def, outpost, out report);
		}

		public AcceptanceReport CanRemove(Outpost outpost)
		{
			if (IsEmpty)
			{
				return new AcceptanceReport("this slot is already empty");
			}
			if (outpost == null)
			{
				return new AcceptanceReport("there is no outpost to refund the build cost to");
			}
			return AcceptanceReport.WasAccepted;
		}

		public bool TryRemove(Outpost outpost, out AcceptanceReport report)
		{
			report = CanRemove(outpost);
			if (!report.Accepted)
			{
				Log.Error("Tried to remove the facility in a slot of outpost " + (outpost?.Label ?? "null") + ": " + report.Reason);
				return false;
			}
			OutpostFacilityDef def = facility.def;
			facility = null;
			OutpostBuildUtility.Refund(outpost, def);
			report = AcceptanceReport.WasAccepted;
			return true;
		}

		public bool TryRemove(Outpost outpost)
		{
			AcceptanceReport report;
			return TryRemove(outpost, out report);
		}

		public void ExposeData()
		{
			Scribe_Deep.Look(ref facility, "facility");
		}
	}
}
