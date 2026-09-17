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
			Vehicles,
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

		/// <summary>载具拓展的载具卡片控件（反射创建），null 表示本窗口不提供载具页。</summary>
		private object vehiclesTransfer;

		private bool vehiclesTabEnabled;

		/// <summary>窗口打开时据点里的载具，用来在关窗/重置时清掉座位分配。</summary>
		private readonly List<Pawn> trackedVehicles = new List<Pawn>();

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
			// 载具拓展的载具卡片与座位窗口都依赖全局静态 CaravanFormation.Current，
			// 这里临时给它装一个代理，关窗时还原。
			vehiclesTabEnabled = VehicleCaravanCompat.TryBeginContext(Notify_TransferablesChanged);
			CalculateAndRecacheTransferables();
			SelectInitialTab();
		}

		/// <summary>
		/// 默认页与载具拓展自己的组建窗口保持一致：据点里真有载具可编入时才开在载具页，否则开在小人页。
		/// 载具页不可用时绝不能停在载具页，否则会画出一个只剩数量提示的空页面。
		/// </summary>
		private void SelectInitialTab()
		{
			tab = Tab.Pawns;
			if (!vehiclesTabEnabled)
			{
				return;
			}
			for (int i = 0; i < transferables.Count; i++)
			{
				if (transferables[i] != null && VehicleCaravanCompat.IsVehicle(transferables[i].AnyThing as Pawn))
				{
					tab = Tab.Vehicles;
					return;
				}
			}
		}

		public override void PostClose()
		{
			base.PostClose();
			ReleaseVehicleState();
		}

		private void ReleaseVehicleState()
		{
			if (trackedVehicles.Count > 0)
			{
				VehicleCaravanCompat.ClearAssignments(trackedVehicles);
				trackedVehicles.Clear();
			}
			VehicleCaravanCompat.EndContext();
			vehiclesTabEnabled = false;
			vehiclesTransfer = null;
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
			// 载具卡片可能在重置/重算时创建失败（RecacheVehiclesTransfer 会把 vehiclesTabEnabled 置回 false），
			// 这时必须把当前页挪回小人页，否则载具页的标签已经消失、内容却还在画。
			if (!vehiclesTabEnabled && tab == Tab.Vehicles)
			{
				tab = Tab.Pawns;
			}
			if (vehiclesTabEnabled)
			{
				// 装了载具框架就无条件显示载具页，标签沿用载具拓展自己的键，与原版组建窗口一致。
				tabsList.Add(new TabRecord("VF_Vehicles".Translate(), delegate
				{
					tab = Tab.Vehicles;
				}, tab == Tab.Vehicles));
			}
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
			case Tab.Vehicles:
				DoVehiclesTab(listRect);
				break;
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

		/// <summary>载具拓展的载具卡片 + 一行编入数量提示（据点远行队一次只允许一台载具）。</summary>
		private void DoVehiclesTab(Rect listRect)
		{
			const float hintHeight = 24f;
			List<Pawn> selectedVehicles = OutpostCaravanUtility.CollectSelectedVehicles(transferables);
			bool tooMany = selectedVehicles.Count > OutpostCaravanUtility.MaxVehiclesPerCaravan;
			Color previous = GUI.color;
			if (tooMany)
			{
				GUI.color = ColorLibrary.RedReadable;
			}
			else
			{
				GUI.color = Color.gray;
			}
			Widgets.Label(new Rect(listRect.x, listRect.y, listRect.width, hintHeight), "DreamsOutposts.OneVehiclePerCaravanHint".Translate(OutpostCaravanUtility.MaxVehiclesPerCaravan));
			GUI.color = previous;
			Rect cardsRect = listRect;
			cardsRect.yMin += hintHeight;
			VehicleCaravanCompat.DrawVehicleWidget(vehiclesTransfer, cardsRect);
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
				VehicleCaravanCompat.ClearAssignments(trackedVehicles);
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
			RecacheVehiclesTransfer();
			Notify_TransferablesChanged();
		}

		/// <summary>
		/// 拆出载具/人员 transferable，交给载具拓展自己的载具卡片组件；没有载具时它也只会显示「无」，
		/// 这样装了载具框架的据点界面与原版组建窗口保持一致。
		/// </summary>
		private void RecacheVehiclesTransfer()
		{
			vehiclesTransfer = null;
			trackedVehicles.Clear();
			if (!vehiclesTabEnabled)
			{
				return;
			}
			List<TransferableOneWay> vehicleTransferables = new List<TransferableOneWay>();
			List<TransferableOneWay> pawnTransferables = new List<TransferableOneWay>();
			for (int i = 0; i < transferables.Count; i++)
			{
				Thing anyThing = transferables[i].AnyThing;
				if (VehicleCaravanCompat.IsVehicle(anyThing as Pawn))
				{
					vehicleTransferables.Add(transferables[i]);
				}
				else if (anyThing is Pawn)
				{
					pawnTransferables.Add(transferables[i]);
				}
			}
			trackedVehicles.AddRange(outpost.VehiclesListForReading);
			vehiclesTransfer = VehicleCaravanCompat.CreateVehicleWidget("VF_Vehicles".Translate(), vehicleTransferables, pawnTransferables, outpost.Tile);
			if (vehiclesTransfer == null)
			{
				vehiclesTabEnabled = false;
				return;
			}
			// 载具卡片会按据点地块的通行性给勾选框上锁（"该生物群系无法被载具通过"）；
			// 能起飞的载具是停放状态，地势通不通与它能否出发无关，这里只对这些型号解锁。
			VehicleCaravanCompat.AllowLaunchableVehiclesOnTile(vehiclesTransfer, vehicleTransferables);
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
