using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public enum AdventurerRarity
	{
		Common,
		Excellent,
		Elite,
		Epic
	}

	/// <summary>How good this particular pawn is as a specimen of its own kind.</summary>
	public enum AdventurerSpecimen
	{
		Inferior,
		Ordinary,
		Superior
	}

	public struct AdventurerRarityProbabilities
	{
		public float Common;
		public float Excellent;
		public float Elite;
		public float Epic;
	}

	public static class AdventurerRecruitUtility
	{
		public const int RecruitIntervalTicks = 120000;
		public const int OfferLifetimeTicks = 600000;
		public const int MaxOffers = 3;
		public const string FacilityDefName = "DreamsOutposts_AdventurerCamp";

		private const float PopulationBaseline = 2f;
		private const float LeftFactor = 0.01f;
		private const float RightFactor = 0.127f;

		// Rating uses PawnKindDef.combatPower, the same per-kind number vanilla uses as its raid-point
		// cost. Scanned from all vanilla humanlike PawnKindDefs (Core + every DLC: 125 kinds,
		// combatPower 30..150) and from the pool this mod can actually build (441 kind x faction
		// entries, 58 kinds). The boundaries sit in the gaps of the real value set, so no kind sits
		// near a boundary:
		//   Common    cp <= 45   : 83 entries  (18.8%), 12 kinds (Drifter 35, Villager 45, Tribal_Archer 45)
		//   Excellent cp  50..60 : 119 entries (27.0%), 12 kinds (Scavenger 50, Town_Guard 60)
		//   Elite     cp  65..85 : 150 entries (34.0%), 15 kinds (Pirate 65, Mercenary_Gunner 85)
		//   Epic      cp >= 100  : 89 entries  (20.2%), 19 kinds (Mercenary_Elite 130, Mercenary_Heavy 140)
		private const float ExcellentCombatPower = 50f;
		private const float EliteCombatPower = 65f;
		private const float EpicCombatPower = 100f;

		// Specimen grade is measured on the same number vanilla shows as "character quality" on a pawn's
		// info card: market value relative to the race's base value (1750 for humans). Vanilla's own
		// AverageSkillCurve puts a healthy adult whose skills average 5.5 at exactly x1.00, so 1.0 means
		// "an experienced, fully healthy person of this race".
		private const float SuperiorSpecimenRatio = 0.85f;
		private const float InferiorSpecimenRatio = 0.50f;

		private const int PreferenceAttempts = 3;

		public static bool IsAvailable(Outpost outpost)
		{
			return CampCount(outpost) > 0;
		}

		public static int CampCount(Outpost outpost)
		{
			if (outpost == null) return 0;
			return outpost.Facilities.Count(f => f?.def?.defName == FacilityDefName);
		}

		public static void Tick(Outpost outpost)
		{
			if (outpost?.adventurerRecruitment == null || outpost.adventurerCandidates == null) return;
			int now = Find.TickManager.TicksGame;
			RemoveExpired(outpost, now);
			if (!IsAvailable(outpost))
			{
				ClearOffers(outpost);
				outpost.adventurerRecruitment.nextRecruitTick = 0;
				return;
			}
			if (outpost.adventurerRecruitment.nextRecruitTick <= 0)
				outpost.adventurerRecruitment.nextRecruitTick = now + RecruitIntervalTicks;
			if (outpost.adventurerRecruitment.offers.Count >= MaxOffers) return;
			if (now < outpost.adventurerRecruitment.nextRecruitTick) return;
			TryGenerateOffer(outpost);
			outpost.adventurerRecruitment.nextRecruitTick = now + RecruitIntervalTicks;
		}

		public static void Reconcile(Outpost outpost)
		{
			if (outpost?.adventurerRecruitment == null || outpost.adventurerCandidates == null) return;
			List<AdventurerOffer> offers = outpost.adventurerRecruitment.offers;
			for (int i = offers.Count - 1; i >= 0; i--)
			{
				Pawn pawn = offers[i]?.pawn;
				if (pawn == null || pawn.Destroyed || !outpost.adventurerCandidates.Contains(pawn)) offers.RemoveAt(i);
			}
		}

		public static bool TryGenerateOffer(Outpost outpost)
		{
			if (outpost == null || outpost.adventurerRecruitment.offers.Count >= MaxOffers) return false;
			List<Pair<PawnKindDef, Faction>> pool = BuildPawnKindPool();
			if (pool.Count == 0)
			{
				Log.Warning("DreamsOutposts: no eligible humanlike faction pawn kinds were available for the adventurer camp.");
				return false;
			}

			// The rolled rarity selects which kind of adventurer turns up, and the rating of the offer is
			// simply that kind's rating. Individual quality is reported separately, as specimen grade.
			AdventurerRarity wanted = RollRarity(outpost);
			List<Pair<PawnKindDef, Faction>> candidates = pool.Where(p => RarityForKind(p.First) == wanted).ToList();
			if (candidates.Count == 0)
			{
				// No kind of the wanted tier exists in this save (for instance the factions that field
				// them are all defeated), so fall back to the closest tier that does have kinds.
				int best = pool.Min(p => Math.Abs((int)RarityForKind(p.First) - (int)wanted));
				candidates = pool.Where(p => Math.Abs((int)RarityForKind(p.First) - (int)wanted) == best).ToList();
			}

			SkillDef preferred = outpost.adventurerRecruitment.preferredSkill;
			Pawn chosen = null;
			for (int attempt = 0; attempt < PreferenceAttempts && chosen == null; attempt++)
			{
				Pair<PawnKindDef, Faction> entry = candidates.RandomElement();
				Pawn pawn = Generate(entry.First, entry.Second, outpost);
				if (pawn == null) continue;
				if (IsPreferenceBlocked(pawn, preferred))
				{
					OutpostUtility.DiscardCandidate(pawn);
					continue;
				}
				chosen = pawn;
			}
			if (chosen == null) return false;

			if (!outpost.adventurerCandidates.TryAdd(chosen))
			{
				OutpostUtility.DiscardCandidate(chosen);
				return false;
			}
			OutpostUtility.TakeOutOfWorld(chosen);
			int now = Find.TickManager.TicksGame;
			outpost.adventurerRecruitment.offers.Add(new AdventurerOffer
			{
				pawn = chosen,
				createdTick = now,
				expireTick = now + OfferLifetimeTicks
			});
			return true;
		}

		private static Pawn Generate(PawnKindDef kind, Faction faction, Outpost outpost)
		{
			try
			{
				return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
					PawnGenerationContext.NonPlayer, outpost.Tile, forceGenerateNewPawn: true,
					allowDead: false, allowDowned: false, canGeneratePawnRelations: false,
					allowPregnant: false, forceRecruitable: true,
					developmentalStages: DevelopmentalStage.Adult));
			}
			catch (Exception ex)
			{
				Log.Warning("DreamsOutposts: failed to generate adventurer of kind " + kind?.defName + ": " + ex.Message);
				return null;
			}
		}

		private static List<Pair<PawnKindDef, Faction>> BuildPawnKindPool()
		{
			List<Pair<PawnKindDef, Faction>> result = new List<Pair<PawnKindDef, Faction>>();
			HashSet<string> seen = new HashSet<string>();
			foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
			{
				if (faction == null || faction.IsPlayer || faction.Hidden || faction.temporary || faction.defeated || !faction.def.humanlikeFaction || faction.def.pawnGroupMakers.NullOrEmpty()) continue;
				foreach (PawnGroupMaker maker in faction.def.pawnGroupMakers)
				{
					if (maker == null || maker.options.NullOrEmpty()) continue;
					if (maker.kindDef != PawnGroupKindDefOf.Combat && maker.kindDef != PawnGroupKindDefOf.Peaceful && maker.kindDef != PawnGroupKindDefOf.Settlement) continue;
					foreach (PawnGenOption option in maker.options)
					{
						PawnKindDef kind = option?.kind;
						if (!Eligible(kind) || !seen.Add(faction.loadID + ":" + kind.defName)) continue;
						result.Add(new Pair<PawnKindDef, Faction>(kind, faction));
					}
				}
			}
			return result;
		}

		private static bool Eligible(PawnKindDef kind)
		{
			if (kind?.race == null || !kind.RaceProps.Humanlike || kind.combatPower <= 0f) return false;
			if (kind.factionLeader || kind.trader || kind.isBoss) return false;
			// Child kinds (Villager_Child, Tribal_Child and friends) only ever show up as children in
			// pawn groups, and we generate adults, so they must never be drafted as adventurers.
			if (kind.pawnGroupDevelopmentStage == DevelopmentalStage.Child) return false;
			return true;
		}

		/// <summary>Rating of a kind, from combatPower - the same number vanilla uses as its raid-point cost.</summary>
		public static AdventurerRarity RarityForKind(PawnKindDef kind)
		{
			float combatPower = kind?.combatPower ?? 0f;
			if (combatPower >= EpicCombatPower) return AdventurerRarity.Epic;
			if (combatPower >= EliteCombatPower) return AdventurerRarity.Elite;
			if (combatPower >= ExcellentCombatPower) return AdventurerRarity.Excellent;
			return AdventurerRarity.Common;
		}

		public static AdventurerRarity RarityFor(Pawn pawn) => RarityForKind(pawn?.kindDef);

		/// <summary>
		/// Picks an eligible kind rated at <paramref name="rarity"/> from the same pool the tavern draws
		/// from. Returns false when no faction currently fields such a kind.
		/// </summary>
		public static bool TryGetEntryFor(AdventurerRarity rarity, out PawnKindDef kind, out Faction faction)
		{
			kind = null;
			faction = null;
			List<Pair<PawnKindDef, Faction>> matching = BuildPawnKindPool().Where(p => RarityForKind(p.First) == rarity).ToList();
			if (matching.Count == 0) return false;
			Pair<PawnKindDef, Faction> entry = matching.RandomElement();
			kind = entry.First;
			faction = entry.Second;
			return true;
		}

		/// <summary>
		/// The same measure as the "character quality" line on a pawn's info card: market value relative
		/// to the race's base value. 1.0 is a healthy adult with average (5.5) skills. It covers health,
		/// every capacity, every skill, life stage, traits and beauty; gear is valued separately by vanilla.
		/// </summary>
		public static float SpecimenRatio(Pawn pawn)
		{
			if (pawn?.def == null) return 1f;
			float baseValue = pawn.def.GetStatValueAbstract(StatDefOf.MarketValue);
			if (baseValue <= 0f) return 1f;
			return pawn.GetStatValue(StatDefOf.MarketValue) / baseValue;
		}

		public static AdventurerSpecimen SpecimenFor(Pawn pawn)
		{
			float ratio = SpecimenRatio(pawn);
			if (ratio >= SuperiorSpecimenRatio) return AdventurerSpecimen.Superior;
			if (ratio <= InferiorSpecimenRatio) return AdventurerSpecimen.Inferior;
			return AdventurerSpecimen.Ordinary;
		}

		private static bool IsPreferenceBlocked(Pawn pawn, SkillDef skill)
		{
			if (pawn?.skills == null || skill == null) return false;
			SkillRecord record = pawn.skills.GetSkill(skill);
			return record != null && record.TotallyDisabled;
		}

		public static float PopulationTendency(Outpost outpost)
		{
			OutpostEventCategoryDef category = DefDatabase<OutpostEventCategoryDef>.GetNamedSilentFail("DreamsOutposts_Population");
			return OutpostEventUtility.GetCategoryWeight(outpost, category);
		}

		public static AdventurerRarityProbabilities ProbabilitiesFor(Outpost outpost)
		{
			float x = Math.Max(PopulationTendency(outpost) - PopulationBaseline, 0f);
			float squared = x * x;
			float common = 75f / (1f + LeftFactor * squared);
			float excellent = 20f / (1f + LeftFactor * 0.2f * squared);
			float elite = 4f * (1f + RightFactor * 0.16f * squared);
			float epic = 1f * (1f + RightFactor * squared);
			float total = common + excellent + elite + epic;
			return new AdventurerRarityProbabilities
			{
				Common = common / total,
				Excellent = excellent / total,
				Elite = elite / total,
				Epic = epic / total
			};
		}

		private static AdventurerRarity RollRarity(Outpost outpost)
		{
			AdventurerRarityProbabilities probabilities = ProbabilitiesFor(outpost);
			float value = Rand.Value;
			if (value < probabilities.Epic) return AdventurerRarity.Epic;
			value -= probabilities.Epic;
			if (value < probabilities.Elite) return AdventurerRarity.Elite;
			value -= probabilities.Elite;
			if (value < probabilities.Excellent) return AdventurerRarity.Excellent;
			return AdventurerRarity.Common;
		}

		public static bool Recruit(Outpost outpost, AdventurerOffer offer)
		{
			Pawn pawn = offer?.pawn;
			if (outpost == null || pawn == null || !outpost.adventurerCandidates.Contains(pawn)) return false;
			bool wasFull = outpost.adventurerRecruitment.offers.Count >= MaxOffers;
			outpost.adventurerCandidates.Remove(pawn);
			outpost.adventurerRecruitment.offers.Remove(offer);
			if (wasFull) outpost.adventurerRecruitment.nextRecruitTick = Find.TickManager.TicksGame + RecruitIntervalTicks;
			pawn.SetFaction(Faction.OfPlayer);
			return OutpostUtility.MovePawnIntoOutpost(outpost, pawn);
		}

		public static void RemoveOffer(Outpost outpost, AdventurerOffer offer)
		{
			Pawn pawn = offer?.pawn;
			bool wasFull = (outpost?.adventurerRecruitment?.offers.Count ?? 0) >= MaxOffers;
			outpost?.adventurerRecruitment?.offers.Remove(offer);
			if (wasFull) outpost.adventurerRecruitment.nextRecruitTick = Find.TickManager.TicksGame + RecruitIntervalTicks;
			if (pawn != null && outpost?.adventurerCandidates != null)
			{
				outpost.adventurerCandidates.Remove(pawn);
				OutpostUtility.DiscardCandidate(pawn);
			}
		}

		private static void RemoveExpired(Outpost outpost, int now)
		{
			for (int i = outpost.adventurerRecruitment.offers.Count - 1; i >= 0; i--)
				if (outpost.adventurerRecruitment.offers[i] == null || now >= outpost.adventurerRecruitment.offers[i].expireTick)
					RemoveOffer(outpost, outpost.adventurerRecruitment.offers[i]);
		}

		private static void ClearOffers(Outpost outpost)
		{
			for (int i = outpost.adventurerRecruitment.offers.Count - 1; i >= 0; i--)
				RemoveOffer(outpost, outpost.adventurerRecruitment.offers[i]);
		}
	}
}
