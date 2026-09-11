using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	public static class OutpostCaravanUtility
	{
		public static Command FormCaravanCommand(Outpost outpost)
		{
			return new Command_Action
			{
				defaultLabel = "CommandFormCaravan".Translate(),
				defaultDesc = "CommandFormCaravanDesc".Translate(),
				icon = FormCaravanComp.FormCaravanCommand,
				action = delegate
				{
					Find.WindowStack.Add(new Window_FormCaravanFromOutpost(outpost));
				}
			};
		}

		public static Command EnterOutpostCommand(Outpost outpost, Caravan caravan)
		{
			return new Command_Action
			{
				defaultLabel = "Enter outpost",
				defaultDesc = "Move every pawn and everything this caravan carries into the outpost, then disband the caravan.",
				icon = SettleUtility.CreateCampCommandTex,
				action = delegate
				{
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					TryEnterOutpost(caravan, outpost);
					if (caravan.Destroyed)
					{
						Find.WorldSelector.ClearSelection();
						Find.WorldSelector.Select(outpost, playSound: false);
					}
				}
			};
		}

		public static void AddToTransferables(Thing thing, List<TransferableOneWay> transferables)
		{
			TransferableOneWay transferable = TransferableUtility.TransferableMatching(thing, transferables, TransferAsOneMode.Normal);
			if (transferable == null)
			{
				transferable = new TransferableOneWay();
				transferables.Add(transferable);
			}
			if (transferable.things.Contains(thing))
			{
				Log.Error("Tried to add the same thing twice to TransferableOneWay: " + thing);
			}
			else
			{
				transferable.things.Add(thing);
			}
		}

		public static void FillTransferables(Outpost outpost, List<TransferableOneWay> transferables)
		{
			List<Pawn> pawns = outpost.PawnsListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				AddToTransferables(pawns[i], transferables);
			}
			for (int j = 0; j < pawns.Count; j++)
			{
				Pawn_InventoryTracker inventory = pawns[j].inventory;
				if (inventory != null)
				{
					ThingOwner<Thing> container = inventory.innerContainer;
					for (int k = 0; k < container.Count; k++)
					{
						AddToTransferables(container[k], transferables);
					}
				}
			}
			List<Thing> stock = outpost.InventoryItems;
			for (int l = 0; l < stock.Count; l++)
			{
				AddToTransferables(stock[l], transferables);
			}
		}

		public static bool TryFormCaravan(Outpost outpost, List<TransferableOneWay> transferables, out Caravan created)
		{
			created = null;
			if (outpost == null || outpost.Destroyed)
			{
				return false;
			}
			List<Pawn> pawnsToSend = TransferableUtility.GetPawnsFromTransferables(transferables);
			if (!pawnsToSend.Any((Pawn p) => CaravanUtility.IsOwner(p, Faction.OfPlayer) && !p.Downed))
			{
				if (ModsConfig.IdeologyActive)
				{
					Messages.Message("CaravanMustHaveAtLeastOneNonSlaveColonist".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				}
				else
				{
					Messages.Message("CaravanMustHaveAtLeastOneColonist".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				}
				return false;
			}
			for (int i = 0; i < pawnsToSend.Count; i++)
			{
				OutpostUtility.MovePawnInventoryIntoOutpost(outpost, pawnsToSend[i]);
			}
			for (int i2 = 0; i2 < pawnsToSend.Count; i2++)
			{
				outpost.pawns.Remove(pawnsToSend[i2]);
			}
			Caravan caravan = CaravanMaker.MakeCaravan(pawnsToSend, Faction.OfPlayer, outpost.Tile, addToWorldPawnsIfNotAlready: true);
			transferables.RemoveAll((TransferableOneWay t) => t.AnyThing is Pawn);
			for (int i3 = 0; i3 < transferables.Count; i3++)
			{
				TransferableOneWay transferable = transferables[i3];
				if (transferable.CountToTransfer <= 0)
				{
					continue;
				}
				TransferableUtility.Transfer(transferable.things, transferable.CountToTransfer, delegate(Thing piece, IThingHolder originalHolder)
				{
					Pawn pawn = CaravanInventoryUtility.FindPawnToMoveInventoryTo(piece, caravan.PawnsListForReading, null);
					if (pawn == null)
					{
						Log.Error("Could not find a pawn in the new caravan to carry " + piece?.ToString() + "; returning it to outpost " + outpost.Label + ".");
						if (!outpost.inventory.TryAdd(piece))
						{
							Log.Error("Failed to return " + piece?.ToString() + " to outpost " + outpost.Label + ".");
						}
					}
					else
					{
						pawn.inventory.TryAddAndUnforbid(piece);
					}
				});
			}
			created = caravan;
			return true;
		}

		public static bool TryEnterOutpost(Caravan caravan, Outpost outpost)
		{
			if (caravan == null || outpost == null || outpost.Destroyed)
			{
				return false;
			}
			OutpostUtility.TransferCaravanItemsTo(caravan, outpost);
			for (int i = caravan.PawnsListForReading.Count - 1; i >= 0; i--)
			{
				Pawn pawn = caravan.PawnsListForReading[i];
				caravan.RemovePawn(pawn);
				if (!OutpostUtility.MovePawnIntoOutpost(outpost, pawn))
				{
					Log.Error("Failed to move " + pawn?.ToString() + " from caravan " + caravan.Label + " into outpost " + outpost.Label + "; putting it back into the caravan.");
					caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: false);
				}
			}
			if (caravan.PawnsListForReading.Count == 0)
			{
				caravan.Destroy();
			}
			return true;
		}
	}
}
