using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostBombardmentProperties
	{
		/// <summary>
		/// 默认炮弹。玩家没有用右键切换过炮弹时使用它；切换过的据点会把自己的选择记在 Outpost.selectedShellDef 上。
		/// </summary>
		public ThingDef shellDef;

		/// <summary>
		/// 可选炮弹的 ThingCategoryDef 名称。原版迫击炮能装填的炮弹就是 MortarShells 这一分类。
		/// </summary>
		public string shellCategory = "MortarShells";

		public int shellsPerStrike = 4;

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

		private ThingCategoryDef cachedShellCategory;

		private bool shellCategoryLookedUp;

		public int CooldownTicks => Mathf.Max(0, Mathf.RoundToInt(cooldownHours * 2500f));

		/// <summary>可选炮弹的分类。找不到时返回 null，调用方会退化成「不按分类过滤」。</summary>
		public ThingCategoryDef ShellCategory
		{
			get
			{
				if (!shellCategoryLookedUp)
				{
					cachedShellCategory = shellCategory.NullOrEmpty() ? null : DefDatabase<ThingCategoryDef>.GetNamedSilentFail(shellCategory);
					shellCategoryLookedUp = true;
				}
				return cachedShellCategory;
			}
		}

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

		public IEnumerable<string> ConfigErrors()
		{
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
			if (!shellCategory.NullOrEmpty() && DefDatabase<ThingCategoryDef>.GetNamedSilentFail(shellCategory) == null)
			{
				yield return "shellCategory " + shellCategory + " is not a ThingCategoryDef; every shell would be selectable.";
			}
		}
	}
}
