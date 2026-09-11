using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	public class Window_FormCaravanFromOutpost : Window
	{
		private enum Tab
		{
			Pawns,
			Items,
			TravelSupplies
		}

		private const float TitleRectHeight = 35f;

		private const float BottomAreaHeight = 55f;

		private const float BottomAreaOffset = 59f;

		private static readonly Vector2 BottomButtonSize = new Vector2(160f, 40f);

		private static readonly List<TabRecord> tabsList = new List<TabRecord>();

		private readonly Outpost outpost;

		private List<TransferableOneWay> transferables;

		private TransferableOneWayWidget pawnsTransfer;

		private TransferableOneWayWidget itemsTransfer;

		private TransferableOneWayWidget travelSuppliesTransfer;

		private Tab tab;

		private float lastMassFlashTime = -9999f;

		private bool formed;

		private bool massUsageDirty = true;

		private float cachedMassUsage;

		private bool massCapacityDirty = true;

		private float cachedMassCapacity;

		private string cachedMassCapacityExplanation;

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

		private float MassUsage
		{
			get
			{
				if (massUsageDirty)
				{
					massUsageDirty = false;
					cachedMassUsage = CollectionsMassCalculator.MassUsageTransferables(transferables, IgnorePawnsInventoryMode.Ignore);
				}
				return cachedMassUsage;
			}
		}

		private float MassCapacity
		{
			get
			{
				if (massCapacityDirty)
				{
					massCapacityDirty = false;
					StringBuilder stringBuilder = new StringBuilder();
					cachedMassCapacity = CollectionsMassCalculator.CapacityTransferables(transferables, stringBuilder);
					cachedMassCapacityExplanation = stringBuilder.ToString();
				}
				return cachedMassCapacity;
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
					float days = DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood(transferables, outpost.Tile, IgnorePawnsInventoryMode.Ignore, Faction.OfPlayer);
					float tillRot = DaysUntilRotCalculator.ApproxDaysUntilRot(transferables, outpost.Tile, IgnorePawnsInventoryMode.Ignore);
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

		public Window_FormCaravanFromOutpost(Outpost outpost)
		{
			this.outpost = outpost;
			forcePause = true;
			absorbInputAroundWindow = true;
		}

		public override void PostOpen()
		{
			base.PostOpen();
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
			Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "FormCaravan".Translate());
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			CaravanUIUtility.DrawCaravanInfo(new CaravanUIUtility.CaravanInfo(MassUsage, MassCapacity, cachedMassCapacityExplanation, TilesPerDay, cachedTilesPerDayExplanation, DaysWorthOfFood, ForagedFoodPerDay, cachedForagedFoodPerDayExplanation, Visibility, cachedVisibilityExplanation), null, outpost.Tile, null, lastMassFlashTime, new Rect(12f, 35f, inRect.width - 24f, 40f));
			tabsList.Clear();
			tabsList.Add(new TabRecord("PawnsTab".Translate(), delegate
			{
				tab = Tab.Pawns;
			}, tab == Tab.Pawns));
			tabsList.Add(new TabRecord("ItemsTab".Translate(), delegate
			{
				tab = Tab.Items;
			}, tab == Tab.Items));
			tabsList.Add(new TabRecord("TravelSupplies".Translate(), delegate
			{
				tab = Tab.TravelSupplies;
			}, tab == Tab.TravelSupplies));
			inRect.yMin += 119f;
			Widgets.DrawMenuSection(inRect);
			TabDrawer.DrawTabs(inRect, tabsList);
			inRect = inRect.ContractedBy(17f);
			Widgets.BeginGroup(inRect);
			Rect contentRect = inRect.AtZero();
			DoBottomButtons(contentRect);
			if (formed)
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
			case Tab.TravelSupplies:
				travelSuppliesTransfer.OnGUI(listRect, out anythingChanged);
				break;
			}
			if (anythingChanged)
			{
				Notify_TransferablesChanged();
			}
			Widgets.EndGroup();
		}

		private void DoBottomButtons(Rect rect)
		{
			Rect acceptRect = new Rect(rect.width / 2f - BottomButtonSize.x / 2f, rect.height - 55f, BottomButtonSize.x, BottomButtonSize.y);
			if (Widgets.ButtonText(acceptRect, "AcceptButton".Translate()))
			{
				if (OutpostCaravanUtility.TryFormCaravan(outpost, transferables, out var created))
				{
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					formed = true;
					Close(doCloseSound: false);
					CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(created));
				}
				else
				{
					lastMassFlashTime = Time.time;
				}
			}
			if (Widgets.ButtonText(new Rect(acceptRect.x - 10f - BottomButtonSize.x, acceptRect.y, BottomButtonSize.x, BottomButtonSize.y), "ResetButton".Translate()))
			{
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();
				CalculateAndRecacheTransferables();
			}
			if (Widgets.ButtonText(new Rect(acceptRect.xMax + 10f, acceptRect.y, BottomButtonSize.x, BottomButtonSize.y), "CancelButton".Translate()))
			{
				Close();
			}
		}

		private void CalculateAndRecacheTransferables()
		{
			transferables = new List<TransferableOneWay>();
			OutpostCaravanUtility.FillTransferables(outpost, transferables);
			CaravanUIUtility.CreateCaravanTransferableWidgets(transferables, out pawnsTransfer, out itemsTransfer, out travelSuppliesTransfer, "FormCaravanColonyThingCountTip".Translate(), IgnorePawnsInventoryMode.Ignore, () => MassCapacity - MassUsage, ignoreSpawnedCorpsesGearAndInventoryMass: false, outpost.Tile);
			Notify_TransferablesChanged();
		}

		private void Notify_TransferablesChanged()
		{
			massUsageDirty = true;
			massCapacityDirty = true;
			tilesPerDayDirty = true;
			daysWorthOfFoodDirty = true;
			foragedFoodPerDayDirty = true;
			visibilityDirty = true;
		}
	}
}
