using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostStockUtility
	{
		public static int CountInStock(Outpost outpost, ThingDef thingDef)
		{
			if (outpost == null || thingDef == null)
			{
				return 0;
			}
			List<Thing> items = outpost.InventoryItems;
			int count = 0;
			for (int i = 0; i < items.Count; i++)
			{
				Thing thing = items[i];
				if (thing != null && !thing.Destroyed && thing.def == thingDef)
				{
					count += thing.stackCount;
				}
			}
			return count;
		}

		public static int TakeFromStock(Outpost outpost, ThingDef thingDef, int count)
		{
			if (outpost?.inventory == null || thingDef == null || count <= 0)
			{
				return 0;
			}
			List<Thing> items = outpost.inventory.InnerListForReading;
			int removed = 0;
			int i = items.Count - 1;
			while (i >= 0 && removed < count)
			{
				Thing thing = items[i];
				if (thing != null && !thing.Destroyed && thing.def == thingDef)
				{
					int take = Mathf.Min(thing.stackCount, count - removed);
					Thing taken = outpost.inventory.Take(thing, take);
					if (taken == null)
					{
						Log.Error("[DreamsOutposts] Failed to take " + take + " " + thingDef.defName + " from outpost " + outpost.Label + "; stopping.");
						break;
					}
					taken.Destroy();
					removed += take;
				}
				i--;
			}
			return removed;
		}

		public static int AddToStock(Outpost outpost, ThingDef thingDef, int count)
		{
			if (outpost?.inventory == null || thingDef == null || count <= 0)
			{
				return 0;
			}
			int stackLimit = Mathf.Max(thingDef.stackLimit, 1);
			int added = 0;
			int remaining = count;
			while (remaining > 0)
			{
				Thing thing = ThingMaker.MakeThing(thingDef, thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null);
				thing.stackCount = Mathf.Min(remaining, stackLimit);
				remaining -= thing.stackCount;
				if (outpost.inventory.TryAdd(thing))
				{
					added += thing.stackCount;
					continue;
				}
				Log.Error("[DreamsOutposts] Failed to add " + thing.stackCount + " " + thingDef.defName + " to outpost " + outpost.Label + "'s inventory; the stack is destroyed.");
				thing.Destroy();
				break;
			}
			return added;
		}

		public static int MaxCraftableUnits(Outpost outpost, List<ThingDefCountClass> costPerUnit)
		{
			if (outpost == null || costPerUnit.NullOrEmpty())
			{
				return int.MaxValue;
			}
			int maxUnits = int.MaxValue;
			for (int i = 0; i < costPerUnit.Count; i++)
			{
				ThingDefCountClass entry = costPerUnit[i];
				if (entry?.thingDef == null || entry.count <= 0)
				{
					continue;
				}
				int requiredPerUnit = 0;
				for (int j = 0; j < costPerUnit.Count; j++)
				{
					ThingDefCountClass other = costPerUnit[j];
					if (other?.thingDef == entry.thingDef && other.count > 0)
					{
						requiredPerUnit += other.count;
					}
				}
				if (requiredPerUnit > 0)
				{
					int units = CountInStock(outpost, entry.thingDef) / requiredPerUnit;
					if (units < maxUnits)
					{
						maxUnits = units;
					}
				}
			}
			return maxUnits;
		}
	}
}
