using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Outpost : WorldObject, IThingHolder, IThingHolderTickable
	{
		public OutpostTypeDef outpostTypeDef;

		public ThingOwner<Pawn> pawns;

		public ThingOwner<Thing> inventory;

		public OutpostFacility coreFacility;

		public List<OutpostSlot> extensionSlots;

		public List<OutpostEventInstance> events;

		/// <summary>
		/// 已排期、尚未发生的后续事件。这里的事件不在 events 里，也不参与普通随机事件抽取；
		/// 到期后由 OutpostEventUtility.TickEvents 通过 AddEvent 转成真正的事件实例。
		/// </summary>
		public List<OutpostScheduledEvent> scheduledEvents;

		public List<OutpostTemporaryEffect> temporaryEffects;

		public int establishedTick;

		public int level = 1;

		public int airdropPods;

		public int nextBombardTick;

		public ThingOwner<Pawn> pendingAirdropPawns;

		public ThingOwner<Pawn> adventurerCandidates;

		public AdventurerRecruitState adventurerRecruitment;

		private static readonly List<Pawn> EmptyPawns = new List<Pawn>();
		private static readonly List<Thing> EmptyInventory = new List<Thing>();

		public bool HasPendingAirdropCargo => pendingAirdropPawns != null && pendingAirdropPawns.Count > 0;

		public bool ShouldTickContents => false;

		public IEnumerable<Pawn> Pawns => pawns?.InnerListForReading ?? EmptyPawns;

		public List<Pawn> PawnsListForReading => pawns?.InnerListForReading ?? EmptyPawns;

		public List<Thing> InventoryItems => inventory?.InnerListForReading ?? EmptyInventory;

		public IEnumerable<Pawn> Colonists => PawnsListForReading.Where((Pawn p) => p.IsColonist);

		public IEnumerable<Pawn> OtherPawns => PawnsListForReading.Where((Pawn p) => !p.IsColonist);

		public IEnumerable<OutpostFacility> Facilities
		{
			get
			{
				if (coreFacility != null)
				{
					yield return coreFacility;
				}
				if (extensionSlots == null)
				{
					yield break;
				}
				for (int i = 0; i < extensionSlots.Count; i++)
				{
					OutpostFacility facility = extensionSlots[i].facility;
					if (facility != null)
					{
						yield return facility;
					}
				}
			}
		}

		public IEnumerable<OutpostFacility> OperationalFacilities
		{
			get
			{
				foreach (OutpostFacility facility in Facilities)
				{
					if (!OutpostTemporaryEffectUtility.IsFacilityDisabled(this, facility)) yield return facility;
				}
			}
		}

		public OutpostWorker Worker => outpostTypeDef?.Worker;

		public int MaxLevel => outpostTypeDef?.MaxLevel ?? 1;

		public bool IsMaxLevel => level >= MaxLevel;

		/// <summary>
		/// 据点当前防卫值：Σ 人员 + Σ 设施。不是存档值，每次读取都按当前 Pawn 和设施重新计算。
		/// </summary>
		public float Defense => OutpostDefenseUtility.TotalDefense(this);

		public OutpostEventInstance AddEvent(OutpostEventDef eventDef)
		{
			if (eventDef == null)
			{
				Log.Error("Cannot add a null event Def to outpost " + Label + ".");
				return null;
			}
			if (events == null)
			{
				events = new List<OutpostEventInstance>();
			}
			int createdTick = Find.TickManager.TicksGame;
			OutpostEventInstance instance = new OutpostEventInstance
			{
				def = eventDef,
				createdTick = createdTick,
				expireTick = createdTick + eventDef.durationTicks
			};
			if (!eventDef.InitializeInstance(this, instance)) return null;
			events.Add(instance);
			if (eventDef.onCreatedEffects != null)
			{
				OutpostEventContext context = new OutpostEventContext { outpost = this, instance = instance };
				for (int i = 0; i < eventDef.onCreatedEffects.Count; i++) eventDef.onCreatedEffects[i]?.Apply(context);
			}
			OutpostEventUtility.SendCreatedLetter(this, instance);
			return instance;
		}

		/// <summary>
		/// 登记一条 delayTicks 之后才发生的后续事件。只负责排期：
		/// 不创建事件实例，不进 events，也不发 Letter。到期由 TickEvents 处理。
		/// </summary>
		public OutpostScheduledEvent AddScheduledEvent(OutpostEventDef eventDef, int delayTicks)
		{
			if (eventDef == null)
			{
				Log.Error("Cannot schedule a null event Def on outpost " + Label + ".");
				return null;
			}
			if (scheduledEvents == null)
			{
				scheduledEvents = new List<OutpostScheduledEvent>();
			}
			OutpostScheduledEvent scheduled = new OutpostScheduledEvent
			{
				eventDef = eventDef,
				triggerTick = Find.TickManager.TicksGame + Mathf.Max(delayTicks, 0)
			};
			scheduledEvents.Add(scheduled);
			return scheduled;
		}

		/// <summary>
		/// 这个据点当前是否允许成为「普通随机事件」的目标。第一版一律允许。
		/// 只描述普通随机事件：剧情事件、后续事件、玩家行为触发事件和强制事件不一定受这里限制。
		/// 以后据点特殊状态、事件屏蔽设施和第三方 Mod 设施都从这个统一入口阻止普通随机事件；
		/// 全局调度器只调用这个方法，不自己去识别具体设施或状态。
		/// </summary>
		public virtual AcceptanceReport CanReceiveRandomEvent()
		{
			return true;
		}

		public int SlotCountForLevel => outpostTypeDef?.GetSlotCount(level) ?? 0;

		public OutpostLevelProperties CurrentLevelProperties => outpostTypeDef?.GetLevel(level);

		public OutpostLevelProperties NextLevelProperties => IsMaxLevel ? null : outpostTypeDef?.GetLevel(level + 1);

		public int TicksSinceEstablished => Mathf.Max(Find.TickManager.TicksGame - establishedTick, 0);

		public float DaysSinceEstablished => (float)TicksSinceEstablished / 60000f;

		public override string Label => outpostTypeDef?.label ?? base.Label;

		public override Material Material => MaterialPool.MatFrom(def.texture, ShaderDatabase.WorldOverlayTransparentLit, (base.Faction == null) ? Color.white : base.Faction.Color, 3550);

		protected override int UpdateRateTicks => Window_OutpostManage.Current?.Outpost == this ? 60 : 1250;

		public Outpost()
		{
			pawns = new ThingOwner<Pawn>(this, oneStackOnly: false);
			inventory = new ThingOwner<Thing>(this, oneStackOnly: false);
			pendingAirdropPawns = new ThingOwner<Pawn>(this, oneStackOnly: false);
			adventurerCandidates = new ThingOwner<Pawn>(this, oneStackOnly: false);
			adventurerRecruitment = new AdventurerRecruitState();
			extensionSlots = new List<OutpostSlot>();
			events = new List<OutpostEventInstance>();
			scheduledEvents = new List<OutpostScheduledEvent>();
			temporaryEffects = new List<OutpostTemporaryEffect>();
		}

		protected override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			OutpostTemporaryEffectUtility.RemoveExpired(this, Find.TickManager.TicksGame);
			OutpostEventUtility.TickEvents(this);
			foreach (OutpostFacility facility in Facilities)
			{
				if (OutpostTemporaryEffectUtility.IsFacilityDisabled(this, facility)) facility.TickDisabledComps(this, delta);
				else facility.TickComps(this, delta);
			}
			AgePawns(delta);
			OutpostAirdropUtility.CheckStalePending(this);
			AdventurerRecruitUtility.Tick(this);
		}

		private void AgePawns(int delta)
		{
			if (pawns == null)
			{
				return;
			}
			List<Pawn> list = pawns.InnerListForReading;
			for (int i = list.Count - 1; i >= 0; i--)
			{
				Pawn pawn = list[i];
				if (pawn != null && !pawn.Dead && pawn.ageTracker != null)
				{
					pawn.ageTracker.AgeTickInterval(delta);
				}
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref outpostTypeDef, "outpostTypeDef");
			Scribe_Values.Look(ref establishedTick, "establishedTick", 0);
			Scribe_Values.Look(ref level, "level", 1);
			Scribe_Values.Look(ref airdropPods, "airdropPods", 0);
			Scribe_Values.Look(ref nextBombardTick, "nextBombardTick", 0);
			Scribe_Deep.Look(ref coreFacility, "coreFacility");
			Scribe_Collections.Look(ref extensionSlots, "extensionSlots", LookMode.Deep);
			Scribe_Collections.Look(ref events, "events", LookMode.Deep);
			Scribe_Collections.Look(ref scheduledEvents, "scheduledEvents", LookMode.Deep);
			Scribe_Collections.Look(ref temporaryEffects, "temporaryEffects", LookMode.Deep);
			Scribe_Deep.Look(ref pawns, "pawns", this);
			Scribe_Deep.Look(ref inventory, "inventory", this);
			Scribe_Deep.Look(ref pendingAirdropPawns, "pendingAirdropPawns", this);
			Scribe_Deep.Look(ref adventurerCandidates, "adventurerCandidates", this);
			Scribe_Deep.Look(ref adventurerRecruitment, "adventurerRecruitment");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (pawns == null)
				{
					pawns = new ThingOwner<Pawn>(this, oneStackOnly: false);
				}
				if (inventory == null)
				{
					inventory = new ThingOwner<Thing>(this, oneStackOnly: false);
				}
				if (pendingAirdropPawns == null)
				{
					pendingAirdropPawns = new ThingOwner<Pawn>(this, oneStackOnly: false);
				}
				if (events == null)
				{
					events = new List<OutpostEventInstance>();
				}
				if (scheduledEvents == null)
				{
					scheduledEvents = new List<OutpostScheduledEvent>();
				}
				if (adventurerCandidates == null)
				{
					adventurerCandidates = new ThingOwner<Pawn>(this, oneStackOnly: false);
				}
				if (adventurerRecruitment == null)
				{
					adventurerRecruitment = new AdventurerRecruitState();
				}
				AdventurerRecruitUtility.Reconcile(this);
				if (temporaryEffects == null)
				{
					temporaryEffects = new List<OutpostTemporaryEffect>();
				}
				if (pendingAirdropPawns.Count > 0)
				{
					OutpostAirdropUtility.ReturnPendingCargo(this);
				}
				if (outpostTypeDef != null && coreFacility == null)
				{
					Log.Error("Outpost " + Label + " had no core facility after loading; reinstalling it from " + outpostTypeDef.defName + ".");
					InitializeCoreFacility();
				}
				EnsureExtensionSlots();
			}
		}

		private void EnsureExtensionSlots()
		{
			if (extensionSlots == null)
			{
				extensionSlots = new List<OutpostSlot>();
			}
			int wanted = outpostTypeDef?.GetSlotCount(level) ?? 0;
			while (extensionSlots.Count > wanted)
			{
				int index = extensionSlots.Count - 1;
				for (int i = extensionSlots.Count - 1; i >= 0; i--)
				{
					if (extensionSlots[i] == null || extensionSlots[i].IsEmpty)
					{
						index = i;
						break;
					}
				}
				OutpostSlot removed = extensionSlots[index];
				if (removed != null && !removed.IsEmpty)
				{
					Log.Error("Outpost " + Label + " level " + level + " allows only " + wanted + " extension slots, so the slot holding " + (removed.facility.def?.defName ?? "null") + " was removed and that facility is gone.");
				}
				extensionSlots.RemoveAt(index);
			}
			while (extensionSlots.Count < wanted)
			{
				extensionSlots.Add(new OutpostSlot());
			}
		}

		public void SetLevel(int newLevel)
		{
			level = Mathf.Clamp(newLevel, 1, MaxLevel);
			EnsureExtensionSlots();
		}

		private void InitializeCoreFacility()
		{
			OutpostFacilityDef def = outpostTypeDef?.coreFacility;
			if (def == null)
			{
				Log.Error("Outpost type " + (outpostTypeDef?.defName ?? "null") + " declares no coreFacility; this outpost will have no core facility.");
				coreFacility = null;
			}
			else
			{
				coreFacility = OutpostFacility.Create(def);
			}
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return inventory;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, pawns);
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, inventory);
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, pendingAirdropPawns);
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, adventurerCandidates);
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder(base.GetInspectString());
			if (stringBuilder.Length != 0)
			{
				stringBuilder.AppendLine();
			}
			OutpostAirdropUtility.CheckStalePending(this);
			stringBuilder.Append("Colonists: " + Colonists.Count());
			stringBuilder.AppendLine();
			stringBuilder.Append("Other pawns: " + OtherPawns.Count());
			stringBuilder.AppendLine();
			stringBuilder.Append("Item stacks: " + InventoryItems.Count);
			stringBuilder.AppendLine();
			stringBuilder.Append("Airdrop pods: " + airdropPods);
			stringBuilder.AppendLine();
			stringBuilder.Append("Core facility: " + (coreFacility?.def?.LabelCap ?? ((TaggedString)"none")));
			stringBuilder.AppendLine();
			stringBuilder.Append("Extension facilities: " + extensionSlots.Count((OutpostSlot s) => !s.IsEmpty) + "/" + extensionSlots.Count);
			stringBuilder.AppendLine();
			stringBuilder.Append("Level: " + level + "/" + MaxLevel);
			return stringBuilder.ToString();
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (base.Faction != Faction.OfPlayer)
			{
				yield break;
			}
			if (Prefs.DevMode && DebugSettings.godMode)
			{
				yield return OutpostEventUtility.AddAllWeightedEventsCommand(this);
			}
			OutpostAirdropUtility.CheckStalePending(this);
			yield return OutpostUtility.ManageCommand(this);
			yield return OutpostAirdropUtility.BuyPodCommand(this);
			yield return OutpostAirdropUtility.AirdropCommand(this);
			Command bombardCommand = OutpostBombardmentUtility.BombardCommand(this);
			if (bombardCommand != null)
			{
				yield return bombardCommand;
			}
			if (PawnsListForReading.Count > 0)
			{
				yield return OutpostCaravanUtility.FormCaravanCommand(this);
			}
			yield return OutpostAbandonUtility.AbandonCommand(this);
			OutpostWorker worker = Worker;
			if (worker == null)
			{
				yield break;
			}
			foreach (Gizmo gizmo2 in worker.GetGizmos(this))
			{
				yield return gizmo2;
			}
		}

		public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
		{
			foreach (Gizmo caravanGizmo in base.GetCaravanGizmos(caravan))
			{
				yield return caravanGizmo;
			}
			if (base.Faction == Faction.OfPlayer && caravan != null && caravan.IsPlayerControlled && Find.WorldSelector.SingleSelectedObject == caravan)
			{
				yield return OutpostCaravanUtility.EnterOutpostCommand(this, caravan);
			}
		}

		public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
		{
			foreach (FloatMenuOption transportersFloatMenuOption in base.GetTransportersFloatMenuOptions(pods, launchAction))
			{
				yield return transportersFloatMenuOption;
			}
			if (base.Faction != Faction.OfPlayer)
			{
				yield break;
			}
			foreach (FloatMenuOption floatMenuOption in TransportersArrivalActionUtility.GetFloatMenuOptions(() => TransportersArrivalAction_StoreInOutpost.CanStoreIn(this, base.Tile), () => new TransportersArrivalAction_StoreInOutpost(this), "DreamsOutposts.StoreInOutpost".Translate(), launchAction, base.Tile))
			{
				yield return floatMenuOption;
			}
		}

		public override void PostRemove()
		{
			OutpostAirdropUtility.ReturnPendingCargo(this);
			Worker?.OnRemoved(this);
			base.PostRemove();
			pawns?.ClearAndDestroyContentsOrPassToWorld();
			if (adventurerCandidates != null)
				foreach (Pawn candidate in adventurerCandidates.InnerListForReading.ToList())
					OutpostUtility.DiscardCandidate(candidate);
		}

		public static Outpost Create(Caravan caravan, OutpostTypeDef def)
		{
			Outpost outpost = (Outpost)WorldObjectMaker.MakeWorldObject(def.worldObjectDef);
			outpost.Tile = caravan.Tile;
			outpost.SetFaction(caravan.Faction);
			outpost.outpostTypeDef = def;
			outpost.establishedTick = Find.TickManager.TicksGame;
			outpost.level = 1;
			outpost.InitializeCoreFacility();
			outpost.EnsureExtensionSlots();
			OutpostUtility.TransferCaravanItemsTo(caravan, outpost);
			for (int i = caravan.PawnsListForReading.Count - 1; i >= 0; i--)
			{
				Pawn pawn = caravan.PawnsListForReading[i];
				caravan.RemovePawn(pawn);
				if (!OutpostUtility.MovePawnIntoOutpost(outpost, pawn))
				{
					Log.Error("Failed to move " + pawn?.ToString() + " into outpost " + outpost.Label + "; putting it back into the caravan.");
					caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: false);
				}
			}
			Find.WorldObjects.Add(outpost);
			def.Worker.OnCreated(outpost);
			if (caravan.PawnsListForReading.Count == 0)
			{
				caravan.Destroy();
			}
			return outpost;
		}
	}
}
