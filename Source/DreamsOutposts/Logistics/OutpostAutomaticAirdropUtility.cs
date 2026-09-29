using System;
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
			if (facility?.def == null || facility.def.automaticAirdropController) return false;
			if (!facility.def.Productions.NullOrEmpty()
				|| facility.def.GetCompProperties<OutpostFacilityCompProperties_Slaughterhouse>() != null) return true;
			foreach (OutpostProcessProperties process in facility.def.Processes)
				if (process?.Worker is OutpostProcessWorker_Taming) return true;
			return false;
		}

		public static bool TryDeliver(OutpostProductionContext context)
		{
			return context != null && TryDeliver(context.Outpost, context.Facility, context.Products);
		}

		// All entries must be newly produced, unowned things; false leaves them untouched for the caller to store.
		public static bool TryDeliver(Outpost outpost, OutpostFacility facility, List<Thing> output)
		{
			if (outpost == null || !IsSelectableProducer(facility) || !facility.autoAirdropEnabled
				|| !HasController(outpost) || output.NullOrEmpty())
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
			Dictionary<ThingDef, int> remainingByDef = new Dictionary<ThingDef, int>();
			Dictionary<ThingDef, int> deliveredByDef = new Dictionary<ThingDef, int>();
			bool intelligent = HasIntelligentController(outpost);
			for (int i = 0; i < output.Count; i++)
			{
				Thing thing = output[i];
				if (thing != null && !thing.Destroyed && thing.stackCount > 0)
				{
					int keepRemaining;
					if (!remainingByDef.TryGetValue(thing.def, out keepRemaining))
						keepRemaining = intelligent ? Mathf.Max(facility.intelligentAirdropStockTarget - CountStored(outpost, thing.def), 0) : 0;
					if (keepRemaining > 0)
					{
						int keep = Mathf.Min(keepRemaining, thing.stackCount);
						remainingByDef[thing.def] = keepRemaining - keep;
						if (keep == thing.stackCount)
						{
							storedProducts.Add(thing);
							continue;
						}
						storedProducts.Add(thing.SplitOff(keep));
					}
					else remainingByDef[thing.def] = 0;
					products.Add(thing);
					deliveredByDef.TryGetValue(thing.def, out int delivered);
					deliveredByDef[thing.def] = delivered + thing.stackCount;
				}
			}
			foreach (Thing thing in storedProducts)
			{
				bool stored = thing is Pawn pawn ? outpost.pawns.TryAdd(pawn) : outpost.inventory.TryAdd(thing);
				if (!stored) throw new InvalidOperationException("Could not store reserved airdrop output " + thing + ".");
			}
			if (products.Count == 0)
			{
				return true;
			}

			IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
			DropPodUtility.DropThingsNear(dropSpot, map, products, 110, canInstaDropDuringInit: false,
				leaveSlag: false, canRoofPunch: false, forbid: false, allowFogged: false, faction: Faction.OfPlayer);
			foreach (KeyValuePair<ThingDef, int> entry in deliveredByDef)
				Messages.Message("DreamsOutposts.AutomaticAirdropDelivered".Translate(entry.Key.LabelCap, entry.Value),
					new TargetInfo(dropSpot, map), MessageTypeDefOf.TaskCompletion, historical: false);
			return true;
		}

		private static int CountStored(Outpost outpost, ThingDef def)
		{
			int count = OutpostStockUtility.CountInStock(outpost, def);
			foreach (Pawn pawn in outpost.PawnsListForReading)
				if (pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.def == def) count += pawn.stackCount;
			return count;
		}
	}
}
