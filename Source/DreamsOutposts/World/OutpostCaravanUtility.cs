using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	public static class OutpostCaravanUtility
	{
		/// <summary>据点组建远行队时最多能编入的载具数量。多台载具混编（尤其飞机与地面车）会被载具拓展拒绝，因此硬性限制为一台。</summary>
		public const int MaxVehiclesPerCaravan = 1;

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
				defaultLabel = "DreamsOutposts.EnterOutpost".Translate(),
				defaultDesc = "DreamsOutposts.EnterOutpostDesc".Translate(),
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
				Log.Error("[DreamsOutposts] Tried to add the same thing twice to TransferableOneWay: " + thing);
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
			// 载具必须一辆一个 transferable：载具拓展的卡片组件与座位窗口都只认 transferable.AnyThing，
			// 两辆同型号载具一旦被合并进同一个 transferable，第二辆就没法分配座位。
			List<Pawn> vehicles = outpost.VehiclesListForReading;
			for (int i = 0; i < vehicles.Count; i++)
			{
				if (vehicles[i] == null) continue;
				TransferableOneWay vehicleTransferable = new TransferableOneWay();
				vehicleTransferable.things.Add(vehicles[i]);
				transferables.Add(vehicleTransferable);
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
			// 载具拓展：选中的载具必须在座位窗口里分配够操作乘员，否则拒绝，要求玩家先去载具页分配座位。
			List<Pawn> vehiclesToSend = CollectSelectedVehicles(transferables);
			// 一次只允许一台载具：多台载具（尤其飞机与地面车）混编会被载具拓展的合并/取回逻辑拒绝，
			// 状态很脆，这里直接拦在提交口。
			if (vehiclesToSend.Count > MaxVehiclesPerCaravan)
			{
				Messages.Message("DreamsOutposts.OneVehiclePerCaravan".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			bool formingVehicleCaravan = vehiclesToSend.Count > 0 && VehicleCaravanCompat.Available;
			if (formingVehicleCaravan && !VehicleCaravanCompat.ValidateCrew(vehiclesToSend, out string crewReason))
			{
				Messages.Message(crewReason, MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			// 已经坐进载具的乘员不再作为远行队成员单独加入，VehicleCaravan 会把车里的算作车内成员。
			List<Pawn> caravanMembers = new List<Pawn>();
			for (int i = 0; i < pawnsToSend.Count; i++)
			{
				if (!formingVehicleCaravan || !VehicleCaravanCompat.IsAboard(pawnsToSend[i]))
				{
					caravanMembers.Add(pawnsToSend[i]);
				}
			}
			List<Pawn> boarded = new List<Pawn>();
			List<Pawn> detached = new List<Pawn>();
			Caravan caravan = null;
			try
			{
				if (formingVehicleCaravan && !VehicleCaravanCompat.TryBoardAssignedPawns(pawnsToSend, vehiclesToSend, boarded, out string boardingReason))
				{
					Messages.Message(boardingReason, MessageTypeDefOf.RejectInput, historical: false);
					ReturnToOutpost(outpost, boarded, detached);
					return false;
				}
				for (int i = 0; i < pawnsToSend.Count; i++)
				{
					Pawn pawn = pawnsToSend[i];
					// 载具的货舱跟着载具走；普通小人的随身物品先倒回据点，再按玩家在物品页的选择重新装车。
					if (!VehicleCaravanCompat.IsVehicle(pawn))
					{
						OutpostUtility.MovePawnInventoryIntoOutpost(outpost, pawn);
					}
					if (outpost.pawns.Remove(pawn) || outpost.vehicles.Remove(pawn))
					{
						detached.Add(pawn);
						outpost.RequestUpdate();
					}
				}
				// 列表里只要含 VehiclePawn，载具拓展的前缀补丁就会把它变成 VehicleCaravan。
				caravan = CaravanMaker.MakeCaravan(caravanMembers, Faction.OfPlayer, outpost.Tile, addToWorldPawnsIfNotAlready: true);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] Failed to form a caravan from outpost " + outpost.Label + ": " + ex);
				Messages.Message("DreamsOutposts.CaravanFormFailedRolledBack".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				ReturnToOutpost(outpost, boarded, detached);
				return false;
			}
			if (caravan == null)
			{
				Log.Error("[DreamsOutposts] CaravanMaker returned no caravan for outpost " + outpost.Label + "; everyone was returned to the outpost.");
				Messages.Message("DreamsOutposts.CaravanFormFailedRolledBack".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				ReturnToOutpost(outpost, boarded, detached);
				return false;
			}
			if (formingVehicleCaravan)
			{
				// 座位分配已经被登车消费掉，必须清掉，否则全局静态分配会污染后面的质量、可见度与界面判定。
				VehicleCaravanCompat.ClearAssignments(vehiclesToSend);
			}
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
						Log.Error("[DreamsOutposts] Could not find a pawn in the new caravan to carry " + piece?.ToString() + "; returning it to outpost " + outpost.Label + ".");
						if (!outpost.inventory.TryAdd(piece))
						{
							Log.Error("[DreamsOutposts] Failed to return " + piece?.ToString() + " to outpost " + outpost.Label + ".");
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

		/// <summary>收集本次被选中的载具。载具拓展会把同型号载具合进一个 transferable，所以按 CountToTransfer 逐辆取。</summary>
		public static List<Pawn> CollectSelectedVehicles(List<TransferableOneWay> transferables)
		{
			List<Pawn> vehicles = new List<Pawn>();
			if (transferables == null)
			{
				return vehicles;
			}
			for (int i = 0; i < transferables.Count; i++)
			{
				TransferableOneWay transferable = transferables[i];
				if (transferable == null || transferable.CountToTransfer <= 0 || !(transferable.AnyThing is Pawn))
				{
					continue;
				}
				int remaining = transferable.CountToTransfer;
				for (int j = 0; j < transferable.things.Count && remaining > 0; j++)
				{
					Pawn pawn = transferable.things[j] as Pawn;
					if (pawn == null || !VehicleCaravanCompat.IsVehicle(pawn))
					{
						continue;
					}
					vehicles.Add(pawn);
					remaining--;
				}
			}
			return vehicles;
		}

		/// <summary>组建失败时把已经登车/已经搬出据点的小人和载具放回据点。</summary>
		private static void ReturnToOutpost(Outpost outpost, List<Pawn> boarded, List<Pawn> detached)
		{
			if (outpost == null)
			{
				return;
			}
			for (int i = boarded.Count - 1; i >= 0; i--)
			{
				Pawn pawn = boarded[i];
				if (pawn.GetCaravan() != null)
				{
					continue;
				}
				Pawn carrier;
				if (!VehicleCaravanCompat.TryUnboard(pawn, out carrier) && VehicleCaravanCompat.IsAboard(pawn))
				{
					Log.Error("[DreamsOutposts] Failed to unboard " + pawn + " from " + (carrier?.ToString() ?? "its vehicle") + " after the caravan could not be formed.");
					continue;
				}
				if (!outpost.pawns.Contains(pawn) && !outpost.pawns.TryAdd(pawn))
				{
					Log.Error("[DreamsOutposts] Failed to return " + pawn + " to outpost " + outpost.Label + " after the caravan could not be formed.");
				}
			}
			for (int i = detached.Count - 1; i >= 0; i--)
			{
				Pawn pawn = detached[i];
				if (pawn.GetCaravan() != null)
				{
					continue;
				}
				ThingOwner<Pawn> target = VehicleCaravanCompat.IsVehicle(pawn) ? outpost.vehicles : outpost.pawns;
				if (target == null)
				{
					Log.Error("[DreamsOutposts] Outpost " + outpost.Label + " has no container for " + pawn + ".");
					continue;
				}
				if (!target.Contains(pawn) && !target.TryAdd(pawn))
				{
					Log.Error("[DreamsOutposts] Failed to return " + pawn + " to outpost " + outpost.Label + " after the caravan could not be formed.");
				}
			}
			outpost.RequestUpdate();
		}

		public static bool TryEnterOutpost(Caravan caravan, Outpost outpost)
		{
			if (caravan == null || outpost == null || outpost.Destroyed)
			{
				return false;
			}
			OutpostUtility.TransferCaravanItemsTo(caravan, outpost);
			MoveCaravanPawnsToOutpost(caravan, outpost);
			if (caravan.PawnsListForReading.Count == 0)
			{
				caravan.Destroy();
			}
			return true;
		}

		public static void MoveCaravanPawnsToOutpost(Caravan caravan, Outpost outpost)
		{
			if (VehicleFrameworkCompatibility.Enabled && caravan.GetType().FullName == "Vehicles.World.VehicleCaravan")
			{
				VehicleFrameworkCompatibility.MoveCaravanPawnsToOutpost(caravan, outpost);
				return;
			}
			for (int i = caravan.PawnsListForReading.Count - 1; i >= 0; i--)
			{
				Pawn pawn = caravan.PawnsListForReading[i];
				caravan.RemovePawn(pawn);
				if (!OutpostUtility.MovePawnIntoOutpost(outpost, pawn))
				{
					Log.Error("[DreamsOutposts] Failed to move " + pawn + " into outpost " + outpost.Label + "; returning it to caravan " + caravan.Label + ".");
					caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: false);
				}
			}
		}
	}
}
