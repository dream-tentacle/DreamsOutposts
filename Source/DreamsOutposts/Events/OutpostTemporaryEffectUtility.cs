using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostTemporaryEffectUtility
	{
		public static void Add(Outpost outpost, OutpostTemporaryEffect effect)
		{
			if (outpost == null || effect == null) return;
			if (outpost.temporaryEffects == null) outpost.temporaryEffects = new List<OutpostTemporaryEffect>();
			outpost.temporaryEffects.Add(effect);
		}

		public static void RemoveExpired(Outpost outpost, int now)
		{
			outpost?.temporaryEffects?.RemoveAll(effect => effect == null || effect.IsExpired(now));
		}

		public static bool IsFacilityDisabled(Outpost outpost, OutpostFacility facility)
		{
			int now = Find.TickManager.TicksGame;
			List<OutpostTemporaryEffect> effects = outpost?.temporaryEffects;
			for (int i = 0; i < (effects?.Count ?? 0); i++)
			{
				OutpostTemporaryEffect effect = effects[i];
				if (effect != null && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.FacilityDisabled && effect.MatchesFacility(outpost, facility)) return true;
			}
			return false;
		}

		public static float ProductionFactor(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production)
		{
			float result = 1f;
			int now = Find.TickManager.TicksGame;
			List<OutpostTemporaryEffect> effects = outpost?.temporaryEffects;
			for (int i = 0; i < (effects?.Count ?? 0); i++)
			{
				OutpostTemporaryEffect effect = effects[i];
				if (effect != null && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.ProductionFactor && effect.MatchesProduction(outpost, facility, production)) result *= Mathf.Max(effect.value, 0f);
			}
			return result;
		}

		public static void ConsumeProductionEffects(Outpost outpost, OutpostFacility facility, OutpostProductionProperties production)
		{
			int now = Find.TickManager.TicksGame;
			outpost?.temporaryEffects?.RemoveAll(effect => effect != null && effect.consumeAfterProduction && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.ProductionFactor && effect.MatchesProduction(outpost, facility, production));
		}

		public static float CategoryOffset(Outpost outpost, OutpostEventCategoryDef category)
		{
			float result = 0f;
			int now = Find.TickManager.TicksGame;
			List<OutpostTemporaryEffect> effects = outpost?.temporaryEffects;
			for (int i = 0; i < (effects?.Count ?? 0); i++)
			{
				OutpostTemporaryEffect effect = effects[i];
				if (effect != null && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.CategoryOffset && effect.category == category) result += effect.value;
			}
			return result;
		}

		public static float MovementCostFactor(Outpost outpost)
		{
			float result = 1f;
			int now = Find.TickManager.TicksGame;
			List<OutpostTemporaryEffect> effects = outpost?.temporaryEffects;
			for (int i = 0; i < (effects?.Count ?? 0); i++)
			{
				OutpostTemporaryEffect effect = effects[i];
				if (effect != null && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.MovementCostFactor) result *= Mathf.Max(effect.value, 0f);
			}
			return result;
		}

		public static float DefenseOffset(Outpost outpost)
		{
			float result = 0f;
			int now = Find.TickManager.TicksGame;
			List<OutpostTemporaryEffect> effects = outpost?.temporaryEffects;
			for (int i = 0; i < (effects?.Count ?? 0); i++)
			{
				OutpostTemporaryEffect effect = effects[i];
				if (effect != null && effect.IsActive(now) && effect.kind == OutpostTemporaryEffectKind.DefenseOffset) result += effect.value;
			}
			return result;
		}

		public static OutpostFacility DisableRandomFacility(Outpost outpost, int durationTicks, bool includeCore, string facilityTag = null, bool producersOnly = false)
		{
			if (outpost == null || durationTicks <= 0) return null;
			List<OutpostFacility> candidates = outpost.OperationalFacilities.Where(facility =>
				facility?.def != null &&
				(!producersOnly || facility.def.IsProducer) &&
				(includeCore || facility != outpost.coreFacility) &&
				(string.IsNullOrWhiteSpace(facilityTag) || facility.def.FacilityTag == OutpostFacilityTagRegistry.Normalize(facilityTag))).ToList();
			if (!candidates.TryRandomElement(out OutpostFacility selected)) return null;
			OutpostTemporaryEffect effect = new OutpostTemporaryEffect
			{
				kind = OutpostTemporaryEffectKind.FacilityDisabled,
				expireTick = Find.TickManager.TicksGame + durationTicks,
				expectedFacility = selected.def,
				targetCore = selected == outpost.coreFacility
			};
			if (!effect.targetCore)
			{
				for (int i = 0; i < (outpost.extensionSlots?.Count ?? 0); i++)
				{
					if (outpost.extensionSlots[i]?.facility == selected) { effect.targetSlot = i; break; }
				}
				if (effect.targetSlot < 0) return null;
			}
			Add(outpost, effect);
			return selected;
		}
	}
}
