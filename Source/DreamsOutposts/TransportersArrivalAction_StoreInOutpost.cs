using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public class TransportersArrivalAction_StoreInOutpost : TransportersArrivalAction
	{
		private Outpost outpost;

		private static readonly List<Thing> tmpContainedThings = new List<Thing>();

		public override bool GeneratesMap => false;

		public TransportersArrivalAction_StoreInOutpost()
		{
		}

		public TransportersArrivalAction_StoreInOutpost(Outpost outpost)
		{
			this.outpost = outpost;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_References.Look(ref outpost, "outpost");
		}

		public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
		{
			FloatMenuAcceptanceReport report = base.StillValid(pods, destinationTile);
			if (!report.Accepted)
			{
				return report;
			}
			return CanStoreIn(outpost, destinationTile);
		}

		public static FloatMenuAcceptanceReport CanStoreIn(Outpost outpost, PlanetTile tile)
		{
			return outpost != null && outpost.Spawned && !outpost.Destroyed && outpost.Faction == Faction.OfPlayer && outpost.Tile == tile;
		}

		public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
		{
			if (outpost == null || outpost.Destroyed || outpost.inventory == null || outpost.pawns == null)
			{
				Log.Error("Transport pods arrived at an outpost that no longer exists, so their contents are lost.");
				return;
			}
			for (int i = 0; i < transporters.Count; i++)
			{
				ThingOwner container = transporters[i].innerContainer;
				if (container == null)
				{
					continue;
				}
				tmpContainedThings.Clear();
				tmpContainedThings.AddRange(container);
				for (int j = 0; j < tmpContainedThings.Count; j++)
				{
					Thing thing = tmpContainedThings[j];
					if (thing != null && !thing.Destroyed && thing.stackCount > 0)
					{
						if (thing is Pawn pawn)
						{
							StorePawn(container, pawn);
						}
						else
						{
							StoreItem(container, thing);
						}
					}
				}
			}
			tmpContainedThings.Clear();
			Messages.Message("DreamsOutposts.TransportPodsStoredInOutpost".Translate(outpost.LabelCap), outpost, MessageTypeDefOf.TaskCompletion);
		}

		private void StorePawn(ThingOwner container, Pawn pawn)
		{
			container.TryTransferToContainer(pawn, outpost.pawns, pawn.stackCount);
			if (container.Contains(pawn) && pawn.stackCount > 0)
			{
				Log.Error("Failed to move " + pawn?.ToString() + " from the transport pods into outpost " + outpost.Label + ".");
			}
			else
			{
				OutpostUtility.MovePawnIntoOutpost(outpost, pawn);
			}
		}

		private void StoreItem(ThingOwner container, Thing thing)
		{
			int stackCount = thing.stackCount;
			container.TryTransferToContainer(thing, outpost.inventory, stackCount);
			if (container.Contains(thing) && thing.stackCount > 0)
			{
				Log.Error("Moved only " + (stackCount - thing.stackCount) + " of " + stackCount + " " + thing.LabelNoCount + " from the transport pods into outpost " + outpost.Label + ".");
			}
		}
	}
}
