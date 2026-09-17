using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	/// <summary>
	/// 临时集市的物资选择窗口。列表直接使用原版 TransferableOneWayWidget
	/// （原版运输舱装载、商队装载用的同一个控件），所以排序、快速搜索、图标、
	/// 数量滑条和单位市值列都是原版外观和原版交互。
	/// 本窗口只负责三件事：列出可提供的据点库存、显示已提供市值与回报上限、
	/// 结算时按实际取走的物资记录赠送数值并发放回报。
	/// </summary>
	public class Window_OutpostTemporaryMarket : Window
	{
		private const float TitleRectHeight = 35f;

		private const float BottomAreaHeight = 40f;

		private const float InfoAreaHeight = 44f;

		private const float ListBottomGap = 6f;

		private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

		private readonly Outpost outpost;

		/// <summary>集市愿意支付回报的物资市值上限，来自事件 Def 的 maxMarketValue。</summary>
		private readonly float cap;

		/// <summary>回报倍率，来自事件 Def 的 returnFactor。</summary>
		private readonly float factor;

		private string title;

		private List<TransferableOneWay> transferables;

		private TransferableOneWayWidget itemsTransfer;

		/// <summary>
		/// 记录下来的赠送数值：结算时按实际从据点库存取走并销毁的物资累加市值，
		/// 而不是用界面上的预测值，所以它同时也是发奖依据。
		/// </summary>
		private float giftedValue;

		private bool exchanging;

		private float RewardCap => cap * factor;

		public override Vector2 InitialSize => new Vector2(1024f, UI.screenHeight);

		protected override float Margin => 0f;

		public Window_OutpostTemporaryMarket(Outpost outpost, float cap, float factor)
		{
			this.outpost = outpost;
			this.cap = Mathf.Max(cap, 0f);
			this.factor = Mathf.Max(factor, 0f);
			doCloseX = true;
			forcePause = true;
			absorbInputAroundWindow = true;
		}

		public override void PostOpen()
		{
			base.PostOpen();
			title = "DreamsOutposts.TemporaryMarket.Title".Translate();
			RecalculateTransferables();
		}

		public override bool CausesMessageBackground()
		{
			return true;
		}

		public override void OnAcceptKeyPressed()
		{
			TryExchange();
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (outpost == null || outpost.Destroyed)
			{
				Close();
				return;
			}
			Text.Font = GameFont.Medium;
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(new Rect(0f, 0f, inRect.width, TitleRectHeight), title);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			inRect.yMin += TitleRectHeight + 4f;
			Widgets.DrawMenuSection(inRect);
			inRect = inRect.ContractedBy(17f);
			Widgets.BeginGroup(inRect);
			Rect contentRect = inRect.AtZero();
			Rect listRect = contentRect;
			listRect.yMax -= BottomAreaHeight + InfoAreaHeight + ListBottomGap;
			if (!exchanging)
			{
				itemsTransfer.OnGUI(listRect, out bool _);
				float infoY = contentRect.height - BottomAreaHeight - InfoAreaHeight;
				DrawSummary(new Rect(0f, infoY, contentRect.width, 22f), new Rect(0f, infoY + 22f, contentRect.width, 22f));
				DoBottomButtons(contentRect);
			}
			Widgets.EndGroup();
		}

		private void DrawSummary(Rect infoRect, Rect warnRect)
		{
			float offered = SelectedValue();
			float reward = Mathf.Min(offered, cap) * factor;
			Widgets.Label(infoRect, "DreamsOutposts.TemporaryMarket.Value".Translate(offered.ToStringMoney(), cap.ToStringMoney(), reward.ToStringMoney()));
			if (offered > cap + 0.01f)
			{
				GUI.color = new Color(1f, 0.52f, 0.42f);
				Widgets.Label(warnRect, "DreamsOutposts.TemporaryMarket.OverCap".Translate(cap.ToStringMoney(), RewardCap.ToStringMoney()));
				GUI.color = Color.white;
			}
		}

		private void DoBottomButtons(Rect rect)
		{
			float y = rect.height - BottomAreaHeight;
			if (Widgets.ButtonText(new Rect(0f, y, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
			{
				Close();
			}
			if (Widgets.ButtonText(new Rect(rect.width / 2f - BottomButtonSize.x / 2f, y, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
			{
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();
				ResetCounts();
			}
			if (Widgets.ButtonText(new Rect(rect.width - BottomButtonSize.x, y, BottomButtonSize.x, BottomButtonSize.y), "DreamsOutposts.TemporaryMarket.Exchange".Translate()))
			{
				TryExchange();
			}
		}

		private void TryExchange()
		{
			if (exchanging)
			{
				return;
			}
			float offered = SelectedValue();
			if (offered <= 0f)
			{
				Messages.Message("DreamsOutposts.TemporaryMarket.NothingSelected".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			if (offered > cap + 0.01f)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"DreamsOutposts.TemporaryMarket.OverCapConfirm".Translate(offered.ToStringMoney(), cap.ToStringMoney(), RewardCap.ToStringMoney()),
					Exchange,
					destructive: true));
				return;
			}
			Exchange();
		}

		private void Exchange()
		{
			if (exchanging)
			{
				return;
			}
			exchanging = true;
			giftedValue = 0f;
			for (int i = 0; i < transferables.Count; i++)
			{
				TransferableOneWay transferable = transferables[i];
				if (transferable == null || transferable.CountToTransfer <= 0 || transferable.AnyThing == null)
				{
					continue;
				}
				TransferableUtility.Transfer(transferable.things, transferable.CountToTransfer, TakeGift);
			}
			if (giftedValue <= 0f)
			{
				exchanging = false;
				Log.Error("[DreamsOutposts] Outpost temporary market took no goods from outpost " + outpost.Label + ", so nothing was exchanged.");
				return;
			}
			float reward = Mathf.Min(giftedValue, cap) * factor;
			if (reward <= 0f)
			{
				Log.Error("[DreamsOutposts] Outpost temporary market gave away goods worth " + giftedValue + " in outpost " + outpost.Label + " but the reward market value was not positive.");
			}
			OutpostEventContext context = new OutpostEventContext
			{
				outpost = outpost,
				itemRewards = new OutpostItemRewardCollector(outpost)
			};
			new OutpostEventEffect_GenerateRandomItems { marketValue = new FloatRange(reward, reward) }.Apply(context);
			context.itemRewards.Commit();
			SoundDefOf.Tick_High.PlayOneShotOnCamera();
			Messages.Message("DreamsOutposts.TemporaryMarket.Done".Translate(giftedValue.ToStringMoney(), reward.ToStringMoney()), outpost, MessageTypeDefOf.TaskCompletion, historical: false);
			Close();
		}

		/// <summary>取出所提供物资的那一件并记入赠送数值；送出多少就记多少。</summary>
		private void TakeGift(Thing piece, IThingHolder originalHolder)
		{
			if (piece == null || piece.Destroyed)
			{
				return;
			}
			giftedValue += piece.MarketValue * piece.stackCount;
			piece.Destroy();
		}

		private void ResetCounts()
		{
			for (int i = 0; i < transferables.Count; i++)
			{
				TransferableOneWay transferable = transferables[i];
				if (transferable != null)
				{
					transferable.ForceTo(0);
				}
			}
		}

		private float SelectedValue()
		{
			if (transferables == null)
			{
				return 0f;
			}
			float value = 0f;
			for (int i = 0; i < transferables.Count; i++)
			{
				TransferableOneWay transferable = transferables[i];
				if (transferable == null || transferable.CountToTransfer <= 0)
				{
					continue;
				}
				Thing thing = transferable.AnyThing;
				if (thing != null)
				{
					value += thing.MarketValue * transferable.CountToTransfer;
				}
			}
			return value;
		}

		private void RecalculateTransferables()
		{
			transferables = new List<TransferableOneWay>();
			List<Thing> stock = outpost.InventoryItems;
			for (int i = 0; i < stock.Count; i++)
			{
				Thing thing = stock[i];
				if (Eligible(thing))
				{
					OutpostAirdropUtility.AddToTransferables(thing, transferables);
				}
			}
			itemsTransfer = new TransferableOneWayWidget(transferables, null, null, "DreamsOutposts.TemporaryMarket.CountTip".Translate(), drawMass: false, IgnorePawnsInventoryMode.DontIgnore, includePawnsMassInMassUsage: false, null, 0f, ignoreSpawnedCorpseGearAndInventoryMass: false, outpost.Tile, drawMarketValue: true, drawEquippedWeapon: false, drawNutritionEatenPerDay: false, drawMechEnergy: false, drawItemNutrition: false, drawForagedFoodPerDay: false, drawDaysUntilRot: false);
		}

		/// <summary>
		/// 可交易范围：据点库存里的物品，市值大于零、可堆叠，且不是任务物品。
		/// 原版列表会额外显示单位市值，方便估算要提供多少。
		/// </summary>
		private static bool Eligible(Thing thing)
		{
			return thing != null
				&& !thing.Destroyed
				&& thing.def != null
				&& thing.def.category == ThingCategory.Item
				&& thing.def.stackLimit > 1
				&& thing.MarketValue > 0f
				&& thing.questTags.NullOrEmpty();
		}
	}
}
