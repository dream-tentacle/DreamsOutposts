using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	public static class OutpostAirdropUtility
	{
		private sealed class PendingCargoHolder : IThingHolder
		{
			private readonly Outpost outpost;

			public IThingHolder ParentHolder => outpost;

			public PendingCargoHolder(Outpost outpost)
			{
				this.outpost = outpost;
			}

			public void GetChildHolders(List<IThingHolder> outChildren)
			{
			}

			public ThingOwner GetDirectlyHeldThings()
			{
				return outpost.pendingAirdropPawns;
			}
		}

		public const int SteelPerPod = 70;

		public const int MaxLaunchDistance = 40;

		private const string RequiredResearchDefName = "TransportPod";

		private const float FallbackPodMassCapacity = 150f;

		private static float cachedPodMassCapacity = -1f;

		public static float PodMassCapacity
		{
			get
			{
				if (cachedPodMassCapacity < 0f)
				{
					CompProperties_Transporter props = ThingDefOf.TransportPod?.GetCompProperties<CompProperties_Transporter>();
					cachedPodMassCapacity = ((props != null && props.massCapacity > 0f) ? props.massCapacity : 150f);
				}
				return cachedPodMassCapacity;
			}
		}

		public static ResearchProjectDef RequiredResearch => DefDatabase<ResearchProjectDef>.GetNamedSilentFail("TransportPod");

		private static AcceptanceReport ResearchReport()
		{
			ResearchProjectDef research = RequiredResearch;
			if (research == null || research.IsFinished)
			{
				return true;
			}
			string reason = "MissingRequiredResearch".Translate() + ": " + research.LabelCap;
			return reason;
		}

		public static AcceptanceReport CanBuyPod(Outpost outpost)
		{
			AcceptanceReport research = ResearchReport();
			if (!research.Accepted)
			{
				return research;
			}
			if (outpost == null || outpost.Destroyed)
			{
				return "DreamsOutposts.OutpostGone".Translate().Resolve();
			}
			int steel = OutpostStockUtility.CountInStock(outpost, ThingDefOf.Steel);
			if (steel < 70)
			{
				return "DreamsOutposts.NotEnoughSteelForPod".Translate(70, steel).Resolve();
			}
			return true;
		}

		public static AcceptanceReport CanAirdrop(Outpost outpost)
		{
			AcceptanceReport research = ResearchReport();
			if (!research.Accepted)
			{
				return research;
			}
			if (outpost == null || outpost.Destroyed)
			{
				return "DreamsOutposts.OutpostGone".Translate().Resolve();
			}
			if (outpost.airdropPods <= 0)
			{
				return "DreamsOutposts.NoAirdropPods".Translate().Resolve();
			}
			return true;
		}

		public static Command BuyPodCommand(Outpost outpost)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DreamsOutposts.CommandAddAirdropPod".Translate(),
				defaultDesc = "DreamsOutposts.CommandAddAirdropPodDesc".Translate(70, PodMassCapacity.ToString("F0")),
				icon = ThingDefOf.TransportPod.uiIcon,
				action = delegate
				{
					TryBuyPod(outpost);
				}
			};
			AcceptanceReport report = CanBuyPod(outpost);
			if (!report.Accepted)
			{
				command.Disable(report.Reason);
			}
			return command;
		}

		public static Command AirdropCommand(Outpost outpost)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "DreamsOutposts.CommandAirdrop".Translate(),
				defaultDesc = "DreamsOutposts.CommandAirdropDesc".Translate(40, PodMassCapacity.ToString("F0")),
				icon = CompLaunchable.LaunchCommandTex,
				action = delegate
				{
					Find.WindowStack.Add(new Window_LoadAirdrop(outpost));
				}
			};
			AcceptanceReport report = CanAirdrop(outpost);
			if (!report.Accepted)
			{
				command.Disable(report.Reason);
			}
			return command;
		}

		public static void TryBuyPod(Outpost outpost)
		{
			AcceptanceReport report = CanBuyPod(outpost);
			if (!report.Accepted)
			{
				Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			int removed = OutpostStockUtility.TakeFromStock(outpost, ThingDefOf.Steel, 70);
			if (removed < 70)
			{
				Log.Error("[DreamsOutposts] Outpost " + outpost.Label + " paid only " + removed + " of " + 70 + " steel for an airdrop pod, but the pod is granted anyway.");
			}
			outpost.airdropPods++;
			SoundDefOf.Tick_High.PlayOneShotOnCamera();
			Messages.Message("DreamsOutposts.AirdropPodAdded".Translate(outpost.airdropPods), outpost, MessageTypeDefOf.TaskCompletion, historical: false);
		}

		public static void AddToTransferables(Thing thing, List<TransferableOneWay> transferables)
		{
			TransferableOneWay transferable = TransferableUtility.TransferableMatching(thing, transferables, TransferAsOneMode.PodsOrCaravanPacking);
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
			List<Thing> stock = outpost.InventoryItems;
			for (int j = 0; j < stock.Count; j++)
			{
				AddToTransferables(stock[j], transferables);
			}
		}

		public static float MassUsage(List<TransferableOneWay> transferables)
		{
			return CollectionsMassCalculator.MassUsageTransferables(transferables, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMass: true);
		}

		public static int PodsNeeded(float massUsage)
		{
			return Mathf.Max(1, Mathf.CeilToInt(massUsage / PodMassCapacity));
		}

		public static void BeginTargeting(Outpost outpost, List<TransferableOneWay> transferables)
		{
			if (outpost == null || outpost.Destroyed)
			{
				return;
			}
			CheckStalePending(outpost);
			MoveSelectedPawnsToPending(outpost, transferables);
			PlanetTile origin = outpost.Tile;
			IEnumerable<IThingHolder> pods = Gen.YieldSingle((IThingHolder)new PendingCargoHolder(outpost));
			Action<PlanetTile, TransportersArrivalAction> launchAction = delegate(PlanetTile tile, TransportersArrivalAction arrivalAction)
			{
				Launch(outpost, transferables, tile, arrivalAction);
			};
			CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(origin)));
			Find.WorldSelector.ClearSelection();
			Find.WorldTargeter.BeginTargeting((GlobalTargetInfo target) => ChoseWorldTarget(target, origin, pods, launchAction), canTargetTiles: true, CompLaunchable.TargeterMouseAttachment, closeWorldTabWhenFinished: true, delegate
			{
				PlanetTile center = origin;
				if (origin.Layer != PlanetLayer.Selected)
				{
					center = PlanetLayer.Selected.GetClosestTile_NewTemp(origin);
				}
				GenDraw.DrawWorldRadiusRing(center, 40);
			}, (GlobalTargetInfo target) => TargetingLabelGetter(target, origin, pods, launchAction), null, origin, showCancelButton: true);
		}

		public static void CheckStalePending(Outpost outpost)
		{
			if (outpost != null && outpost.HasPendingAirdropCargo && (Find.WorldTargeter == null || !Find.WorldTargeter.IsTargeting) && (Find.Targeter == null || !Find.Targeter.IsTargeting))
			{
				ReturnPendingCargo(outpost);
			}
		}

		private static void Launch(Outpost outpost, List<TransferableOneWay> transferables, PlanetTile destinationTile, TransportersArrivalAction arrivalAction)
		{
			float massUsage;
			List<ActiveTransporterInfo> pods = BuildPods(outpost, transferables, out massUsage);
			Find.WorldTargeter.StopTargeting();
			if (pods != null)
			{
				Depart(outpost, pods, destinationTile, arrivalAction, massUsage);
			}
		}

		private static List<ActiveTransporterInfo> BuildPods(Outpost outpost, List<TransferableOneWay> transferables, out float massUsage)
		{
			massUsage = MassUsage(transferables);
			if (outpost == null || outpost.Destroyed)
			{
				ReturnPendingCargo(outpost);
				return null;
			}
			int needed = PodsNeeded(massUsage);
			if (needed > outpost.airdropPods)
			{
				Messages.Message("DreamsOutposts.AirdropNotEnoughPods".Translate(needed, outpost.airdropPods), MessageTypeDefOf.RejectInput, historical: false);
				ReturnPendingCargo(outpost);
				return null;
			}
			List<ActiveTransporterInfo> pods = new List<ActiveTransporterInfo>();
			for (int i = 0; i < needed; i++)
			{
				pods.Add(new ActiveTransporterInfo
				{
					sentTransporterDef = ThingDefOf.TransportPod
				});
			}
			int podIndex = 0;
			ThingOwner<Pawn> pending = outpost.pendingAirdropPawns;
			List<Pawn> pawnsToSend = TransferableUtility.GetPawnsFromTransferables(transferables);
			for (int j = 0; j < pawnsToSend.Count; j++)
			{
				Pawn pawn = pawnsToSend[j];
				if (pawn == null)
				{
					continue;
				}
				bool taken = pending?.Remove(pawn) ?? false;
				if (!taken)
				{
					taken = outpost.pawns.Remove(pawn);
					if (taken) outpost.RequestUpdate();
				}
				if (!taken)
				{
					Log.Error("[DreamsOutposts] Could not find " + pawn?.ToString() + " in outpost " + outpost.Label + " or in its pending airdrop cargo; leaving it behind.");
					continue;
				}
				if (!pods[podIndex].innerContainer.TryAdd(pawn))
				{
					Log.Error("[DreamsOutposts] Failed to load " + pawn?.ToString() + " into an outpost airdrop pod; returning it to outpost " + outpost.Label + ".");
					if (!outpost.pawns.TryAdd(pawn))
					{
						Log.Error("[DreamsOutposts] Failed to return " + pawn?.ToString() + " to outpost " + outpost.Label + "; passing it to the world so it is not lost.");
						Find.WorldPawns.PassToWorld(pawn);
					}
					else outpost.RequestUpdate();
				}
				podIndex = (podIndex + 1) % needed;
			}
			ReturnPendingCargo(outpost);
			for (int k = 0; k < transferables.Count; k++)
			{
				TransferableOneWay transferable = transferables[k];
				if (transferable == null || transferable.ThingDef == null || transferable.ThingDef.category == ThingCategory.Pawn || transferable.CountToTransfer <= 0)
				{
					continue;
				}
				ActiveTransporterInfo pod = pods[podIndex];
				TransferableUtility.Transfer(transferable.things, transferable.CountToTransfer, delegate(Thing piece, IThingHolder originalHolder)
				{
					if (!pod.innerContainer.TryAdd(piece))
					{
						Log.Error("[DreamsOutposts] Failed to load " + piece?.ToString() + " into an outpost airdrop pod; returning it to outpost " + outpost.Label + ".");
						if (!outpost.inventory.TryAdd(piece))
						{
							Log.Error("[DreamsOutposts] Failed to return " + piece?.ToString() + " to outpost " + outpost.Label + ".");
						}
					}
				});
				podIndex = (podIndex + 1) % needed;
			}
			outpost.airdropPods -= needed;
			return pods;
		}

		private static void Depart(Outpost outpost, List<ActiveTransporterInfo> pods, PlanetTile destinationTile, TransportersArrivalAction arrivalAction, float massUsage)
		{
			int pawnCount = 0;
			int itemCount = 0;
			for (int i = 0; i < pods.Count; i++)
			{
				ThingOwner container = pods[i].innerContainer;
				for (int j = 0; j < container.Count; j++)
				{
					if (container[j] is Pawn pawn)
					{
						pawnCount++;
						if (!pawn.IsWorldPawn())
						{
							Find.WorldPawns.PassToWorld(pawn);
						}
					}
					else
					{
						itemCount++;
					}
				}
			}
			TravellingTransporters travelling = (TravellingTransporters)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.TravellingTransporters);
			travelling.Tile = outpost.Tile;
			travelling.SetFaction(Faction.OfPlayer);
			travelling.destinationTile = destinationTile;
			travelling.arrivalAction = arrivalAction;
			for (int k = 0; k < pods.Count; k++)
			{
				travelling.AddTransporter(pods[k], justLeftTheMap: true);
			}
			Find.WorldObjects.Add(travelling);
			SoundDefOf.Tick_High.PlayOneShotOnCamera();
			if (pawnCount == 0 && itemCount == 0)
			{
				Log.Error("[DreamsOutposts] An outpost airdrop from " + outpost.Label + " launched with empty pods.");
			}
			Messages.Message("DreamsOutposts.AirdropLaunched".Translate(outpost.LabelCap, pods.Count, massUsage.ToString("F0"), pawnCount, itemCount), new GlobalTargetInfo(destinationTile), MessageTypeDefOf.TaskCompletion, historical: false);
		}

		public static void ReturnPendingCargo(Outpost outpost)
		{
			ThingOwner<Pawn> pending = outpost?.pendingAirdropPawns;
			if (pending == null || pending.Count == 0)
			{
				return;
			}
			List<Pawn> tmpPawns = pending.InnerListForReading.ToList();
			for (int i = 0; i < tmpPawns.Count; i++)
			{
				Pawn pawn = tmpPawns[i];
				if (pawn == null) continue;
				if (pending.TryTransferToContainer(pawn, outpost.pawns, 1) > 0)
				{
					outpost.RequestUpdate();
					continue;
				}
				else
				{
					Log.Error("[DreamsOutposts] Failed to return " + pawn?.ToString() + " to outpost " + outpost.Label + " after an airdrop was cancelled.");
					pending.Remove(pawn);
					if (!outpost.pawns.TryAdd(pawn))
					{
						Log.Error("[DreamsOutposts] Failed to re-add " + pawn?.ToString() + " to outpost " + outpost.Label + "; passing it to the world so it is not lost.");
						Find.WorldPawns.PassToWorld(pawn);
					}
					else outpost.RequestUpdate();
				}
			}
		}

		private static void MoveSelectedPawnsToPending(Outpost outpost, List<TransferableOneWay> transferables)
		{
			ThingOwner<Pawn> pending = outpost.pendingAirdropPawns;
			if (pending == null)
			{
				return;
			}
			List<Pawn> pawns = TransferableUtility.GetPawnsFromTransferables(transferables);
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null) continue;
				if (outpost.pawns.TryTransferToContainer(pawn, pending, 1) > 0)
				{
					outpost.RequestUpdate();
				}
				else
				{
					Log.Error("[DreamsOutposts] Failed to move " + pawn?.ToString() + " out of outpost " + outpost.Label + " for an airdrop; it stays in the outpost.");
				}
			}
		}

		private static bool ChoseWorldTarget(GlobalTargetInfo target, PlanetTile origin, IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
		{
			if (!target.IsValid)
			{
				Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			if (target.HasWorldObject && !target.WorldObject.def.validLaunchTarget)
			{
				Messages.Message("MessageWorldObjectIsInvalid".Translate(target.WorldObject.Named("OBJECT")), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			if (ModsConfig.OdysseyActive && target.HasWorldObject && target.WorldObject.RequiresSignalJammerToReach)
			{
				Messages.Message("TransportPodDestinationRequiresSignalJammer".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			int distance = Find.WorldGrid.TraversalDistanceBetween(origin, target.Tile, passImpassable: true, int.MaxValue, canTraverseLayers: true);
			if (distance > 40)
			{
				Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			List<FloatMenuOption> options = GetArrivalOptions(target.Tile, pods, launchAction).ToList();
			if (!options.Any())
			{
				if (Find.World.Impassable(target.Tile))
				{
					Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
					return false;
				}
				launchAction(target.Tile, null);
				return true;
			}
			if (options.Count == 1)
			{
				if (!options.First().Disabled)
				{
					options.First().action();
					return true;
				}
				return false;
			}
			Find.WindowStack.Add(new FloatMenu(options));
			return false;
		}

		private static IEnumerable<FloatMenuOption> GetArrivalOptions(PlanetTile tile, IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
		{
			bool anything = false;
			if (TransportersArrivalAction_FormCaravan.CanFormCaravanAt(pods, tile) && !Find.WorldObjects.AnySettlementBaseAt(tile) && !Find.WorldObjects.AnySiteAt(tile))
			{
				anything = true;
				yield return new FloatMenuOption("FormCaravanHere".Translate(), delegate
				{
					launchAction(tile, new TransportersArrivalAction_FormCaravan("MessageTransportPodsArrived"));
				});
			}
			List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects;
			for (int i = 0; i < worldObjects.Count; i++)
			{
				if (worldObjects[i].Tile != tile)
				{
					continue;
				}
				foreach (FloatMenuOption option in worldObjects[i].GetTransportersFloatMenuOptions(pods, launchAction))
				{
					anything = true;
					yield return option;
				}
			}
			if (!anything && !Find.World.Impassable(tile))
			{
				yield return new FloatMenuOption("TransportPodsContentsWillBeLost".Translate(), delegate
				{
					launchAction(tile, null);
				});
			}
		}

		private static TaggedString TargetingLabelGetter(GlobalTargetInfo target, PlanetTile origin, IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
		{
			if (!target.IsValid)
			{
				return null;
			}
			if (target.Tile.Layer != origin.Layer && !origin.Layer.HasConnectionPathTo(target.Tile.Layer))
			{
				GUI.color = ColorLibrary.RedReadable;
				return "TransportPodDestinationNoPath".Translate(target.Tile.Layer.Def.Named("LAYER"));
			}
			if (ModsConfig.OdysseyActive)
			{
				WorldObject worldObject = Find.World.worldObjects.WorldObjectAt<WorldObject>(target.Tile);
				if (worldObject != null && worldObject.RequiresSignalJammerToReach)
				{
					GUI.color = ColorLibrary.RedReadable;
					return "TransportPodDestinationRequiresSignalJammer".Translate();
				}
			}
			int distance = Find.WorldGrid.TraversalDistanceBetween(origin, target.Tile, passImpassable: true, 40, canTraverseLayers: true);
			if (distance > 40)
			{
				GUI.color = ColorLibrary.RedReadable;
				return "TransportPodDestinationBeyondMaximumRange".Translate();
			}
			if (target.HasWorldObject && !target.WorldObject.def.validLaunchTarget)
			{
				return string.Empty;
			}
			List<FloatMenuOption> options = GetArrivalOptions(target.Tile, pods, launchAction).ToList();
			if (!options.Any())
			{
				return string.Empty;
			}
			if (options.Count == 1)
			{
				if (options.First().Disabled)
				{
					GUI.color = ColorLibrary.RedReadable;
				}
				return options.First().Label;
			}
			if (target.WorldObject is MapParent mapParent)
			{
				return "ClickToSeeAvailableOrders_WorldObject".Translate(mapParent.LabelCap);
			}
			return "ClickToSeeAvailableOrders_Empty".Translate();
		}
	}
}
