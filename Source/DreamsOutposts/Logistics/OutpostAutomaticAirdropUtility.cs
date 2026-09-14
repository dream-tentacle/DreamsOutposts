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
			int totalCount = 0;
			ThingDef productDef = null;
			for (int i = 0; i < context.Products.Count; i++)
			{
				Thing thing = context.Products[i];
				if (thing != null && !thing.Destroyed && thing.stackCount > 0)
				{
					products.Add(thing);
					totalCount += thing.stackCount;
					productDef = productDef ?? thing.def;
				}
			}
			if (products.Count == 0)
			{
				return false;
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
