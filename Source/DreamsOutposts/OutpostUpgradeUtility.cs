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
				reason = "Invalid outpost.";
				return false;
			}
			if (outpost.IsMaxLevel)
			{
				reason = "Already at the highest level (" + outpost.MaxLevel + ").";
				return false;
			}
			OutpostLevelProperties nextLevel = outpost.NextLevelProperties;
			if (nextLevel == null)
			{
				reason = "Outpost type " + (outpost.outpostTypeDef?.defName ?? "null") + " has no definition for level " + (outpost.level + 1) + ".";
				return false;
			}
			if (outpost.DaysSinceEstablished < nextLevel.daysRequired)
			{
				reason = "Needs " + (nextLevel.daysRequired - outpost.DaysSinceEstablished).ToString("0.#") + " more days since founding.";
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
				return "Max level.";
			}
			OutpostLevelProperties nextLevel = outpost.NextLevelProperties;
			if (nextLevel == null)
			{
				return "No definition for the next level.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Level " + (outpost.level + 1) + ": ");
			stringBuilder.Append(nextLevel.daysRequired.ToString("0.#") + " days since founding");
			if (!nextLevel.cost.NullOrEmpty())
			{
				stringBuilder.Append(", " + OutpostBuildUtility.CostLabel(nextLevel.cost));
			}
			stringBuilder.Append("\nSlots: " + outpost.SlotCountForLevel + " -> " + nextLevel.slotCount);
			if (CanUpgrade(outpost, out var reason))
			{
				stringBuilder.Append("\n\nReady to upgrade.");
			}
			else
			{
				stringBuilder.Append("\n\nNot yet: " + reason);
			}
			return stringBuilder.ToString();
		}
	}
}
