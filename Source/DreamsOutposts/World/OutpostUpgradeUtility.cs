using System.Collections.Generic;
using System.Text;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostUpgradeUtility
	{
		public static bool CanUpgrade(Outpost outpost, out string reason)
		{
			reason = null;
			if (outpost == null)
			{
				reason = "DreamsOutposts.InvalidOutpost".Translate();
				return false;
			}
			if (outpost.IsMaxLevel)
			{
				reason = "DreamsOutposts.AlreadyHighestLevel".Translate(outpost.MaxLevel);
				return false;
			}
			OutpostLevelProperties nextLevel = outpost.NextLevelProperties;
			if (nextLevel == null)
			{
				reason = "DreamsOutposts.NoNextLevelDefinition".Translate(outpost.outpostTypeDef?.defName ?? "null", outpost.level + 1);
				return false;
			}
			if (outpost.DaysSinceEstablished < nextLevel.daysRequired)
			{
				reason = "DreamsOutposts.NeedsMoreDays".Translate((nextLevel.daysRequired - outpost.DaysSinceEstablished).ToString("0.#"));
				return false;
			}
			List<ThingDefCountClass> missing = new List<ThingDefCountClass>();
			if (!OutpostBuildUtility.CanAfford(outpost, nextLevel.cost, missing))
			{
				reason = OutpostBuildUtility.MissingLabel(missing);
				return false;
			}
			return true;
		}

		public static bool TryUpgrade(Outpost outpost)
		{
			if (!CanUpgrade(outpost, out var reason))
			{
				Log.Error("Tried to upgrade outpost " + (outpost?.Label ?? "null") + " but it cannot be upgraded: " + reason);
				return false;
			}
			OutpostLevelProperties nextLevel = outpost.NextLevelProperties;
			if (!OutpostBuildUtility.TryPay(outpost, nextLevel.cost, "the upgrade cost to level " + (outpost.level + 1)))
			{
				Log.Error("Outpost " + outpost.Label + " failed to pay for the upgrade to level " + (outpost.level + 1) + "; level unchanged.");
				return false;
			}
			outpost.SetLevel(outpost.level + 1);
			return true;
		}

		public static string UpgradeDescription(Outpost outpost)
		{
			if (outpost == null)
			{
				return string.Empty;
			}
			if (outpost.IsMaxLevel)
			{
				return "DreamsOutposts.MaxLevel".Translate();
			}
			OutpostLevelProperties nextLevel = outpost.NextLevelProperties;
			if (nextLevel == null)
			{
				return "DreamsOutposts.NoNextLevel".Translate();
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("DreamsOutposts.UpgradeLevelSummary".Translate(outpost.level + 1));
			stringBuilder.Append("DreamsOutposts.DaysSinceFounding".Translate(nextLevel.daysRequired.ToString("0.#")));
			if (!nextLevel.cost.NullOrEmpty())
			{
				stringBuilder.Append(", " + OutpostBuildUtility.CostLabel(nextLevel.cost));
			}
			stringBuilder.Append("\n" + "DreamsOutposts.SlotsChange".Translate(outpost.SlotCountForLevel, nextLevel.slotCount));
			if (CanUpgrade(outpost, out var reason))
			{
				stringBuilder.Append("\n\n" + "DreamsOutposts.ReadyToUpgrade".Translate());
			}
			else
			{
				stringBuilder.Append("\n\n" + "DreamsOutposts.NotYet".Translate(reason));
			}
			return stringBuilder.ToString();
		}
	}
}
