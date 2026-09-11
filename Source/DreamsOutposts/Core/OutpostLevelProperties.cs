using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostLevelProperties
	{
		public int slotCount;

		public float daysRequired;

		public List<ThingDefCountClass> cost = new List<ThingDefCountClass>();

		public int DaysRequiredTicks => (int)(daysRequired * 60000f);

		public IEnumerable<string> ConfigErrors(int levelIndex)
		{
			string prefix = "levels[" + levelIndex + "]: ";
			if (slotCount < 0)
			{
				yield return prefix + "slotCount must not be negative.";
			}
			if (float.IsNaN(daysRequired) || float.IsInfinity(daysRequired))
			{
				yield return prefix + "daysRequired must be a finite number.";
			}
			else if (daysRequired < 0f)
			{
				yield return prefix + "daysRequired must not be negative.";
			}
			if (levelIndex == 0 && (!cost.NullOrEmpty() || daysRequired != 0f))
			{
				yield return prefix + "level 1 is the starting state, so its cost and daysRequired are ignored; keep them empty/zero.";
			}
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < (cost?.Count ?? 0); i++)
			{
				ThingDefCountClass entry = cost[i];
				if (entry == null)
				{
					yield return prefix + "cost[" + i + "] is null.";
					continue;
				}
				if (entry.thingDef == null)
				{
					yield return prefix + "cost[" + i + "] has no thingDef.";
					continue;
				}
				if (entry.count <= 0)
				{
					yield return prefix + "cost[" + i + "] (" + entry.thingDef.defName + ") must have a positive count.";
				}
				if (!seen.Add(entry.thingDef))
				{
					yield return prefix + "duplicate cost entry for " + entry.thingDef.defName + ".";
				}
			}
		}
	}
}
