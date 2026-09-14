using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostAutomaticAirdropUtility
	{
		public static bool HasController(Outpost outpost)
		{
			if (outpost == null)
			{
				return false;
			}
			foreach (OutpostFacility facility in outpost.OperationalFacilities)
			{
				if (facility?.def != null && facility.def.automaticAirdropController)
				{
					return true;
				}
			}
			return false;
		}

		public static bool HasIntelligentController(Outpost outpost)
		{
			if (outpost == null) return false;
			foreach (OutpostFacility facility in outpost.OperationalFacilities)
			{
				if (facility?.def?.intelligentAirdropController == true) return true;
			}
			return false;
		}

		public static bool IsSelectableProducer(OutpostFacility facility)
		{
			return facility?.def != null && !facility.def.automaticAirdropController && !facility.def.Productions.NullOrEmpty();
		}

		public static bool TryDeliver(OutpostProductionContext context)
		{
			if (context?.Outpost == null || !IsSelectableProducer(context.Facility) || !context.Facility.autoAirdropEnabled
				|| !HasController(context.Outpost) || context.Products.NullOrEmpty())
			{
				return false;
			}
			Map map = Find.AnyPlayerHomeMap;
			if (map == null)
			{
				return false;
			}

			List<Thing> products = new List<Thing>();
			List<Thing> storedProducts = new List<Thing>();
			int totalCount = 0;
			ThingDef productDef = null;
			bool intelligent = HasIntelligentController(context.Outpost);
			int keepRemaining = intelligent
				? Mathf.Max(context.Facility.intelligentAirdropStockTarget - OutpostStockUtility.CountInStock(context.Outpost, context.Product), 0)
				: 0;
			for (int i = 0; i < context.Products.Count; i++)
			{
				Thing thing = context.Products[i];
				if (thing != null && !thing.Destroyed && thing.stackCount > 0)
				{
					if (keepRemaining > 0)
					{
						int keep = Mathf.Min(keepRemaining, thing.stackCount);
						if (keep == thing.stackCount)
						{
							storedProducts.Add(thing);
							keepRemaining -= keep;
							continue;
						}
						storedProducts.Add(thing.SplitOff(keep));
						keepRemaining -= keep;
					}
					products.Add(thing);
					totalCount += thing.stackCount;
					productDef = productDef ?? thing.def;
				}
			}
			if (storedProducts.Count > 0)
			{
				OutpostProductionUtility.StoreInOutpostInventory(context.Outpost, storedProducts);
			}
			if (products.Count == 0)
			{
				return true;
			}

			IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
			DropPodUtility.DropThingsNear(dropSpot, map, products, 110, canInstaDropDuringInit: false,
				leaveSlag: false, canRoofPunch: false, forbid: false, allowFogged: false, faction: Faction.OfPlayer);
			string productLabel = productDef?.LabelCap ?? "DreamsOutposts.Nothing".Translate();
			Messages.Message("DreamsOutposts.AutomaticAirdropDelivered".Translate(productLabel, totalCount),
				new TargetInfo(dropSpot, map), MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}
	}
}
