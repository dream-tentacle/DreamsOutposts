using System;
using Verse;

namespace DreamsOutposts
{
	public enum OutpostTemporaryEffectKind
	{
		ProductionFactor,
		CategoryOffset,
		MovementCostFactor,
		FacilityDisabled,
		DefenseOffset
	}

	public class OutpostTemporaryEffect : IExposable
	{
		public OutpostTemporaryEffectKind kind;
		public int startTick;
		public int expireTick;
		public float value;
		public bool consumeAfterProduction;
		public string productionTag;
		public bool excludeProductionTag;
		public OutpostFacilityDef producingFacility;
		public OutpostEventCategoryDef category;
		public bool targetCore;
		public int targetSlot = -1;
		public OutpostFacilityDef expectedFacility;

		public bool IsExpired(int now)
		{
			return expireTick <= now;
		}

		public bool IsActive(int now)
		{
			return startTick <= now && !IsExpired(now);
		}

		public bool MatchesProduction(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production)
		{
			if (expectedFacility != null && !MatchesFacility(outpost, facility)) return false;
			if (targetCore && facility != outpost?.coreFacility) return false;
			if (producingFacility != null && facility?.def != producingFacility) return false;
			if (string.IsNullOrWhiteSpace(productionTag)) return true;
			if (production?.tags == null) return excludeProductionTag;
			for (int i = 0; i < production.tags.Count; i++)
			{
				if (string.Equals(production.tags[i]?.Trim(), productionTag.Trim(), StringComparison.OrdinalIgnoreCase)) return !excludeProductionTag;
			}
			return excludeProductionTag;
		}

		public bool MatchesFacility(Outpost outpost, OutpostFacility facility)
		{
			if (outpost == null || facility == null || facility.def != expectedFacility) return false;
			if (targetCore) return facility == outpost.coreFacility;
			return targetSlot >= 0 && targetSlot < (outpost.extensionSlots?.Count ?? 0) && outpost.extensionSlots[targetSlot]?.facility == facility;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref kind, "kind");
			Scribe_Values.Look(ref startTick, "startTick");
			Scribe_Values.Look(ref expireTick, "expireTick");
			Scribe_Values.Look(ref value, "value");
			Scribe_Values.Look(ref consumeAfterProduction, "consumeAfterProduction");
			Scribe_Values.Look(ref productionTag, "productionTag");
			Scribe_Values.Look(ref excludeProductionTag, "excludeProductionTag");
			Scribe_Defs.Look(ref producingFacility, "producingFacility");
			Scribe_Defs.Look(ref category, "category");
			Scribe_Values.Look(ref targetCore, "targetCore");
			Scribe_Values.Look(ref targetSlot, "targetSlot", -1);
			Scribe_Defs.Look(ref expectedFacility, "expectedFacility");
		}
	}
}
