using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties_Farming : OutpostProductionProperties
	{
		public string sowTag = "Ground";

		public bool plantsGrowWildHere;

		public bool hasPermanentDarknessOutside;

		public OutpostProductionProperties_Farming()
		{
			workerClass = typeof(OutpostProductionWorker_Farming);
		}

		public bool IsSowable(ThingDef plantDef)
		{
			if (plantDef == null || plantDef.category != ThingCategory.Plant)
			{
				return false;
			}
			PlantProperties plant = plantDef.plant;
			if (plant == null)
			{
				return false;
			}
			if (string.IsNullOrEmpty(sowTag) || plant.sowTags == null || !plant.sowTags.Contains(sowTag))
			{
				return false;
			}
			if (!SowResearchFinished(plant))
			{
				return false;
			}
			if (plant.mustBePermanentDarknessToSow && !hasPermanentDarknessOutside)
			{
				return false;
			}
			if (plant.mustBeWildToSow && !plantsGrowWildHere)
			{
				return false;
			}
			return plant.Harvestable && plant.harvestedThingDef != null && plant.growDays > 0f;
		}

		public List<ThingDef> SowablePlants()
		{
			List<ThingDef> result = new List<ThingDef>();
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef plantDef = allDefs[i];
				if (IsSowable(plantDef))
				{
					result.Add(plantDef);
				}
			}
			result.Sort(CompareCandidates);
			return result;
		}

		public bool HasAnyPlantWithSowTag()
		{
			if (string.IsNullOrEmpty(sowTag))
			{
				return false;
			}
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef plantDef = allDefs[i];
				if (plantDef.category == ThingCategory.Plant && plantDef.plant != null && plantDef.plant.sowTags != null && plantDef.plant.sowTags.Contains(sowTag))
				{
					return true;
				}
			}
			return false;
		}

		private static bool SowResearchFinished(PlantProperties plant)
		{
			if (plant.sowResearchPrerequisites.NullOrEmpty())
			{
				return true;
			}
			for (int i = 0; i < plant.sowResearchPrerequisites.Count; i++)
			{
				if (!plant.sowResearchPrerequisites[i].IsFinished)
				{
					return false;
				}
			}
			return true;
		}

		private static float PlantListPriority(ThingDef plantDef)
		{
			if (plantDef.plant.IsTree)
			{
				return 1f;
			}
			switch (plantDef.plant.purpose)
			{
			case PlantPurpose.Food:
				return 4f;
			case PlantPurpose.Health:
				return 3f;
			case PlantPurpose.Beauty:
				return 2f;
			default:
				return 0f;
			}
		}

		private static int CompareCandidates(ThingDef a, ThingDef b)
		{
			int byPriority = PlantListPriority(b).CompareTo(PlantListPriority(a));
			if (byPriority != 0)
			{
				return byPriority;
			}
			return string.Compare(a.label, b.label, StringComparison.Ordinal);
		}
	}
}
