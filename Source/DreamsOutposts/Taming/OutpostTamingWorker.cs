using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public class OutpostWorker_Taming : OutpostWorker
	{
		public override AcceptanceReport CanCreate(IEnumerable<Pawn> pawns, PlanetTile tile)
		{
			BiomeDef biome = tile.Valid ? Find.WorldGrid[tile].PrimaryBiome : null;
			return OutpostProductionWorker_Taming.GetLocalAnimals(biome).Any()
				? AcceptanceReport.WasAccepted
				: new AcceptanceReport("DreamsOutposts.Taming.NoAnimals".Translate());
		}
	}

	public class OutpostProductionWorker_Taming : OutpostProductionWorker
	{
		private const string RareFacilityDefName = "DreamsOutposts_RareAnimalTrackingCenter";
		private const float RareAnimalChance = 0.05f;

		private static List<PawnKindDef> rareAnimals;

		public override bool UsesDynamicProduct => true;

		public override bool OverrideProductionCycle(OutpostProductionContext context)
		{
			BiomeDef biome = Find.WorldGrid[context.Outpost.Tile].PrimaryBiome;
			List<PawnKindDef> localAnimals = GetLocalAnimals(biome).ToList();
			if (localAnimals.Count == 0)
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "no naturally spawning animals on this tile";
				return true;
			}

			context.BaseOutput = CalculateProduction(context.Outpost.Pawns, context.Outpost.outpostTypeDef, context.Production, context.State);
			context.ModifiedOutput = OutpostProductionUtility.ApplyModifiers(context.Outpost, context.Facility, context.Production, context.BaseOutput);
			context.WantedAmount = GenMath.RoundRandom(context.ModifiedOutput);
			context.ActualAmount = context.WantedAmount;
			if (context.ActualAmount <= 0)
			{
				context.Outcome = OutpostProductionOutcome.Idle;
				context.FailureReason = "taming capacity rounded to zero";
				return true;
			}

			for (int i = 0; i < context.ActualAmount; i++)
			{
				Capture(context.Outpost, localAnimals.RandomElement());
			}

			if (HasRareFacility(context.Outpost) && Rand.Chance(RareAnimalChance) && RareAnimals.TryRandomElement(out PawnKindDef rareAnimal))
			{
				Capture(context.Outpost, rareAnimal);
			}

			context.Outcome = OutpostProductionOutcome.Completed;
			OutpostTemporaryEffectUtility.ConsumeProductionEffects(context.Outpost, context.Facility, context.Production);
			return true;
		}

		public static IEnumerable<PawnKindDef> GetLocalAnimals(BiomeDef biome)
		{
			if (biome == null)
			{
				yield break;
			}
			foreach (PawnKindDef kind in biome.AllWildAnimals)
			{
				if (IsEligibleAnimal(kind) && biome.CommonalityOfAnimal(kind) > 0f)
				{
					yield return kind;
				}
			}
		}

		private static List<PawnKindDef> RareAnimals
		{
			get
			{
				if (rareAnimals == null)
				{
					HashSet<PawnKindDef> naturalAnimals = new HashSet<PawnKindDef>(
						DefDatabase<BiomeDef>.AllDefsListForReading.SelectMany(GetLocalAnimals));
					rareAnimals = DefDatabase<PawnKindDef>.AllDefsListForReading
						.Where(kind => IsEligibleAnimal(kind) && !naturalAnimals.Contains(kind))
						.ToList();
				}
				return rareAnimals;
			}
		}

		private static bool IsEligibleAnimal(PawnKindDef kind)
		{
			RaceProperties race = kind?.race?.race;
			return race != null
				&& race.Animal
				&& race.IsFlesh
				&& !race.IsAnomalyEntity
				&& race.allowedOnCaravan
				&& race.trainability != null;
		}

		private static bool HasRareFacility(Outpost outpost)
		{
			return outpost.OperationalFacilities.Any(facility => facility?.def?.defName == RareFacilityDefName);
		}

		private static void Capture(Outpost outpost, PawnKindDef kind)
		{
			Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer, outpost.Tile);
			if (!outpost.pawns.TryAdd(pawn))
			{
				pawn.Destroy();
				throw new InvalidOperationException("Outpost refused captured animal " + kind.defName + ".");
			}
		}
	}
}
