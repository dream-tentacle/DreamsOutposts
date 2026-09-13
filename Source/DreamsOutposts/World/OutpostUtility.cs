using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostUtility
	{
		public static AcceptanceReport CanCreate(Caravan caravan, OutpostTypeDef def)
		{
			if (caravan == null || def == null)
			{
				return "DreamsOutposts.InvalidCaravanOrOutpostType".Translate();
			}
			if (def.coreFacility != null && !def.coreFacility.IsResearchUnlocked)
			{
				return "DreamsOutposts.InstallFail.ResearchMissing".Translate(def.coreFacility.FirstMissingResearch?.LabelCap ?? ((TaggedString)"null"));
			}
			if (!SettleInEmptyTileUtility.CanCreateMapAt(caravan.Tile))
			{
				return "DreamsOutposts.TileCannotHostOutpost".Translate();
			}
			foreach (WorldObject obj in Find.WorldObjects.ObjectsAt(caravan.Tile))
			{
				if (obj != caravan)
				{
					return "DreamsOutposts.TileOccupied".Translate();
				}
			}
			return def.Worker.CanCreate(caravan.PawnsListForReading, caravan.Tile);
		}

		public static void Create(Caravan caravan, OutpostTypeDef def)
		{
			AcceptanceReport report = CanCreate(caravan, def);
			if (!report.Accepted)
			{
				Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
			}
			else
			{
				Outpost created = Outpost.Create(caravan, def);
				Find.WorldSelector.ClearSelection();
				Find.WorldSelector.Select(created, playSound: false);
			}
		}

		public static Command CreateCommand(Caravan caravan)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DreamsOutposts.CreateOutpost".Translate(),
				defaultDesc = "DreamsOutposts.CreateOutpostDesc".Translate(),
				icon = SettleUtility.SettleCommandTex
			};
			command.action = delegate
			{
				Find.WindowStack.Add(new OutpostCreationDialog(caravan));
			};
			if (DefDatabase<OutpostTypeDef>.AllDefsListForReading.Count == 0)
			{
				command.Disable("DreamsOutposts.NoOutpostTypesLoaded".Translate());
			}
			return command;
		}

		public static Command ManageCommand(Outpost outpost)
		{
			return new Command_Action
			{
				defaultLabel = "DreamsOutposts.ManageOutpost".Translate(),
				defaultDesc = "DreamsOutposts.ManageOutpostDesc".Translate(),
				icon = TexCommand.Install,
				action = delegate
				{
					Find.WindowStack.Add(new Window_OutpostManage(outpost));
				}
			};
		}

		public static void TransferCaravanItemsTo(Caravan caravan, Outpost outpost)
		{
			if (caravan == null || outpost == null || outpost.inventory == null)
			{
				return;
			}
			List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan).ToList();
			for (int i = 0; i < items.Count; i++)
			{
				Thing item = items[i];
				if (item == null)
				{
					continue;
				}
				if (item is Pawn carried)
				{
					MoveContainedPawnIntoOutpost(outpost, carried);
				}
				else
				{
					if (item.Destroyed || item.stackCount <= 0)
					{
						continue;
					}
					ThingOwner holder = item.holdingOwner;
					if (holder == null)
					{
						Log.Error("Cannot move " + item?.ToString() + " into outpost " + outpost.Label + ": it is not held by anything.");
						continue;
					}
					int stackCount = item.stackCount;
					holder.TryTransferToContainer(item, outpost.inventory, stackCount);
					if (holder.Contains(item) && item.stackCount > 0)
					{
						Log.Error("Moved only " + (stackCount - item.stackCount) + " of " + stackCount + " " + item.LabelNoCount + " into outpost " + outpost.Label + ".");
					}
				}
			}
		}

		public static bool IsHeldByOutpost(Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}
			for (IThingHolder holder = pawn.ParentHolder; holder != null; holder = holder.ParentHolder)
			{
				if (holder is Outpost)
				{
					return true;
				}
			}
			return false;
		}

		public static void TakeOutOfWorld(Pawn pawn)
		{
			if (pawn != null && pawn.IsWorldPawn())
			{
				Find.WorldPawns.RemovePawn(pawn);
			}
		}

		public static bool MovePawnIntoOutpost(Outpost outpost, Pawn pawn)
		{
			if (pawn == null)
			{
				return false;
			}
			if (outpost?.pawns == null || outpost.inventory == null)
			{
				Log.Error("Outpost " + (outpost?.Label ?? "null") + " has no pawn or inventory container, so " + pawn?.ToString() + " cannot be moved into it.");
				return false;
			}
			if (!outpost.pawns.Contains(pawn) && !outpost.pawns.TryAdd(pawn))
			{
				Log.Error("Failed to move " + pawn?.ToString() + " into outpost " + outpost.Label + ".");
				return false;
			}
			TakeOutOfWorld(pawn);
			MovePawnInventoryIntoOutpost(outpost, pawn);
			return true;
		}

		public static void MovePawnInventoryIntoOutpost(Outpost outpost, Pawn pawn)
		{
			Pawn_InventoryTracker inventory = pawn?.inventory;
			if (outpost?.inventory == null || inventory == null)
			{
				return;
			}
			ThingOwner<Thing> container = inventory.innerContainer;
			List<Thing> items = container.InnerListForReading.ToList();
			for (int i = 0; i < items.Count; i++)
			{
				Thing item = items[i];
				if (item == null)
				{
					continue;
				}
				if (item is Pawn carried)
				{
					MoveContainedPawnIntoOutpost(outpost, carried);
				}
				else if (!item.Destroyed && item.stackCount > 0)
				{
					int stackCount = item.stackCount;
					container.TryTransferToContainer(item, outpost.inventory, stackCount);
					if (container.Contains(item) && item.stackCount > 0)
					{
						Log.Error("Moved only " + (stackCount - item.stackCount) + " of " + stackCount + " " + item.LabelNoCount + " from " + pawn.LabelShort + "'s inventory into outpost " + outpost.Label + ".");
					}
				}
			}
		}

		private static void MoveContainedPawnIntoOutpost(Outpost outpost, Pawn pawn)
		{
			ThingOwner holder = pawn.holdingOwner;
			holder?.Remove(pawn);
			if (!MovePawnIntoOutpost(outpost, pawn) && holder != null && !holder.TryAdd(pawn))
			{
				Log.Error("Failed to put " + pawn?.ToString() + " back after it could not be moved into outpost " + (outpost?.Label ?? "null") + ".");
			}
		}

		public static List<OutpostFacilityDef> InstallableFacilities(OutpostTypeDef outpostTypeDef)
		{
			List<OutpostFacilityDef> result = new List<OutpostFacilityDef>();
			List<OutpostFacilityDef> defs = DefDatabase<OutpostFacilityDef>.AllDefsListForReading;
			for (int i = 0; i < defs.Count; i++)
			{
				OutpostFacilityDef def = defs[i];
				if (def != null && def.installableAsExtension && def.IsAllowedIn(outpostTypeDef) && MatchesCoreProduction(def, outpostTypeDef))
				{
					result.Add(def);
				}
			}
			result.Sort((OutpostFacilityDef a, OutpostFacilityDef b) => string.Compare(a.LabelCap, b.LabelCap, StringComparison.OrdinalIgnoreCase));
			return result;
		}

		private static bool MatchesCoreProduction(OutpostFacilityDef facility, OutpostTypeDef outpostType)
		{
			if (facility.FacilityTag != OutpostFacilityTagRegistry.ProductionBoost)
			{
				return true;
			}
			List<OutpostProductionProperties> coreProductions = outpostType?.coreFacility?.Productions;
			if (coreProductions.NullOrEmpty() || facility.productionModifiers.NullOrEmpty())
			{
				return false;
			}
			for (int i = 0; i < facility.productionModifiers.Count; i++)
			{
				OutpostProductionModifier modifier = facility.productionModifiers[i];
				for (int j = 0; j < coreProductions.Count; j++)
				{
					if (modifier != null && modifier.Matches(coreProductions[j])) return true;
				}
			}
			return false;
		}

		public static int CountInstalled(Outpost outpost, OutpostFacilityDef def)
		{
			if (outpost == null || def == null)
			{
				return 0;
			}
			int count = 0;
			foreach (OutpostFacility facility2 in outpost.Facilities)
			{
				if (facility2?.def == def)
				{
					count++;
				}
			}
			return count;
		}

		public static bool IsInstallLimitReached(Outpost outpost, OutpostFacilityDef def)
		{
			if (outpost == null || def == null || def.maxPerOutpost <= 0)
			{
				return false;
			}
			return CountInstalled(outpost, def) >= def.maxPerOutpost;
		}
	}
}
