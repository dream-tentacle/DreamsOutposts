using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	/// <summary>左侧（Colony）是远行队，右侧（Trader）是据点仓库；正数取出，负数存入。</summary>
	internal sealed class OutpostExchangeTradeable : Tradeable
	{
		public override bool Interactive => true;
		public override bool TraderWillTrade => true;
		public override bool IsCurrency => false;
		public override TransferablePositiveCountDirection PositiveCountDirection => TransferablePositiveCountDirection.Source;

		public override AcceptanceReport UnderflowReport()
		{
			return new AcceptanceReport("DreamsOutposts.ExchangeOutpost.CaravanHasNoMore".Translate());
		}

		public override AcceptanceReport OverflowReport()
		{
			return new AcceptanceReport("DreamsOutposts.ExchangeOutpost.OutpostHasNoMore".Translate());
		}
	}

	internal static class OutpostExchangeUtility
	{
		public static List<OutpostExchangeTradeable> Build(Caravan caravan, Outpost outpost)
		{
			List<Tradeable> all = new List<Tradeable>();
			List<Thing> caravanItems = CaravanInventoryUtility.AllInventoryItems(caravan).ToList();
			for (int i = 0; i < caravanItems.Count; i++) Add(all, caravanItems[i], Transactor.Colony);
			List<Thing> stock = outpost.InventoryItems;
			for (int i = 0; i < stock.Count; i++) Add(all, stock[i], Transactor.Trader);
			return all.OfType<OutpostExchangeTradeable>()
				.OrderBy((OutpostExchangeTradeable t) => TransferableUIUtility.DefaultListOrderPriority(t))
				.ThenBy((OutpostExchangeTradeable t) => t.ThingDef.label)
				.ThenBy((OutpostExchangeTradeable t) => QualityOrder(t.AnyThing))
				.ThenBy((OutpostExchangeTradeable t) => t.AnyThing.HitPoints)
				.ToList();
		}

		private static void Add(List<Tradeable> all, Thing thing, Transactor owner)
		{
			if (thing == null || thing.Destroyed || thing is Pawn || thing.def?.category != ThingCategory.Item) return;
			OutpostExchangeTradeable transferable = TransferableUtility.TradeableMatching(thing, all) as OutpostExchangeTradeable;
			if (transferable == null)
			{
				transferable = new OutpostExchangeTradeable();
				all.Add(transferable);
			}
			transferable.AddThing(thing, owner);
		}

		public static float MassDelta(List<OutpostExchangeTradeable> transferables)
		{
			float delta = 0f;
			for (int i = 0; i < transferables.Count; i++)
			{
				OutpostExchangeTradeable t = transferables[i];
				if (t.CountToTransfer > 0) delta += MassOf(t.thingsTrader, t.CountToTransfer);
				else if (t.CountToTransfer < 0) delta -= MassOf(t.thingsColony, -t.CountToTransfer);
			}
			return delta;
		}

		private static float MassOf(List<Thing> things, int count)
		{
			float mass = 0f;
			TransferableUtility.TransferNoSplit(things, count,
				(Thing thing, int taken) => mass += thing.GetStatValue(StatDefOf.Mass) * taken,
				removeIfTakingEntireThing: false, errorIfNotEnoughThings: false);
			return mass;
		}

		public static bool Execute(Caravan caravan, Outpost outpost, List<OutpostExchangeTradeable> transferables)
		{
			bool failed = false;
			// 先卸货再装货，结算中途也不会比最终状态更重。
			for (int i = 0; i < transferables.Count; i++)
			{
				OutpostExchangeTradeable t = transferables[i];
				if (t.CountToTransfer >= 0) continue;
				TransferableUtility.Transfer(t.thingsColony, -t.CountToTransfer, delegate(Thing piece, IThingHolder original)
				{
					if (!outpost.inventory.TryAdd(piece))
					{
						failed = true;
						Restore(piece, original);
					}
				});
			}

			for (int i = 0; i < transferables.Count; i++)
			{
				OutpostExchangeTradeable t = transferables[i];
				if (t.CountToTransfer <= 0) continue;
				TransferableUtility.Transfer(t.thingsTrader, t.CountToTransfer, delegate(Thing piece, IThingHolder original)
				{
					Pawn carrier = CaravanInventoryUtility.FindPawnToMoveInventoryTo(piece, caravan.PawnsListForReading, null);
					ThingOwner<Thing> target = carrier?.inventory?.innerContainer;
					if (target == null || !target.TryAdd(piece))
					{
						failed = true;
						Restore(piece, original);
						return;
					}
					CompForbiddable forbiddable = piece.TryGetComp<CompForbiddable>();
					if (forbiddable != null) forbiddable.Forbidden = false;
				});
			}

			caravan.RecacheInventory();
			outpost.RequestUpdate();
			return !failed;
		}

		private static void Restore(Thing piece, IThingHolder originalHolder)
		{
			ThingOwner original = originalHolder?.GetDirectlyHeldThings();
			if (piece == null || piece.Destroyed || piece.holdingOwner == original) return;
			if (original == null || !original.TryAdd(piece))
				Log.Error("[DreamsOutposts] Failed to return " + piece + " after an outpost exchange transfer failed.");
		}

		private static int QualityOrder(Thing thing)
		{
			QualityCategory quality;
			return thing != null && thing.TryGetQuality(out quality) ? (int)quality : -1;
		}
	}

	/// <summary>交易式双向交换窗口，不创建全局 TradeSession。</summary>
	public sealed class Window_ExchangeCaravanWithOutpost : Window
	{
		private const float TitleHeight = 35f;
		private const float SummaryHeight = 30f;
		private const float HeaderHeight = 30f;
		private const float RowHeight = 30f;
		private const float BottomHeight = 55f;
		private const float CountWidth = 110f;
		private const float AdjustWidth = 240f;
		private static readonly Vector2 ButtonSize = new Vector2(160f, 40f);

		private readonly Caravan caravan;
		private readonly Outpost outpost;
		private List<OutpostExchangeTradeable> transferables = new List<OutpostExchangeTradeable>();
		private Vector2 scrollPosition;
		private bool exchanging;

		public override Vector2 InitialSize => new Vector2(1024f, UI.screenHeight);
		protected override float Margin => 0f;

		public Window_ExchangeCaravanWithOutpost(Caravan caravan, Outpost outpost)
		{
			this.caravan = caravan;
			this.outpost = outpost;
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
			soundAppear = SoundDefOf.CommsWindow_Open;
			soundClose = SoundDefOf.CommsWindow_Close;
		}

		public override void PostOpen()
		{
			base.PostOpen();
			transferables = OutpostExchangeUtility.Build(caravan, outpost);
		}

		public override bool CausesMessageBackground() => true;
		public override void OnAcceptKeyPressed() => TryExchange();

		public override void Close(bool doCloseSound = true)
		{
			DragSliderManager.ForceStop();
			base.Close(doCloseSound);
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (!Valid())
			{
				Close();
				return;
			}

			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), "DreamsOutposts.ExchangeOutpost.Title".Translate(outpost.Label));
			Text.Font = GameFont.Small;
			DrawSummary(new Rect(12f, TitleHeight, inRect.width - 24f, SummaryHeight));

			Rect body = new Rect(0f, TitleHeight + SummaryHeight, inRect.width, inRect.height - TitleHeight - SummaryHeight);
			Widgets.DrawMenuSection(body);
			body = body.ContractedBy(17f);
			Widgets.BeginGroup(body);
			Rect content = body.AtZero();
			DrawHeader(new Rect(0f, 0f, content.width, HeaderHeight));
			DrawRows(new Rect(0f, HeaderHeight, content.width, content.height - HeaderHeight - BottomHeight));
			DrawButtons(content);
			Widgets.EndGroup();
		}

		private void DrawSummary(Rect rect)
		{
			float current = caravan.MassUsage;
			float capacity = caravan.MassCapacity;
			float after = Mathf.Max(0f, current + OutpostExchangeUtility.MassDelta(transferables));
			Color previous = GUI.color;
			if (after > capacity + 0.001f) GUI.color = ColorLibrary.RedReadable;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, "DreamsOutposts.ExchangeOutpost.Mass".Translate(current.ToString("F1"), capacity.ToString("F1"), after.ToString("F1")));
			Text.Anchor = TextAnchor.UpperLeft;
			GUI.color = previous;
		}

		private static void DrawHeader(Rect rect)
		{
			Widgets.DrawLightHighlight(rect);
			float itemWidth = rect.width - CountWidth * 2f - AdjustWidth;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(80f, rect.y, itemWidth - 80f, rect.height), "DreamsOutposts.ExchangeOutpost.Item".Translate());
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(itemWidth, rect.y, CountWidth, rect.height), "DreamsOutposts.ExchangeOutpost.Caravan".Translate());
			Widgets.Label(new Rect(itemWidth + CountWidth, rect.y, AdjustWidth, rect.height), "DreamsOutposts.ExchangeOutpost.Direction".Translate());
			Widgets.Label(new Rect(rect.width - CountWidth, rect.y, CountWidth, rect.height), "DreamsOutposts.ExchangeOutpost.Stock".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private void DrawRows(Rect rect)
		{
			Rect view = new Rect(0f, 0f, rect.width - 16f, 6f + transferables.Count * RowHeight);
			Widgets.BeginScrollView(rect, ref scrollPosition, view);
			float y = 6f;
			for (int i = 0; i < transferables.Count; i++, y += RowHeight)
			{
				if (y < scrollPosition.y - RowHeight || y > scrollPosition.y + rect.height) continue;
				DrawRow(new Rect(0f, y, view.width, RowHeight), transferables[i], i);
			}
			Widgets.EndScrollView();
		}

		private static void DrawRow(Rect rect, OutpostExchangeTradeable transferable, int index)
		{
			if (index % 2 == 1) Widgets.DrawLightHighlight(rect);
			Widgets.BeginGroup(rect);
			Rect row = rect.AtZero();
			float itemWidth = row.width - CountWidth * 2f - AdjustWidth;
			Rect item = new Rect(0f, 0f, itemWidth, row.height);
			Rect caravanCount = new Rect(item.xMax, 0f, CountWidth, row.height);
			Rect adjust = new Rect(caravanCount.xMax, 0f, AdjustWidth, row.height);
			Rect stockCount = new Rect(adjust.xMax, 0f, CountWidth, row.height);
			ExchangeTransferableUiOriginal.DrawTransferableInfo(transferable, item);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(caravanCount, transferable.CountHeldBy(Transactor.Colony).ToStringCached());
			Widgets.Label(stockCount, transferable.CountHeldBy(Transactor.Trader).ToStringCached());
			Text.Anchor = TextAnchor.UpperLeft;
			ExchangeTransferableUiOriginal.DrawCountAdjust(adjust, transferable, index,
				transferable.GetMinimumToTransfer(), transferable.GetMaximumToTransfer());
			Widgets.EndGroup();
		}

		private void DrawButtons(Rect rect)
		{
			float y = rect.height - ButtonSize.y;
			Rect accept = new Rect(rect.width / 2f - ButtonSize.x / 2f, y, ButtonSize.x, ButtonSize.y);
			if (Widgets.ButtonText(accept, "AcceptButton".Translate())) TryExchange();
			if (Widgets.ButtonText(new Rect(accept.x - 10f - ButtonSize.x, y, ButtonSize.x, ButtonSize.y), "ResetButton".Translate()))
			{
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();
				for (int i = 0; i < transferables.Count; i++) transferables[i].ForceTo(0);
			}
			if (Widgets.ButtonText(new Rect(accept.xMax + 10f, y, ButtonSize.x, ButtonSize.y), "CancelButton".Translate())) Close();
		}

		private void TryExchange()
		{
			if (exchanging || !Valid()) return;
			if (!transferables.Any((OutpostExchangeTradeable t) => t.CountToTransfer != 0))
			{
				Messages.Message("DreamsOutposts.ExchangeOutpost.NothingSelected".Translate(), MessageTypeDefOf.RejectInput, false);
				return;
			}
			float after = caravan.MassUsage + OutpostExchangeUtility.MassDelta(transferables);
			if (after > caravan.MassCapacity + 0.001f)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"DreamsOutposts.ExchangeOutpost.OverMassConfirm".Translate(after.ToString("F1"), caravan.MassCapacity.ToString("F1")),
					ExecuteExchange));
				return;
			}
			ExecuteExchange();
		}

		private void ExecuteExchange()
		{
			if (exchanging || !Valid()) return;
			exchanging = true;
			bool success = OutpostExchangeUtility.Execute(caravan, outpost, transferables);
			exchanging = false;
			if (!success)
			{
				Messages.Message("DreamsOutposts.ExchangeOutpost.PartialFailure".Translate(), MessageTypeDefOf.RejectInput, false);
				transferables = OutpostExchangeUtility.Build(caravan, outpost);
				return;
			}
			SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
			Messages.Message("DreamsOutposts.ExchangeOutpost.Done".Translate(outpost.Label), caravan, MessageTypeDefOf.TaskCompletion, false);
			Close(false);
		}

		private bool Valid()
		{
			return caravan != null && !caravan.Destroyed && caravan.IsPlayerControlled
				&& outpost != null && !outpost.Destroyed && outpost.Faction == Faction.OfPlayer
				&& outpost.inventory != null && caravan.Tile == outpost.Tile;
		}
	}
}
