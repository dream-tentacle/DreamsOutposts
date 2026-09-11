using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostBombardmentProperties
	{
		public ThingDef shellDef;

		public int shellsPerStrike = 4;

		public List<ThingDefCountClass> costPerShell = new List<ThingDefCountClass>();

		public int maxRangeTiles = 12;

		public float cooldownHours = 18f;

		public int ticksBetweenShells = 60;

		public ResearchProjectDef researchPrerequisite;

		public SkillDef requiredSkill;

		public int requiredSkillLevel;

		public float missRadius = -1f;

		private const string VanillaMortarGunDefName = "Artillery_Mortar";

		private const float FallbackMissRadius = 9f;

		private static ThingDef cachedVanillaMortarGun;

		private static bool vanillaMortarGunLookedUp;

		private static readonly List<ThingDefCountClass> NoCost = new List<ThingDefCountClass>();

		public ThingDef ProjectileDef => shellDef?.projectileWhenLoaded;

		public int CooldownTicks => Mathf.Max(0, Mathf.RoundToInt(cooldownHours * 2500f));

		public List<ThingDefCountClass> CostPerShell => costPerShell.NullOrEmpty() ? NoCost : costPerShell;

		public float EffectiveMissRadius
		{
			get
			{
				if (missRadius >= 0f)
				{
					return missRadius;
				}
				if (!vanillaMortarGunLookedUp)
				{
					cachedVanillaMortarGun = DefDatabase<ThingDef>.GetNamedSilentFail("Artillery_Mortar");
					vanillaMortarGunLookedUp = true;
				}
				float radius = ((cachedVanillaMortarGun != null && !cachedVanillaMortarGun.Verbs.NullOrEmpty()) ? cachedVanillaMortarGun.Verbs[0].ForcedMissRadius : 0f);
				return (radius > 0f) ? radius : 9f;
			}
		}

		public bool HasSkillRequirement => requiredSkill != null && requiredSkillLevel > 0;

		public bool PawnMeetsSkillRequirement(Pawn pawn)
		{
			if (!HasSkillRequirement)
			{
				return true;
			}
			if (pawn?.skills == null)
			{
				return false;
			}
			SkillRecord record = pawn.skills.GetSkill(requiredSkill);
			return record != null && record.Level >= requiredSkillLevel;
		}

		public List<ThingDefCountClass> CostForShells(int shells)
		{
			List<ThingDefCountClass> result = new List<ThingDefCountClass>();
			if (shells <= 0 || costPerShell.NullOrEmpty())
			{
				return result;
			}
			for (int i = 0; i < costPerShell.Count; i++)
			{
				ThingDefCountClass entry = costPerShell[i];
				if (entry?.thingDef == null || entry.count <= 0 || AlreadyListedIn(result, entry.thingDef))
				{
					continue;
				}
				int perShell = 0;
				for (int j = 0; j < costPerShell.Count; j++)
				{
					ThingDefCountClass other = costPerShell[j];
					if (other?.thingDef == entry.thingDef && other.count > 0)
					{
						perShell += other.count;
					}
				}
				result.Add(new ThingDefCountClass(entry.thingDef, perShell * shells));
			}
			return result;
		}

		private static bool AlreadyListedIn(List<ThingDefCountClass> list, ThingDef thingDef)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i]?.thingDef == thingDef)
				{
					return true;
				}
			}
			return false;
		}

		public IEnumerable<string> ConfigErrors()
		{
			if (shellDef == null)
			{
				yield return "shellDef is required: a bombardment must know which shell it fires.";
			}
			else if (shellDef.projectileWhenLoaded == null)
			{
				yield return "shellDef " + shellDef.defName + " has no projectileWhenLoaded, so it has nothing to fire.";
			}
			if (shellsPerStrike <= 0)
			{
				yield return "shellsPerStrike must be positive.";
			}
			if (maxRangeTiles <= 0)
			{
				yield return "maxRangeTiles must be positive.";
			}
			if (cooldownHours < 0f)
			{
				yield return "cooldownHours must not be negative.";
			}
			if (requiredSkill == null && requiredSkillLevel > 0)
			{
				yield return "requiredSkillLevel is set but requiredSkill is missing; nobody would be filtered out.";
			}
			if (requiredSkillLevel < 0 || requiredSkillLevel > 20)
			{
				yield return "requiredSkillLevel must be between 0 and 20.";
			}
			if (ticksBetweenShells < 0)
			{
				yield return "ticksBetweenShells must not be negative.";
			}
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < (costPerShell?.Count ?? 0); i++)
			{
				ThingDefCountClass entry = costPerShell[i];
				if (entry == null)
				{
					yield return "costPerShell[" + i + "] is null.";
					continue;
				}
				if (entry.thingDef == null)
				{
					yield return "costPerShell[" + i + "] has no thingDef.";
					continue;
				}
				if (entry.count <= 0)
				{
					yield return "costPerShell[" + i + "] (" + entry.thingDef.defName + ") must have a positive count.";
				}
				if (!seen.Add(entry.thingDef))
				{
					yield return "Duplicate costPerShell entry for " + entry.thingDef.defName + ".";
				}
			}
		}
	}
}
