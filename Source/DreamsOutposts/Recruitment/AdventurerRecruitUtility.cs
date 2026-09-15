using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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
		/// <summary>One attempt at the camp, successful or not, happens every three days.</summary>
		public const int RecruitIntervalTicks = 180000;

		public const int OfferLifetimeTicks = 600000;
		public const int MaxOffers = 3;
		public const string FacilityDefName = "DreamsOutposts_AdventurerCamp";

		/// <summary>
		/// Divisor the camp's social skill is measured against: colonists with fifty Social levels
		/// between them are certain to produce a candidate on every attempt.
		/// </summary>
		public const float RecruitChanceDivisor = 50f;

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

		private const int PreferenceSamples = 5;

		// Two guarantees are layered on top of whatever vanilla rolled, per tier: an age low enough to be
		// worth recruiting, and a band of passion in the skills the pawn can actually work to match the
		// tier. Passion is counted vanilla's way: a minor passion is one fire, a major one two. The lower
		// end of a band is filled in by EnsurePassions, the upper end is cut back by ReducePassions, and a
		// zero means that direction does not apply to the tier, so Common and Excellent are only ever cut
		// back while Epic is only ever filled in.
		private const int EliteMaxBiologicalAgeYears = 40;
		private const int EpicMaxBiologicalAgeYears = 35;
		private const int CommonMaxPassion = 4;
		private const int ExcellentMaxPassion = 6;
		private const int EliteMinPassion = 7;
		private const int EliteMaxPassion = 8;
		private const int EpicMinPassion = 10;
		// Every fire taken away also costs the skill this many raw levels.
		private const int PassionPenaltyLevels = 3;

		public static bool IsAvailable(Outpost outpost)
		{
			return CampCount(outpost) > 0;
		}

		public static int CampCount(Outpost outpost)
		{
			if (outpost == null) return 0;
			return outpost.Facilities.Count(f => f?.def?.defName == FacilityDefName);
		}

		/// <summary>
		/// Combined Social level of the colonists staffing the camp, which is what the recruit roll is
		/// measured against. Only colonists count: prisoners and other hangers-on do not run the tavern.
		/// A pawn whose Social is totally disabled contributes nothing, and neither does a pawn with no
		/// skills at all, both of which the shared AvailableSkillLevel helper already reports as -1.
		/// </summary>
		public static int SocialSkillTotal(Outpost outpost)
		{
			if (outpost == null) return 0;
			int total = 0;
			foreach (Pawn pawn in outpost.Colonists)
			{
				int level = OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Social);
				if (level > 0) total += level;
			}
			return total;
		}

		/// <summary>
		/// Chance that one attempt produces a candidate: the camp's social skill total divided by
		/// <see cref="RecruitChanceDivisor"/>, capped at certainty so a large population cannot push
		/// the roll past 100%.
		/// </summary>
		public static float RecruitChance(Outpost outpost)
		{
			return Mathf.Clamp01(SocialSkillTotal(outpost) / RecruitChanceDivisor);
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
			// One roll per attempt. A failed roll produces nothing at all - no letter, no message - and
			// the next attempt is a whole interval away, so a weak camp is simply quiet for longer.
			if (Rand.Chance(RecruitChance(outpost)))
			{
				if (TryGenerateOffer(outpost))
				{
					Messages.Message("DreamsOutposts.Tavern.Recruited".Translate(outpost.LabelCap), outpost,
						MessageTypeDefOf.PositiveEvent, historical: false);
				}
				else
				{
					// The roll succeeded, so a candidate should have turned up. Reaching this point means
					// the kind pool was empty, generation threw, or the candidate could not be registered.
					// The message exists so a silently doing-nothing tavern is never mistaken for a bad
					// roll; the log is what actually says which of the three happened.
					Log.Warning("DreamsOutposts: tavern recruitment at " + outpost.Label
						+ " rolled a success but produced no candidate.");
					Messages.Message("DreamsOutposts.Tavern.RecruitFailed", outpost, MessageTypeDefOf.NegativeEvent,
						historical: false);
				}
			}
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
			return TryGenerateOffer(outpost, null);
		}

		/// <summary>
		/// Generates one offer into the tavern. A null <paramref name="forcedRarity"/> rolls the tier from
		/// the population tendency; a fixed one forces that tier (the developer gizmo uses this). Everything
		/// else - kind pool, skill-preference sampling, epic sanitising, candidate bookkeeping - is shared.
		/// </summary>
		public static bool TryGenerateOffer(Outpost outpost, AdventurerRarity? forcedRarity)
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
			AdventurerRarity wanted = forcedRarity ?? RollRarity(outpost);
			List<Pair<PawnKindDef, Faction>> candidates = pool.Where(p => RarityForKind(p.First) == wanted).ToList();
			if (candidates.Count == 0)
			{
				// No kind of the wanted tier exists in this save (for instance the factions that field
				// them are all defeated), so fall back to the closest tier that does have kinds.
				int best = pool.Min(p => Math.Abs((int)RarityForKind(p.First) - (int)wanted));
				candidates = pool.Where(p => Math.Abs((int)RarityForKind(p.First) - (int)wanted) == best).ToList();
			}

			// One kind of the rolled tier turns up. With a skill preference set - and only then - several
			// specimens of that kind are generated and the best one in that skill is the one that shows up.
			Pair<PawnKindDef, Faction> entry = candidates.RandomElement();
			SkillDef preferred = outpost.adventurerRecruitment.preferredSkill;
			Pawn chosen = preferred == null
				? Generate(entry.First, entry.Second, outpost)
				: GenerateBestFor(entry.First, entry.Second, outpost, preferred);
			if (chosen == null) return false;
			SanitizeAdventurer(chosen);

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

		/// <summary>
		/// Traits that make an adventurer worse. Vanilla has no "negative trait" flag, so this follows only
		/// the agreed criteria: work the trait disables, a market-value penalty, more social fights, more
		/// disease, a bigger appetite, or a mental break threshold pushed up. Stat modifiers are otherwise
		/// deliberately ignored, because stat direction is per stat (low flammability is good, a low incoming
		/// damage factor is good) and vanilla does not mark it; the mental break threshold is the one agreed
		/// exception, where a positive offset is unambiguously worse.
		/// </summary>
		public static bool IsNegativeTrait(Trait trait)
		{
			TraitDef def = trait?.def;
			if (def == null) return false;
			if (!def.disabledWorkTypes.NullOrEmpty()) return true;
			if (def.disabledWorkTags != WorkTags.None) return true;
			TraitDegreeData data = trait.CurrentData;
			if (data == null) return false;
			if (data.marketValueFactorOffset < 0f) return true;
			if (data.socialFightChanceFactor > 1f) return true;
			if (data.randomDiseaseMtbDays > 0f) return true;
			if (data.hungerRateFactor > 1f) return true;
			if (RaisesMentalBreakThreshold(data)) return true;
			return false;
		}

		/// <summary>
		/// True when a trait degree pushes the mental break threshold up, meaning the pawn cracks under less
		/// pressure. Vanilla uses this on Nervous (+0.08), Volatile (+0.15), Neurotic (+0.08/+0.14) and
		/// Too Smart (+0.12), while Steadfast and Iron-willed subtract from it and stay clean.
		/// </summary>
		private static bool RaisesMentalBreakThreshold(TraitDegreeData data)
		{
			if (data.statOffsets == null) return false;
			for (int i = 0; i < data.statOffsets.Count; i++)
			{
				StatModifier offset = data.statOffsets[i];
				if (offset != null && offset.stat == StatDefOf.MentalBreakThreshold && offset.value > 0f) return true;
			}
			return false;
		}

		/// <summary>
		/// Strips every negative trait from a top-tier adventurer. Two known limits: skills a trait had
		/// already granted are not rolled back, and traits inherited from a gene are left alone because
		/// removing one would also remove the gene behind it.
		/// </summary>
		public static void StripNegativeTraitsIfEpic(Pawn pawn)
		{
			if (pawn?.story?.traits == null) return;
			if (RarityForKind(pawn.kindDef) != AdventurerRarity.Epic) return;
			List<Trait> toRemove = pawn.story.traits.allTraits
				.Where(t => t != null && t.sourceGene == null && IsNegativeTrait(t))
				.ToList();
			if (toRemove.Count == 0) return;
			for (int i = 0; i < toRemove.Count; i++) pawn.story.traits.RemoveTrait(toRemove[i]);
			Log.Message("DreamsOutposts: stripped " + toRemove.Count + " negative trait(s) from epic adventurer "
				+ pawn.LabelShort + ": " + string.Join(", ", toRemove.Select(t => t.Label).ToArray()));
		}

		/// <summary>
		/// Brings an adventurer of any tier inside its band: too many years behind them is fixed, the band's
		/// lower end is filled in, the upper end is cut back, and - for the top tier alone - negative traits
		/// and every hediff vanilla marks as bad are stripped. Negative traits go first so that a trait which
		/// is about to be removed cannot block the passion pass. HediffDef.isBad defaults to true and vanilla
		/// explicitly sets it false on implants and added parts, drug highs, pregnancy and the like, so
		/// prosthetics and bionics are untouched by this. A tier with a zero for a number skips that step.
		/// </summary>
		public static void SanitizeAdventurer(Pawn pawn)
		{
			if (pawn == null) return;
			AdventurerRarity rarity = RarityForKind(pawn.kindDef);
			CapBiologicalAge(pawn, MaxBiologicalAgeYearsFor(rarity));
			StripNegativeTraitsIfEpic(pawn);
			EnsurePassions(pawn, PassionTargetFor(rarity));
			ReducePassions(pawn, PassionCapFor(rarity));
			StripBadHediffsIfEpic(pawn);
		}

		/// <summary>Biological age ceiling guaranteed by a tier, or 0 when that tier gets no guarantee.</summary>
		public static int MaxBiologicalAgeYearsFor(AdventurerRarity rarity)
		{
			switch (rarity)
			{
				case AdventurerRarity.Epic: return EpicMaxBiologicalAgeYears;
				case AdventurerRarity.Elite: return EliteMaxBiologicalAgeYears;
				default: return 0;
			}
		}

		/// <summary>Lower end of a tier's passion band, or 0 when the tier is never filled in.</summary>
		public static int PassionTargetFor(AdventurerRarity rarity)
		{
			switch (rarity)
			{
				case AdventurerRarity.Epic: return EpicMinPassion;
				case AdventurerRarity.Elite: return EliteMinPassion;
				default: return 0;
			}
		}

		/// <summary>Upper end of a tier's passion band, or 0 when the tier is never cut back.</summary>
		public static int PassionCapFor(AdventurerRarity rarity)
		{
			switch (rarity)
			{
				case AdventurerRarity.Elite: return EliteMaxPassion;
				case AdventurerRarity.Excellent: return ExcellentMaxPassion;
				case AdventurerRarity.Common: return CommonMaxPassion;
				default: return 0;
			}
		}

		/// <summary>
		/// Clamps biological age down to <paramref name="maxYears"/>; a non-positive ceiling means the tier
		/// guarantees nothing and the pawn is left as generated. Biological age is what drives every stat, so
		/// it is the one clamped down; chronological age is left alone, which is why such a pawn reads as
		/// "35 (74)" on its info card. A race whose adulthood begins after the ceiling is skipped, because
		/// clamping it would produce a child.
		/// </summary>
		public static void CapBiologicalAge(Pawn pawn, int maxYears)
		{
			if (pawn?.ageTracker == null || maxYears <= 0) return;
			long capTicks = maxYears * 3600000L;
			if (pawn.ageTracker.AdultMinAgeTicks > capTicks) return;
			if (pawn.ageTracker.AgeBiologicalTicks <= capTicks) return;
			int before = pawn.ageTracker.AgeBiologicalYears;
			pawn.ageTracker.AgeBiologicalTicks = capTicks;
			Log.Message("DreamsOutposts: lowered " + RarityForKind(pawn.kindDef) + " adventurer " + pawn.LabelShort
				+ " from " + before + " to " + maxYears + " biological years of age.");
		}

		/// <summary>
		/// Total passion on the skills this pawn can actually work, counting a minor passion as one fire and
		/// a major passion as two. Skills the pawn cannot use at all are worth nothing, so they are skipped.
		/// </summary>
		private static int WorkablePassionCount(Pawn pawn)
		{
			int fires = 0;
			List<SkillRecord> skills = pawn.skills.skills;
			for (int i = 0; i < skills.Count; i++)
			{
				SkillRecord record = skills[i];
				if (record == null || record.TotallyDisabled) continue;
				if (record.passion == Passion.Major) fires += 2;
				else if (record.passion == Passion.Minor) fires += 1;
			}
			return fires;
		}

		/// <summary>
		/// True when an active gene grants this skill a level of passion: the "{0} great" aptitude genes
		/// (AptitudeRemarkable, +8 aptitude) also add one level of interest. That fire is the gene's doing,
		/// so the reduce pass leaves the skill alone.
		/// </summary>
		private static bool GeneAddsPassion(Pawn pawn, SkillDef skill)
		{
			return GenePassionMod(pawn, skill, PassionMod.PassionModType.AddOneLevel);
		}

		/// <summary>
		/// True when an active gene wipes every level of passion from this skill: the "{0} awful" aptitude
		/// genes (AptitudeTerrible, -8 aptitude) also strip all interest in it. Note that such a gene does
		/// not disable the skill, so the plain TotallyDisabled test does not catch it; vanilla's own skill
		/// generation skips these skills when handing out passion, and the fill pass does the same, because a
		/// fire given there would only contradict the gene.
		/// </summary>
		private static bool GeneDropsPassion(Pawn pawn, SkillDef skill)
		{
			return GenePassionMod(pawn, skill, PassionMod.PassionModType.DropAll);
		}

		/// <summary>
		/// Gene lookup behind the two filters above. Only active genes count, matching vanilla: Gene.Active
		/// already excludes genes that were overridden, are below their minAgeActive, or are switched off by
		/// the pawn's mutant def.
		/// </summary>
		private static bool GenePassionMod(Pawn pawn, SkillDef skill, PassionMod.PassionModType type)
		{
			if (!ModsConfig.BiotechActive || pawn?.genes == null || skill == null) return false;
			List<Gene> genes = pawn.genes.GenesListForReading;
			for (int i = 0; i < genes.Count; i++)
			{
				Gene gene = genes[i];
				PassionMod mod = gene?.def?.passionMod;
				if (gene.Active && mod != null && mod.skill == skill && mod.modType == type) return true;
			}
			return false;
		}

		/// <summary>
		/// Raises the pawn to <paramref name="target"/> fires of passion across the skills it can work; a
		/// non-positive target means the tier guarantees nothing. Each round takes the remaining shortfall
		/// and the skills that are not already major; when there are more of those skills than the shortfall,
		/// that many of them are picked at random and given one fire each, and when there are fewer, every
		/// one of them is given one fire and the round repeats. A pawn with only two workable skills
		/// therefore tops out at four fires and stops there. Skills an active gene has stripped of all
		/// interest are never picked.
		/// </summary>
		public static void EnsurePassions(Pawn pawn, int target)
		{
			if (pawn?.skills?.skills == null || target <= 0) return;

			int added = 0;
			// Every addition is worth exactly one fire, so the shortfall strictly shrinks and this ends.
			for (int round = 0; round <= target; round++)
			{
				int shortfall = target - WorkablePassionCount(pawn);
				if (shortfall <= 0) break;
				List<SkillRecord> upgradable = pawn.skills.skills
					.Where(s => s != null && !s.TotallyDisabled && s.passion != Passion.Major
						&& !GeneDropsPassion(pawn, s.def))
					.ToList();
				// Nothing left below major passion and the target still is not met: stop.
				if (upgradable.Count == 0) break;
				if (upgradable.Count > shortfall)
				{
					for (int i = 0; i < shortfall; i++)
					{
						SkillRecord pick = upgradable.RandomElement();
						upgradable.Remove(pick);
						pick.passion = pick.passion.IncrementPassion();
					}
					added += shortfall;
					break;
				}
				for (int i = 0; i < upgradable.Count; i++)
					upgradable[i].passion = upgradable[i].passion.IncrementPassion();
				added += upgradable.Count;
			}
			if (added > 0)
				Log.Message("DreamsOutposts: gave " + RarityForKind(pawn.kindDef) + " adventurer " + pawn.LabelShort
					+ " " + added + " passion point(s) toward " + target + ", now at " + WorkablePassionCount(pawn) + ".");
		}

		/// <summary>
		/// Cuts a pawn back down to <paramref name="cap"/> fires of passion; a non-positive cap means the
		/// tier is never cut back. Each round takes the excess and the skills that carry a fire; when there
		/// are more of those skills than the excess, that many of them are picked at random and lose one fire
		/// each, and when there are fewer, every one of them loses one fire and the round repeats. Every fire
		/// taken also costs the skill <see cref="PassionPenaltyLevels"/> raw levels, so a skill hit twice
		/// (major, then minor) pays twice. A skill whose fire came from an active gene is never picked, so a
		/// pawn whose excess sits entirely on such skills is simply left above the cap.
		/// </summary>
		public static void ReducePassions(Pawn pawn, int cap)
		{
			if (pawn?.skills?.skills == null || cap <= 0) return;

			int removed = 0;
			int levels = 0;
			// Every removal takes exactly one fire, so the excess strictly shrinks and this ends.
			for (int round = 0; round <= cap; round++)
			{
				int excess = WorkablePassionCount(pawn) - cap;
				if (excess <= 0) break;
				List<SkillRecord> burning = pawn.skills.skills
					.Where(s => s != null && !s.TotallyDisabled && s.passion != Passion.None
						&& !GeneAddsPassion(pawn, s.def))
					.ToList();
				// No skill carries a fire any more, so there is nothing left to take.
				if (burning.Count == 0) break;
				if (burning.Count > excess)
				{
					for (int i = 0; i < excess; i++)
					{
						SkillRecord pick = burning.RandomElement();
						burning.Remove(pick);
						levels += DowngradePassion(pick);
					}
					removed += excess;
					break;
				}
				for (int i = 0; i < burning.Count; i++)
					levels += DowngradePassion(burning[i]);
				removed += burning.Count;
			}
			if (removed > 0)
				Log.Message("DreamsOutposts: took " + removed + " passion point(s) and " + levels
					+ " raw skill level(s) from " + RarityForKind(pawn.kindDef) + " adventurer " + pawn.LabelShort
					+ ", now at " + WorkablePassionCount(pawn) + ".");
		}

		/// <summary>
		/// Takes one fire off a skill - major becomes minor, minor becomes none - and drops the skill's raw
		/// level by <see cref="PassionPenaltyLevels"/>. Only the raw level moves: the displayed level is
		/// <c>levelInt + Aptitude</c>, and Aptitude is where genes and traits add their skill levels, so
		/// writing levelInt leaves every gene- and trait-granted level intact.
		/// </summary>
		private static int DowngradePassion(SkillRecord record)
		{
			record.passion = (record.passion == Passion.Major) ? Passion.Minor : Passion.None;
			int before = record.levelInt;
			record.levelInt = Math.Max(record.levelInt - PassionPenaltyLevels, 0);
			return before - record.levelInt;
		}

		/// <summary>
		/// Removes every hediff whose def is flagged isBad. Missing parts go through vanilla RestorePart
		/// instead of RemoveHediff: Hediff_MissingPart.PostAdd gives the missing part's whole subtree a
		/// MissingPart of its own, and RestorePart is what clears that subtree consistently.
		/// </summary>
		public static void StripBadHediffsIfEpic(Pawn pawn)
		{
			if (pawn?.health?.hediffSet == null) return;
			if (RarityForKind(pawn.kindDef) != AdventurerRarity.Epic) return;

			List<Hediff> bad = pawn.health.hediffSet.hediffs.Where(h => h?.def != null && h.def.isBad).ToList();
			if (bad.Count == 0) return;
			List<string> labels = bad.Select(h => h.def.LabelCap.ToString()).ToList();

			List<BodyPartRecord> missingParts = bad
				.Where(h => h is Hediff_MissingPart && h.Part != null)
				.Select(h => h.Part)
				.ToList();
			for (int i = 0; i < missingParts.Count; i++) pawn.health.RestorePart(missingParts[i]);

			for (int i = 0; i < bad.Count; i++)
			{
				Hediff hediff = bad[i];
				if (hediff is Hediff_MissingPart) continue;
				// A RestorePart call above may already have cleared this one along with its subtree.
				if (!pawn.health.hediffSet.hediffs.Contains(hediff)) continue;
				pawn.health.RemoveHediff(hediff);
			}
			Log.Message("DreamsOutposts: stripped " + labels.Count + " bad hediff(s) from epic adventurer "
				+ pawn.LabelShort + ": " + string.Join(", ", labels.ToArray()));
		}

		/// <summary>
		/// Generates up to <see cref="PreferenceSamples"/> specimens of one kind and keeps the best in the
		/// preferred skill. The rejected specimens are discarded and never reach the offer list.
		/// </summary>
		private static Pawn GenerateBestFor(PawnKindDef kind, Faction faction, Outpost outpost, SkillDef preferred)
		{
			List<Pawn> samples = new List<Pawn>();
			for (int i = 0; i < PreferenceSamples; i++)
			{
				Pawn pawn = Generate(kind, faction, outpost);
				if (pawn != null) samples.Add(pawn);
			}
			if (samples.Count == 0) return null;

			Pawn best = samples[0];
			float bestScore = PreferenceScore(best, preferred);
			for (int i = 1; i < samples.Count; i++)
			{
				float score = PreferenceScore(samples[i], preferred);
				if (score > bestScore)
				{
					bestScore = score;
					best = samples[i];
				}
			}
			for (int i = 0; i < samples.Count; i++)
				if (samples[i] != best) OutpostUtility.DiscardCandidate(samples[i]);
			return best;
		}

		/// <summary>
		/// Ranking for the skill preference: level dominates, passion only breaks ties within a level, and
		/// anyone totally incapable of the skill always loses.
		/// </summary>
		private static float PreferenceScore(Pawn pawn, SkillDef skill)
		{
			if (pawn?.skills == null || skill == null) return -1f;
			SkillRecord record = pawn.skills.GetSkill(skill);
			if (record == null || record.TotallyDisabled) return -1f;
			float passion = record.passion == Passion.Major ? 2f : record.passion == Passion.Minor ? 1f : 0f;
			return record.Level * 10f + passion;
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
		/// Translated name of a rating. Both the tavern page and the event preview use this, so a rating
		/// never shows up as its raw enum name (which is what "Excellent" would look like in a Chinese game).
		/// </summary>
		public static string RarityLabel(AdventurerRarity rarity) => ("DreamsOutposts.Tavern.Rarity." + rarity).Translate();

		/// <summary>Translated name of a specimen grade, shared the same way as <see cref="RarityLabel"/>.</summary>
		public static string SpecimenLabel(AdventurerSpecimen specimen) => ("DreamsOutposts.Tavern.Specimen." + specimen).Translate();

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
