using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityDef : Def
	{
		public bool installableAsExtension = true;

		/// <summary>
		/// 新 UI 的图标名（对应 UiIcon 枚举，例如 Mine / Farm / Store / Turret）。
		/// 留空时按 defName / label / 产物的关键字自动推断。以后有 PNG 资源时可以另外指定贴图。
		/// </summary>
		public string uiIcon;

		public List<OutpostTypeDef> allowedOutpostTypes = new List<OutpostTypeDef>();

		public List<OutpostTypeDef> disallowedOutpostTypes = new List<OutpostTypeDef>();

		public bool whitelistOnly;

		public int maxPerOutpost;

		public List<ResearchProjectDef> researchPrerequisites = new List<ResearchProjectDef>();

		/// <summary>
		/// 安装这个扩建设施所需的据点最低等级。1 表示任何等级都可以安装。
		/// </summary>
		public int minOutpostLevel = 1;

		public List<OutpostProductionProperties> productions = new List<OutpostProductionProperties>();

		public List<ThingDefCountClass> buildCost = new List<ThingDefCountClass>();

		private static readonly List<ThingDefCountClass> NoCost = new List<ThingDefCountClass>();

		public List<OutpostProductionModifier> productionModifiers = new List<OutpostProductionModifier>();

		public List<OutpostEventCategoryModifier> eventCategoryModifiers = new List<OutpostEventCategoryModifier>();

		public OutpostBombardmentProperties bombardment;

		/// <summary>
		/// 训练属性：安装后据点内的殖民者会持续获得经验。留空表示这个设施不训练任何人。
		/// </summary>
		public OutpostTrainingProperties training;

		/// <summary>
		/// 这个设施已安装时给据点提供的固定防卫值。
		/// 核心设施和扩展设施都通过 Outpost.Facilities 统一参与 OutpostDefenseUtility 的计算。
		/// </summary>
		public float defense;

		public int bombardmentShellBonus;

		public bool IsResearchUnlocked
		{
			get
			{
				if (researchPrerequisites.NullOrEmpty())
				{
					return true;
				}
				for (int i = 0; i < researchPrerequisites.Count; i++)
				{
					ResearchProjectDef research = researchPrerequisites[i];
					if (research != null && !research.IsFinished)
					{
						return false;
					}
				}
				return true;
			}
		}

		public ResearchProjectDef FirstMissingResearch
		{
			get
			{
				if (researchPrerequisites.NullOrEmpty())
				{
					return null;
				}
				for (int i = 0; i < researchPrerequisites.Count; i++)
				{
					ResearchProjectDef research = researchPrerequisites[i];
					if (research != null && !research.IsFinished)
					{
						return research;
					}
				}
				return null;
			}
		}

		public List<ThingDefCountClass> BuildCost => buildCost.NullOrEmpty() ? NoCost : buildCost;

		public bool HasBuildCost => !buildCost.NullOrEmpty();

		public bool IsProducer => !productions.NullOrEmpty();

		public bool IsProductionModifier => !productionModifiers.NullOrEmpty();

		public bool IsAllowedIn(OutpostTypeDef outpostTypeDef)
		{
			return OutpostTypeRestrictionUtility.IsAllowedIn(allowedOutpostTypes, disallowedOutpostTypes, whitelistOnly, outpostTypeDef);
		}

		public bool IsLevelRequirementMetBy(int outpostLevel)
		{
			return outpostLevel >= minOutpostLevel;
		}

		public AcceptanceReport MeetsLevelRequirement(Outpost outpost)
		{
			if (outpost == null || IsLevelRequirementMetBy(outpost.level))
			{
				return AcceptanceReport.WasAccepted;
			}
			return new AcceptanceReport("DreamsOutposts.InstallFail.LevelTooLow".Translate(outpost.level, minOutpostLevel));
		}

		public void GetProductionModifiersFor(OutpostProductionProperties production, out float offsetSum, out float factorProduct)
		{
			offsetSum = 0f;
			factorProduct = 1f;
			if (productionModifiers == null || production == null)
			{
				return;
			}
			for (int i = 0; i < productionModifiers.Count; i++)
			{
				OutpostProductionModifier modifier = productionModifiers[i];
				if (modifier != null && modifier.Matches(production))
				{
					offsetSum += modifier.offset;
					factorProduct *= modifier.factor;
				}
			}
		}

		public OutpostProductionProperties GetProduction(string id)
		{
			if (productions == null || string.IsNullOrEmpty(id))
			{
				return null;
			}
			for (int i = 0; i < productions.Count; i++)
			{
				OutpostProductionProperties production = productions[i];
				if (production != null && production.id == id)
				{
					return production;
				}
			}
			return null;
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string item in base.ConfigErrors())
			{
				yield return item;
			}
			foreach (string item2 in OutpostTypeRestrictionErrors())
			{
				yield return item2;
			}
			foreach (string item3 in BuildCostErrors())
			{
				yield return item3;
			}
			foreach (string item4 in ProductionModifierErrors())
			{
				yield return item4;
			}
			foreach (string item5 in BombardmentErrors())
			{
				yield return item5;
			}
			if (training != null)
			{
				foreach (string item7 in training.ConfigErrors())
				{
					yield return "training: " + item7;
				}
			}
			if (maxPerOutpost < 0)
			{
				yield return "maxPerOutpost must not be negative; use 0 for unlimited.";
			}
			if (minOutpostLevel < 1)
			{
				yield return "minOutpostLevel must be at least 1; level 1 is the starting state of every outpost.";
			}
			foreach (string item6 in MinOutpostLevelErrors())
			{
				yield return item6;
			}
			if (defense < 0f)
			{
				yield return "defense must not be negative.";
			}
			for (int i = 0; i < (researchPrerequisites?.Count ?? 0); i++)
			{
				if (researchPrerequisites[i] == null)
				{
					yield return "researchPrerequisites[" + i + "] is null.";
				}
			}
			if (productions == null)
			{
				yield return "productions must not be null.";
				yield break;
			}
			HashSet<string> ids = new HashSet<string>();
			for (int j = 0; j < productions.Count; j++)
			{
				OutpostProductionProperties production = productions[j];
				if (production == null)
				{
					yield return "productions[" + j + "] is null.";
					continue;
				}
				if (!ids.Add(production.id ?? string.Empty))
				{
					yield return "Duplicate production id: " + production.id;
				}
				using (IEnumerator<string> enumerator6 = production.ConfigErrors().GetEnumerator())
				{
					while (enumerator6.MoveNext())
					{
						yield return string.Concat(str3: enumerator6.Current, str0: "Production ", str1: production.id, str2: ": ");
					}
				}
			}
		}

		private IEnumerable<string> OutpostTypeRestrictionErrors()
		{
			for (int i = 0; i < (allowedOutpostTypes?.Count ?? 0); i++)
			{
				OutpostTypeDef allowed = allowedOutpostTypes[i];
				if (allowed == null)
				{
					yield return "allowedOutpostTypes[" + i + "] is null.";
				}
				else if (disallowedOutpostTypes != null && disallowedOutpostTypes.Contains(allowed))
				{
					yield return "Outpost type " + allowed.defName + " is in both allowedOutpostTypes and disallowedOutpostTypes; the whitelist wins, but the config is contradictory.";
				}
			}
			for (int j = 0; j < (disallowedOutpostTypes?.Count ?? 0); j++)
			{
				if (disallowedOutpostTypes[j] == null)
				{
					yield return "disallowedOutpostTypes[" + j + "] is null.";
				}
			}
			if (installableAsExtension && whitelistOnly && allowedOutpostTypes.NullOrEmpty())
			{
				yield return "whitelistOnly is true but allowedOutpostTypes is empty, so this installable facility can never be installed in any outpost type.";
			}
		}

		private IEnumerable<string> MinOutpostLevelErrors()
		{
			List<OutpostTypeDef> outpostTypes = DefDatabase<OutpostTypeDef>.AllDefsListForReading;
			for (int i = 0; i < outpostTypes.Count; i++)
			{
				OutpostTypeDef outpostType = outpostTypes[i];
				if (outpostType != null && outpostType.MaxLevel < minOutpostLevel && IsAllowedIn(outpostType))
				{
					yield return "minOutpostLevel " + minOutpostLevel + " is above the maximum level (" + outpostType.MaxLevel + ") of outpost type " + outpostType.defName + ", which allows this facility, so it can never be installed there.";
				}
			}
		}

		private IEnumerable<string> BuildCostErrors()
		{
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < (buildCost?.Count ?? 0); i++)
			{
				ThingDefCountClass cost = buildCost[i];
				if (cost == null)
				{
					yield return "buildCost[" + i + "] is null.";
					continue;
				}
				if (cost.thingDef == null)
				{
					yield return "buildCost[" + i + "] has no thingDef.";
					continue;
				}
				if (cost.count <= 0)
				{
					yield return "buildCost[" + i + "] (" + cost.thingDef.defName + ") must have a positive count.";
				}
				if (!seen.Add(cost.thingDef))
				{
					yield return "Duplicate buildCost entry for " + cost.thingDef.defName + ".";
				}
			}
		}

		private IEnumerable<string> BombardmentErrors()
		{
			if (bombardment != null)
			{
				foreach (string error in bombardment.ConfigErrors())
				{
					yield return "bombardment: " + error;
				}
			}
			if (bombardmentShellBonus < 0)
			{
				yield return "bombardmentShellBonus must not be negative.";
			}
		}

		private IEnumerable<string> ProductionModifierErrors()
		{
			for (int i = 0; i < (productionModifiers?.Count ?? 0); i++)
			{
				OutpostProductionModifier modifier = productionModifiers[i];
				if (modifier == null)
				{
					yield return "productionModifiers[" + i + "] is null.";
					continue;
				}
				if (modifier.productionTag != null && string.IsNullOrEmpty(modifier.NormalizedTag))
				{
					yield return "productionModifiers[" + i + "] has an empty productionTag; leave it out entirely for a global modifier.";
				}
				using (IEnumerator<string> enumerator = modifier.ConfigErrors().GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						yield return string.Concat(str3: enumerator.Current, str0: "productionModifiers[", str1: i.ToString(), str2: "]: ");
					}
				}
			}
		}
	}
}
