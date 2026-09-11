using System.Collections.Generic;
using System.Text;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostBuildUtility
	{
		public static bool CanAfford(Outpost outpost, List<ThingDefCountClass> cost, List<ThingDefCountClass> missing = null)
		{
			missing?.Clear();
			if (outpost == null)
			{
				return false;
			}
			if (cost.NullOrEmpty())
			{
				return true;
			}
			bool affordable = true;
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass item = cost[i];
				if (item?.thingDef != null && item.count > 0 && !AlreadyChecked(cost, i))
				{
					int shortfall = RequiredCount(cost, i) - OutpostStockUtility.CountInStock(outpost, item.thingDef);
					if (shortfall > 0)
					{
						affordable = false;
						missing?.Add(new ThingDefCountClass(item.thingDef, shortfall));
					}
				}
			}
			return affordable;
		}

		public static bool CanAfford(Outpost outpost, OutpostFacilityDef def, List<ThingDefCountClass> missing = null)
		{
			return CanAfford(outpost, def?.BuildCost, missing);
		}

		public static bool TryPay(Outpost outpost, List<ThingDefCountClass> cost, string labelForLog)
		{
			if (outpost == null)
			{
				return false;
			}
			if (cost.NullOrEmpty())
			{
				return true;
			}
			bool paid = true;
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass item = cost[i];
				if (item?.thingDef != null && item.count > 0 && !AlreadyChecked(cost, i))
				{
					int required = RequiredCount(cost, i);
					int removed = OutpostStockUtility.TakeFromStock(outpost, item.thingDef, required);
					if (removed < required)
					{
						Log.Error("Failed to pay " + labelForLog + " in outpost " + outpost.Label + ": removed only " + removed + " of " + required + " " + item.thingDef.defName + ".");
						paid = false;
					}
				}
			}
			return paid;
		}

		public static bool TryPay(Outpost outpost, OutpostFacilityDef def)
		{
			return TryPay(outpost, def?.BuildCost, "the build cost of " + (def?.defName ?? "null"));
		}

		private static int RequiredCount(List<ThingDefCountClass> cost, int index)
		{
			ThingDef thingDef = cost[index].thingDef;
			int required = 0;
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass other = cost[i];
				if (other?.thingDef == thingDef && other.count > 0)
				{
					required += other.count;
				}
			}
			return required;
		}

		private static bool AlreadyChecked(List<ThingDefCountClass> cost, int index)
		{
			ThingDef thingDef = cost[index].thingDef;
			for (int i = 0; i < index; i++)
			{
				if (cost[i]?.thingDef == thingDef)
				{
					return true;
				}
			}
			return false;
		}

		public static string CostLabel(List<ThingDefCountClass> cost)
		{
			if (cost.NullOrEmpty())
			{
				return "none";
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass item = cost[i];
				if (item != null && item.thingDef != null && item.count > 0)
				{
					AppendItem(stringBuilder, item.thingDef, item.count);
				}
			}
			return (stringBuilder.Length == 0) ? "none" : stringBuilder.ToString();
		}

		public static string CostLabel(OutpostFacilityDef def)
		{
			return CostLabel(def?.BuildCost);
		}

		public static List<ThingDefCountClass> RefundCost(OutpostFacilityDef def)
		{
			List<ThingDefCountClass> refund = new List<ThingDefCountClass>();
			List<ThingDefCountClass> cost = def?.BuildCost;
			if (cost.NullOrEmpty())
			{
				return refund;
			}
			for (int i = 0; i < cost.Count; i++)
			{
				ThingDefCountClass item = cost[i];
				if (item?.thingDef != null && item.count > 0 && !AlreadyChecked(cost, i))
				{
					int half = RequiredCount(cost, i) / 2;
					if (half > 0)
					{
						refund.Add(new ThingDefCountClass(item.thingDef, half));
					}
				}
			}
			return refund;
		}

		public static string RefundLabel(OutpostFacilityDef def)
		{
			return CostLabel(RefundCost(def));
		}

		public static void Refund(Outpost outpost, OutpostFacilityDef def)
		{
			List<ThingDefCountClass> refund = RefundCost(def);
			for (int i = 0; i < refund.Count; i++)
			{
				ThingDefCountClass item = refund[i];
				OutpostStockUtility.AddToStock(outpost, item.thingDef, item.count);
			}
		}

		public static string MissingLabel(List<ThingDefCountClass> missing)
		{
			if (missing.NullOrEmpty())
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < missing.Count; i++)
			{
				ThingDefCountClass item = missing[i];
				if (item != null && item.thingDef != null && item.count > 0)
				{
					AppendItem(stringBuilder, item.thingDef, item.count);
				}
			}
			return (stringBuilder.Length == 0) ? string.Empty : ("missing " + stringBuilder.ToString());
		}

		private static void AppendItem(StringBuilder stringBuilder, ThingDef thingDef, int count)
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(thingDef.label + " x" + count);
		}
	}
}
