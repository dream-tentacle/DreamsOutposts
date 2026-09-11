using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	public class Window_LoadAirdrop : Window
	{
		private enum Tab
		{
			Pawns,
			Items
		}

		private const float TitleRectHeight = 35f;

		private const float BottomAreaHeight = 55f;

		private const float BottomAreaOffset = 59f;

		private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

		private static readonly List<TabRecord> tabsList = new List<TabRecord>();

		private readonly Outpost outpost;

		private string title;

		private List<TransferableOneWay> transferables;

		private TransferableOneWayWidget pawnsTransfer;

		private TransferableOneWayWidget itemsTransfer;

		private Tab tab;

		private float lastMassFlashTime = -9999f;

		private bool launching;

		private bool massUsageDirty = true;

		private float cachedMassUsage;

		private bool tilesPerDayDirty = true;

		private float cachedTilesPerDay;

		private string cachedTilesPerDayExplanation;

		private bool daysWorthOfFoodDirty = true;

		private (float days, float tillRot) cachedDaysWorthOfFood;

		private bool foragedFoodPerDayDirty = true;

		private (ThingDef food, float perDay) cachedForagedFoodPerDay;

		private string cachedForagedFoodPerDayExplanation;

		private bool visibilityDirty = true;

		private float cachedVisibility;

		private string cachedVisibilityExplanation;

		public override Vector2 InitialSize => new Vector2(1024f, UI.screenHeight);

		protected override float Margin => 0f;

		private float MassCapacity => OutpostAirdropUtility.PodMassCapacity * (float)Mathf.Max(outpost?.airdropPods ?? 0, 0);

		private string MassCapacityExplanation => "DreamsOutposts.AirdropMassCapacityExplanation".Translate(OutpostAirdropUtility.PodMassCapacity.ToString("F0"), Mathf.Max(outpost?.airdropPods ?? 0, 0));

		private float MassUsage
		{
			get
			{
				if (massUsageDirty)
				{
					massUsageDirty = false;
					cachedMassUsage = OutpostAirdropUtility.MassUsage(transferables);
				}
				return cachedMassUsage;
			}
		}

		private float TilesPerDay
		{
			get
			{
				if (tilesPerDayDirty)
				{
					tilesPerDayDirty = false;
					StringBuilder stringBuilder = new StringBuilder();
					cachedTilesPerDay = TilesPerDayCalculator.ApproxTilesPerDay(transferables, MassUsage, MassCapacity, outpost.Tile, PlanetTile.Invalid, isShuttle: false, stringBuilder);
					cachedTilesPerDayExplanation = stringBuilder.ToString();
				}
				return cachedTilesPerDay;
			}
		}

		private (float days, float tillRot) DaysWorthOfFood
		{
			get
			{
				if (daysWorthOfFoodDirty)
				{
					daysWorthOfFoodDirty = false;
					float days = DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood(transferables, outpost.Tile, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, Faction.OfPlayer);
					float tillRot = DaysUntilRotCalculator.ApproxDaysUntilRot(transferables, outpost.Tile, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload);
					cachedDaysWorthOfFood = (days: days, tillRot: tillRot);
				}
				return cachedDaysWorthOfFood;
			}
		}

		private (ThingDef food, float perDay) ForagedFoodPerDay
		{
			get
			{
				if (foragedFoodPerDayDirty)
				{
					foragedFoodPerDayDirty = false;
					BiomeDef biome = outpost.Biome;
					if (biome == null)
					{
						cachedForagedFoodPerDay = (food: null, perDay: 0f);
						cachedForagedFoodPerDayExplanation = null;
					}
					else
					{
						StringBuilder stringBuilder = new StringBuilder();
						cachedForagedFoodPerDay = ForagedFoodPerDayCalculator.ForagedFoodPerDay(transferables, biome, Faction.OfPlayer, stringBuilder);
						cachedForagedFoodPerDayExplanation = stringBuilder.ToString();
					}
				}
				return cachedForagedFoodPerDay;
			}
		}

		private float Visibility
		{
			get
			{
				if (visibilityDirty)
				{
					visibilityDirty = false;
					StringBuilder stringBuilder = new StringBuilder();
					cachedVisibility = CaravanVisibilityCalculator.Visibility(transferables, stringBuilder);
					cachedVisibilityExplanation = stringBuilder.ToString();
				}
				return cachedVisibility;
			}
		}

		public Window_LoadAirdrop(Outpost outpost)
		{
			this.outpost = outpost;
			forcePause = true;
			absorbInputAroundWindow = true;
		}

		public override void PostOpen()
		{
			base.PostOpen();
			title = "LoadTransporters".Translate(Find.ActiveLanguageWorker.Pluralize(ThingDefOf.TransportPod.label)).CapitalizeFirst();
			CalculateAndRecacheTransferables();
		}

		public override bool CausesMessageBackground()
		{
			return true;
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
			Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			CaravanUIUtility.DrawCaravanInfo(new CaravanUIUtility.CaravanInfo(MassUsage, MassCapacity, MassCapacityExplanation, TilesPerDay, cachedTilesPerDayExplanation, DaysWorthOfFood, ForagedFoodPerDay, cachedForagedFoodPerDayExplanation, Visibility, cachedVisibilityExplanation), null, outpost.Tile, null, lastMassFlashTime, new Rect(12f, 35f, inRect.width - 24f, 40f));
			tabsList.Clear();
			tabsList.Add(new TabRecord("PawnsTab".Translate(), delegate
			{
				tab = Tab.Pawns;
			}, tab == Tab.Pawns));
			tabsList.Add(new TabRecord("ItemsTab".Translate(), delegate
			{
				tab = Tab.Items;
			}, tab == Tab.Items));
			inRect.yMin += 119f;
			Widgets.DrawMenuSection(inRect);
			TabDrawer.DrawTabs(inRect, tabsList);
			inRect = inRect.ContractedBy(17f);
			Widgets.BeginGroup(inRect);
			Rect contentRect = inRect.AtZero();
			DoBottomButtons(contentRect);
			if (launching)
			{
				Widgets.EndGroup();
				return;
			}
			Rect listRect = contentRect;
			listRect.yMax -= 59f;
			bool anythingChanged = false;
			switch (tab)
			{
			case Tab.Pawns:
				pawnsTransfer.OnGUI(listRect, out anythingChanged);
				break;
			case Tab.Items:
				itemsTransfer.OnGUI(listRect, out anythingChanged);
				break;
			}
			if (anythingChanged)
			{
				Notify_TransferablesChanged();
			}
			Widgets.EndGroup();
		}

		public override void OnAcceptKeyPressed()
		{
			OnAccept();
		}

		private void DoBottomButtons(Rect rect)
		{
			float y = rect.height - 55f;
			if (Widgets.ButtonText(new Rect(0f, y, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
			{
				Close();
			}
			if (Widgets.ButtonText(new Rect(rect.width / 2f - BottomButtonSize.x / 2f, y, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
			{
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();
				CalculateAndRecacheTransferables();
			}
			if (Widgets.ButtonText(new Rect(rect.width - BottomButtonSize.x, y, BottomButtonSize.x, BottomButtonSize.y), "AcceptButton".Translate()))
			{
				OnAccept();
			}
		}

		private void OnAccept()
		{
			if (!launching)
			{
				if (outpost == null || outpost.Destroyed)
				{
					Close();
					return;
				}
				if (!transferables.Any((TransferableOneWay t) => t.CountToTransfer != 0))
				{
					Messages.Message("CantSendEmptyTransportPods".Translate(), MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				if (MassUsage > MassCapacity)
				{
					lastMassFlashTime = Time.time;
					Messages.Message("TooBigTransportersMassUsage".Translate(), MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				launching = true;
				SoundDefOf.Tick_High.PlayOneShotOnCamera();
				Close(doCloseSound: false);
				OutpostAirdropUtility.BeginTargeting(outpost, transferables);
			}
		}

		private void CalculateAndRecacheTransferables()
		{
			transferables = new List<TransferableOneWay>();
			OutpostAirdropUtility.FillTransferables(outpost, transferables);
			pawnsTransfer = new TransferableOneWayWidget(null, null, null, "TransporterColonyThingCountTip".Translate(), drawMass: true, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMassInMassUsage: true, () => MassCapacity - MassUsage, 0f, ignoreSpawnedCorpseGearAndInventoryMass: false, outpost.Tile, drawMarketValue: true, drawEquippedWeapon: true, drawNutritionEatenPerDay: true, drawMechEnergy: false, drawItemNutrition: false, drawForagedFoodPerDay: true);
			CaravanUIUtility.AddPawnsSections(pawnsTransfer, transferables);
			itemsTransfer = new TransferableOneWayWidget(transferables.Where((TransferableOneWay x) => x.ThingDef.category != ThingCategory.Pawn), null, null, "TransporterColonyThingCountTip".Translate(), drawMass: true, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, includePawnsMassInMassUsage: true, () => MassCapacity - MassUsage, 0f, ignoreSpawnedCorpseGearAndInventoryMass: false, outpost.Tile, drawMarketValue: true, drawEquippedWeapon: false, drawNutritionEatenPerDay: false, drawMechEnergy: false, drawItemNutrition: true, drawForagedFoodPerDay: false, drawDaysUntilRot: true);
			Notify_TransferablesChanged();
		}

		private void Notify_TransferablesChanged()
		{
			massUsageDirty = true;
			tilesPerDayDirty = true;
			daysWorthOfFoodDirty = true;
			foragedFoodPerDayDirty = true;
			visibilityDirty = true;
		}
	}
}
