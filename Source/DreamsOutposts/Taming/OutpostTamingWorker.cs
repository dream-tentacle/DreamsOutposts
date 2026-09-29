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
			return OutpostProcessWorker_Taming.GetLocalAnimals(biome).Any()
				? AcceptanceReport.WasAccepted
				: new AcceptanceReport("DreamsOutposts.Taming.NoAnimals".Translate());
		}
	}

	public class OutpostProcessWorker_Taming : OutpostProcessWorker
	{
		private const string RareFacilityDefName = "DreamsOutposts_RareAnimalTrackingCenter";
		private const float RareAnimalChance = 0.05f;
		private static List<PawnKindDef> rareAnimals;

		public override void Execute(OutpostProcessContext context)
		{
			BiomeDef biome = Find.WorldGrid[context.Outpost.Tile].PrimaryBiome;
			List<PawnKindDef> localAnimals = GetLocalAnimals(biome).ToList();
			if (localAnimals.Count == 0)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = "no naturally spawning animals on this tile";
				return;
			}

			int count = GenMath.RoundRandom(context.ModifiedOutput);
			if (count <= 0)
			{
				context.Outcome = OutpostProcessOutcome.Idle;
				context.FailureReason = "taming capacity rounded to zero";
				return;
			}

			for (int i = 0; i < count; i++)
				Capture(context.Outpost, context.Facility, localAnimals.RandomElement());

			if (HasRareFacility(context.Outpost) &&
				Rand.Chance(RareAnimalChance) &&
				RareAnimals.TryRandomElement(out PawnKindDef rareAnimal))
			{
				Capture(context.Outpost, context.Facility, rareAnimal);
			}

			context.Outcome = OutpostProcessOutcome.Completed;
		}

		public static IEnumerable<PawnKindDef> GetLocalAnimals(BiomeDef biome)
		{
			if (biome == null) yield break;
			foreach (PawnKindDef kind in biome.AllWildAnimals)
			{
				if (IsEligibleAnimal(kind) && biome.CommonalityOfAnimal(kind) > 0f)
					yield return kind;
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
			return race != null &&
				race.Animal &&
				race.IsFlesh &&
				!race.IsAnomalyEntity &&
				race.allowedOnCaravan &&
				race.trainability != null;
		}

		private static bool HasRareFacility(Outpost outpost)
		{
			return outpost.OperationalFacilities.Any(
				facility => facility?.def?.defName == RareFacilityDefName);
		}

		private static void Capture(Outpost outpost, OutpostFacility facility, PawnKindDef kind)
		{
			Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer, outpost.Tile);
			if (!OutpostAutomaticAirdropUtility.TryDeliver(outpost, facility, new List<Thing> { pawn })
				&& !outpost.pawns.TryAdd(pawn))
			{
				pawn.Destroy();
				throw new InvalidOperationException(
					"Outpost refused captured animal " + kind.defName + ".");
			}
			outpost.RequestUpdate();
		}
	}
}
