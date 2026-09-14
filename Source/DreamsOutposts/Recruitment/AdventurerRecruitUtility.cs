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

		private const int GenerationAttempts = 12;
		private const float PopulationBaseline = 2f;
		private const float LeftFactor = 0.01f;
		private const float RightFactor = 0.127f;

		private sealed class GeneratedCandidate
		{
			public Pawn Pawn;
			public float Quality;
			public float Preference;
			public AdventurerRarity Rarity;
		}

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
			AdventurerRarity wanted = RollRarity(outpost);
			List<GeneratedCandidate> generated = new List<GeneratedCandidate>();
			Pawn retained = null;
			try
			{
			for (int i = 0; i < GenerationAttempts; i++)
			{
				Pair<PawnKindDef, Faction> entry = pool.RandomElement();
				Pawn pawn = Generate(entry.First, entry.Second, outpost);
				if (pawn == null) continue;
				GeneratedCandidate candidate = new GeneratedCandidate { Pawn = pawn };
				generated.Add(candidate);
				candidate.Quality = QualityScore(pawn);
				candidate.Preference = PreferenceScore(pawn, outpost.adventurerRecruitment.preferredSkill);
				candidate.Rarity = RarityFor(candidate.Quality);
			}
			if (generated.Count == 0) return false;
			GeneratedCandidate chosen = generated
				.OrderBy(c => Math.Abs((int)c.Rarity - (int)wanted))
				.ThenByDescending(c => c.Rarity == wanted ? c.Preference : 0f)
				.ThenByDescending(c => c.Quality)
				.First();
			if (!outpost.adventurerCandidates.TryAdd(chosen.Pawn))
			{
				return false;
			}
			OutpostUtility.TakeOutOfWorld(chosen.Pawn);
			int now = Find.TickManager.TicksGame;
			outpost.adventurerRecruitment.offers.Add(new AdventurerOffer
			{
				pawn = chosen.Pawn,
				createdTick = now,
				expireTick = now + OfferLifetimeTicks
			});
			retained = chosen.Pawn;
			return true;
			}
			finally
			{
				foreach (GeneratedCandidate candidate in generated)
					if (candidate.Pawn != retained) OutpostUtility.DiscardCandidate(candidate.Pawn);
			}
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
			return kind?.race != null && kind.RaceProps.Humanlike && kind.combatPower > 0f
				&& !kind.factionLeader && !kind.trader && !kind.isBoss;
		}

		public static float QualityScore(Pawn pawn)
		{
			if (pawn == null) return 0f;
			float score = 0f;
			if (pawn.skills != null)
			{
				float[] weights = { 0.7f, 0.55f, 0.4f, 0.3f, 0.2f };
				List<SkillRecord> skills = pawn.skills.skills.OrderByDescending(s => s.Level).Take(weights.Length).ToList();
				for (int i = 0; i < skills.Count; i++) score += skills[i].Level * weights[i];
				float passion = pawn.skills.skills.Sum(s => s.passion == Passion.Major ? 2.5f : s.passion == Passion.Minor ? 1.25f : 0f);
				score += Math.Min(passion, 10f);
			}
			float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
			score += health * 10f;
			if (pawn.health?.capacities != null)
			{
				PawnCapacityDef[] important = { PawnCapacityDefOf.Consciousness, PawnCapacityDefOf.Moving, PawnCapacityDefOf.Manipulation, PawnCapacityDefOf.Sight };
				float total = 0f;
				for (int i = 0; i < important.Length; i++) total += Math.Min(pawn.health.capacities.GetLevel(important[i]), 1.2f);
				score += total * 1.5f;
			}
			if (pawn.story?.traits != null)
			{
				float traits = pawn.story.traits.allTraits.Where(t => !t.Suppressed).Sum(t => t.CurrentData.marketValueFactorOffset * 5f);
				score += Math.Max(-6f, Math.Min(traits, 6f));
			}
			return Math.Max(score, 0f);
		}

		public static AdventurerRarity RarityFor(float score)
		{
			if (score >= 60f) return AdventurerRarity.Epic;
			if (score >= 48f) return AdventurerRarity.Elite;
			if (score >= 35f) return AdventurerRarity.Excellent;
			return AdventurerRarity.Common;
		}

		public static AdventurerRarity RarityFor(Pawn pawn) => RarityFor(QualityScore(pawn));

		private static float PreferenceScore(Pawn pawn, SkillDef skill)
		{
			if (pawn?.skills == null || skill == null) return 0f;
			SkillRecord record = pawn.skills.GetSkill(skill);
			if (record == null || record.TotallyDisabled) return -100f;
			return record.Level + (record.passion == Passion.Major ? 8f : record.passion == Passion.Minor ? 4f : 0f);
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
