using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 「设施」页：等级卡 + 核心设施 + 扩建设施槽位。
	/// 只负责画，所有数据来自 OutpostUiCache。
	/// </summary>
	public class Page_OutpostFacilities : OutpostManagePage, IUiShellPage
	{
		private const float SectionGap = UiMetrics.SectionSpacing;

		private const float CardHoverShadowAlpha = 0.7f;

		private static readonly string[] HelpSections =
		{
			"DreamsOutposts.Ui.Help.Facilities",
			"DreamsOutposts.Ui.Help.WorkSpeed"
		};

		private OutpostUiCache fallbackCache;

		private int observedLevel = -1;

		private float upgradeFlashStartedAt = float.NegativeInfinity;

		/// <summary>刚建成的扩展槽位：套用和等级卡同款的闪光；未触发过时槽位号是 -1。</summary>
		private string[] observedSlotFacilities;

		private int builtFlashSlotIndex = -1;

		private float builtFlashStartedAt = float.NegativeInfinity;

		/// <summary>滚动画布的可见上下边（视口组内坐标系），用于挡掉滚出可视区的卡片点击。</summary>
		private float viewportTop;

		private float viewportBottom;

		private Window_OutpostManage Shell => hostWindow as Window_OutpostManage;

		private OutpostUiCache Cache
		{
			get
			{
				Window_OutpostManage shell = Shell;
				if (shell != null)
				{
					return shell.Cache;
				}
				if (fallbackCache == null || fallbackCache.Outpost != outpost)
				{
					fallbackCache = new OutpostUiCache(outpost);
				}
				fallbackCache.Refresh();
				return fallbackCache;
			}
		}

		public string NavSummary
		{
			get
			{
				OutpostUiCache cache = Cache;
				return "DreamsOutposts.Ui.Nav.Slots".Translate(cache.UsedSlots, cache.SlotCount).ToString();
			}
		}

		public string HeadDescription => null;

		public string HeadHint => null;

		public float BodyHeight(float width, float availableHeight)
		{
			return Layout(new Rect(0f, 0f, width, 0f), false);
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			// 鼠标坐标相对于视口组；rect.y 只是内容的滚动偏移，不能用于视口边界。
			viewportTop = 0f;
			viewportBottom = availableHeight;
			Layout(rect, true);
		}

		// ---------------------------------------------------------------

		private float Layout(Rect rect, bool draw)
		{
			switch (DreamsOutpostsMod.UiStyle)
			{
			case OutpostUiStyle.Vanilla:
				return LayoutVanillaNative(rect, draw);

			// 黑夜风与现代科技风共用同一套布局，差别只在配色。
			case OutpostUiStyle.ModernTechDark:
			case OutpostUiStyle.ModernTech:
			default:
				return LayoutModernTech(rect, draw);
			}
		}

		/// <summary>
		/// 原版风设施页：使用原版 MenuSection + 普通列表行，不复用现代风卡片/网格视觉。
		/// </summary>
		private float LayoutVanillaNative(Rect rect, bool draw)
		{
			OutpostUiCache cache = Cache;
			if (rect.width < 80f)
			{
				return 1f;
			}
			const float gap = 18f;
			float y = rect.y;
			float overviewHeight = VanillaOverviewHeight(cache);
			if (draw) DrawVanillaOverview(new Rect(rect.x, y, rect.width, overviewHeight), cache);
			y += overviewHeight + gap;

			float coreHeight = 32f + VanillaFacilityRowHeight(cache.Core);
			if (draw)
			{
				Widgets.DrawLineHorizontal(rect.x, y - gap * 0.5f, rect.width, Widgets.SeparatorLineColor);
				DrawVanillaCoreSection(new Rect(rect.x, y, rect.width, coreHeight), cache);
			}
			y += coreHeight + gap;

			float extensionHeight = 32f;
			if (cache.SlotCount == 0)
			{
				extensionHeight += 54f;
			}
			else
			{
				for (int i = 0; i < cache.SlotCount; i++)
				{
					UiFacilityView view = (i < cache.Slots.Count) ? cache.Slots[i] : null;
					extensionHeight += view != null ? VanillaFacilityRowHeight(view) : 54f;
				}
			}
			if (draw)
			{
				Widgets.DrawLineHorizontal(rect.x, y - gap * 0.5f, rect.width, Widgets.SeparatorLineColor);
				DrawVanillaExtensionSection(new Rect(rect.x, y, rect.width, extensionHeight), cache);
			}
			y += extensionHeight;
			return Mathf.Max(y - rect.y, 1f);
		}

		private static float VanillaOverviewHeight(OutpostUiCache cache)
		{
			if (cache.Upgrade.IsMaxLevel)
			{
				return 92f;
			}
			return 32f + 28f + cache.Upgrade.Checks.Count * 30f + 40f;
		}

		private void DrawVanillaOverview(Rect rect, OutpostUiCache cache)
		{
			Rect inner = rect;
			float y = inner.y;
			string levelText = "DreamsOutposts.Level".Translate(outpost.level, cache.MaxLevel);
			UiText.Draw(new Rect(inner.x, y, inner.width, 28f), levelText,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			Widgets.DrawLineHorizontal(inner.x, y + 30f, inner.width, Widgets.SeparatorLineColor);
			y += 32f;
			float half = inner.width * 0.5f;
			UiText.Draw(new Rect(inner.x, y, half, 28f), cache.DaysText,
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			UiText.Draw(new Rect(inner.x + half, y, half, 28f),
				"DreamsOutposts.Ui.Chip.CurrentSlots".Translate(outpost.SlotCountForLevel),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
			y += 28f;
			Widgets.DrawLineHorizontal(inner.x, y, inner.width, Widgets.SeparatorLineColor);
			y += 4f;

			if (cache.Upgrade.IsMaxLevel)
			{
				UiText.Draw(new Rect(inner.x, y, inner.width, 28f), cache.Upgrade.MaxLevelText,
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				return;
			}

			for (int i = 0; i < cache.Upgrade.Checks.Count; i++)
			{
				UiUpgradeCheck check = cache.Upgrade.Checks[i];
				Rect row = new Rect(inner.x, y, inner.width, 30f);
				Widgets.DrawHighlightIfMouseover(row);
				float valueWidth = Mathf.Min(180f, row.width * 0.42f);
				UiText.Draw(new Rect(row.x + 4f, row.y, Mathf.Max(row.width - valueWidth - 8f, 40f), row.height), check.Name,
					UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
				UiText.Draw(new Rect(row.xMax - valueWidth - 4f, row.y, valueWidth, row.height), check.ValueText,
					UiFont.Body, check.Ok ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleRight, check.Ok);
				y += 30f;
			}

			string buttonLabel = "DreamsOutposts.Ui.Upgrade.Button".Translate().ToString();
			float buttonWidth = Mathf.Min(220f, inner.width);
			Rect button = new Rect(inner.center.x - buttonWidth * 0.5f, y + 4f, buttonWidth, 32f);
			string tip = cache.Upgrade.CanUpgrade
				? "DreamsOutposts.Ui.Upgrade.ConfirmTip".Translate().ToString()
				: cache.Upgrade.Reason;
			if (UiWidgets.Button(button, buttonLabel, UiButtonKind.Secondary, cache.Upgrade.CanUpgrade,
				cache.Upgrade.Reason, UiButtonSize.Normal, tip))
			{
				if (OutpostUpgradeUtility.TryUpgrade(outpost))
				{
					Window_OutpostManage shell = Shell;
					if (shell != null) shell.Cache.Invalidate();
				}
			}
		}

		private void DrawVanillaCoreSection(Rect rect, OutpostUiCache cache)
		{
			Rect inner = rect;
			string title = "DreamsOutposts.CoreFacility".Translate();
			const float infoSize = Widgets.InfoCardButtonSize;
			float infoX = inner.x + UiText.Width(title, UiFont.Body, true) + 6f;
			UiText.Draw(new Rect(inner.x, inner.y, Mathf.Max(infoX - inner.x, 40f), 28f), title,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			Rect infoRect = new Rect(infoX, inner.y + (28f - infoSize) * 0.5f, infoSize, infoSize);
			if (Widgets.ButtonImage(infoRect, TexButton.Info))
			{
				UiOutpostHelpWindow.Open(HelpSections);
			}
			Widgets.DrawLineHorizontal(inner.x, inner.y + 30f, inner.width, Widgets.SeparatorLineColor);
			Rect row = new Rect(inner.x, inner.y + 32f, inner.width, VanillaFacilityRowHeight(cache.Core));
			if (cache.Core != null)
			{
				DrawVanillaFacilityRow(row, cache.Core);
			}
			else
			{
				UiText.Draw(row, "DreamsOutposts.FacilityNoDef".Translate(), UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
			}
		}

		private void DrawVanillaExtensionSection(Rect rect, OutpostUiCache cache)
		{
			Rect inner = rect;
			UiText.Draw(new Rect(inner.x, inner.y, inner.width, 28f),
				"DreamsOutposts.ExtensionFacilities".Translate(cache.UsedSlots, cache.SlotCount),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			Widgets.DrawLineHorizontal(inner.x, inner.y + 30f, inner.width, Widgets.SeparatorLineColor);
			float y = inner.y + 32f;
			if (cache.SlotCount == 0)
			{
				UiText.Draw(new Rect(inner.x, y, inner.width, 54f), "DreamsOutposts.NoExtensionSlots".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}
			for (int i = 0; i < cache.SlotCount; i++)
			{
				UiFacilityView view = (i < cache.Slots.Count) ? cache.Slots[i] : null;
				float rowHeight = view != null ? VanillaFacilityRowHeight(view) : 54f;
				Rect row = new Rect(inner.x, y, inner.width, rowHeight);
				if (view != null)
				{
					DrawVanillaFacilityRow(row, view);
				}
				else
				{
					DrawVanillaEmptySlotRow(row, i);
				}
				y += rowHeight;
			}
		}

		private static float VanillaFacilityRowHeight(UiFacilityView view)
		{
			return VanillaProgressInfo(view) != null ? 78f : 54f;
		}

		private static UiFacilityInfoItem VanillaProgressInfo(UiFacilityView view)
		{
			if (view?.InfoGroups == null)
			{
				return null;
			}
			for (int groupIndex = 0; groupIndex < view.InfoGroups.Count; groupIndex++)
			{
				UiFacilityInfoGroup group = view.InfoGroups[groupIndex];
				for (int itemIndex = 0; itemIndex < (group?.Items?.Count ?? 0); itemIndex++)
				{
					UiFacilityInfoItem item = group.Items[itemIndex];
					if (item != null && item.Kind == UiFacilityInfoKind.Progress)
					{
						return item;
					}
				}
			}
			return null;
		}

		private void DrawVanillaFacilityRow(Rect row, UiFacilityView view)
		{
			Widgets.DrawHighlightIfMouseover(row);
			const float iconSize = 30f;
			Rect icon = new Rect(row.x + 6f, row.y + (row.height - iconSize) * 0.5f, iconSize, iconSize);
			UiDraw.Icon(icon, view.Icon, UiPalette.Ink);

			UiProductionView configurableProduction = VanillaConfigurableProduction(view);
			const float infoSize = Widgets.InfoCardButtonSize;
			float infoX = row.xMax - infoSize - 4f;
			OutpostFacilityComp_Slaughterhouse slaughterhouse = view.Facility?.GetComp<OutpostFacilityComp_Slaughterhouse>();
			string switchLabel = slaughterhouse?.ToggleLabel ?? "DreamsOutposts.Ui.Switch".Translate().ToString();
			bool hasSwitch = configurableProduction != null || slaughterhouse != null;
			float switchWidth = hasSwitch
				? Mathf.Clamp(UiText.Width(switchLabel, UiFont.Body) + 24f, 64f, 160f)
				: 0f;
			float switchX = hasSwitch ? infoX - switchWidth - 6f : infoX;
			float defenseWidth = view.Defense > 0f ? 90f : 0f;
			float defenseX = view.Defense > 0f ? switchX - defenseWidth - 6f : switchX;
			float textRight = view.Defense > 0f ? defenseX : switchX;
			float textX = icon.xMax + 10f;
			float textWidth = Mathf.Max(textRight - textX - 6f, 40f);

			UiText.Draw(new Rect(textX, row.y + 5f, textWidth, 22f), view.Label,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			string secondary = VanillaFacilitySummary(view);
			UiText.Draw(new Rect(textX, row.y + 27f, textWidth, 20f), secondary,
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			UiFacilityInfoItem progressInfo = VanillaProgressInfo(view);
			if (progressInfo != null)
			{
				Rect progressRect = new Rect(textX, row.y + 53f, textWidth, 18f);
				Widgets.FillableBar(progressRect, Mathf.Clamp01(progressInfo.Progress));
				UiText.Draw(
					progressRect,
					progressInfo.ProgressText ?? string.Empty,
					UiFont.Caption,
					UiPalette.Ink,
					TextAnchor.MiddleCenter,
					true,
					false,
					true);
				if (!string.IsNullOrEmpty(progressInfo.Tooltip))
				{
					UiWidgets.Tip(
						progressRect,
						progressInfo.Tooltip,
						GenText.StableStringHash("vanilla-facility-progress-" + view.SlotIndex));
				}
			}
			if (view.Defense > 0f)
			{
				UiText.Draw(new Rect(defenseX, row.y, defenseWidth, row.height),
					"DreamsOutposts.Defense".Translate().ToString() + " " + view.Defense.ToString("0.#"),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight);
			}
			if (slaughterhouse != null)
			{
				Rect switchRect = new Rect(switchX, row.y + 11f, switchWidth, 32f);
				TooltipHandler.TipRegion(switchRect, "DreamsOutposts.Slaughterhouse.Tooltip".Translate());
				if (Widgets.ButtonText(switchRect, switchLabel)) slaughterhouse.TogglePaused(outpost);
			}
			else if (configurableProduction != null && view.Facility != null)
			{
				Rect switchRect = new Rect(switchX, row.y + 11f, switchWidth, 32f);
				string tip = configurableProduction.Props.Worker.ConfigurationTip(configurableProduction.Props);
				TooltipHandler.TipRegion(switchRect, new TipSignal(tip, GenText.StableStringHash("vanilla-production-config-" + view.SlotIndex + "-" + configurableProduction.Props.id)));
				if (Widgets.ButtonText(switchRect, switchLabel))
				{
					configurableProduction.Props.Worker.OpenConfiguration(
						configurableProduction.Props,
						view.Facility.GetProductionState(configurableProduction.Props.id),
						Cache.Invalidate);
				}
			}
			if (view.Facility != null)
			{
				Rect infoRect = new Rect(infoX, row.y + (row.height - infoSize) * 0.5f, infoSize, infoSize);
				if (Widgets.ButtonImage(infoRect, TexButton.Info))
				{
					Window_OutpostManage shell = Shell;
					if (shell != null) shell.OpenDetailsModal(view);
				}
			}
		}

		private static UiProductionView VanillaConfigurableProduction(UiFacilityView view)
		{
			if (view?.Productions == null)
			{
				return null;
			}
			for (int i = 0; i < view.Productions.Count; i++)
			{
				UiProductionView production = view.Productions[i];
				if (production != null && production.HasConfiguration && production.Props?.Worker != null)
				{
					return production;
				}
			}
			return null;
		}

		private static string VanillaFacilitySummary(UiFacilityView view)
		{
			UiProductionView configurableProduction = VanillaConfigurableProduction(view);
			if (configurableProduction != null && !string.IsNullOrEmpty(configurableProduction.ConfigurationSummary))
			{
				return configurableProduction.ConfigurationSummary;
			}
			if (view?.InfoGroups != null)
			{
				for (int groupIndex = 0; groupIndex < view.InfoGroups.Count; groupIndex++)
				{
					UiFacilityInfoGroup group = view.InfoGroups[groupIndex];
					for (int itemIndex = 0; itemIndex < (group?.Items?.Count ?? 0); itemIndex++)
					{
						UiFacilityInfoItem item = group.Items[itemIndex];
						if (item == null) continue;
						if (item.Kind == UiFacilityInfoKind.Progress && !string.IsNullOrEmpty(item.LeftText))
						{
							return item.LeftText;
						}
						if (item.Importance == UiFacilityInfoImportance.Supporting &&
							item.Kind == UiFacilityInfoKind.Value &&
							!string.IsNullOrEmpty(item.Value))
						{
							return item.Value;
						}
					}
				}
			}
			if (!string.IsNullOrEmpty(view.SubLabel))
			{
				return view.SubLabel;
			}
			if (view.Productions.Count > 0)
			{
				UiProductionView production = view.Productions[0];
				return production.ProductLabel + " ×" + production.Output.ToString("0.#") + " / " + production.IntervalText;
			}
			return view.Description ?? string.Empty;
		}

		private void DrawVanillaEmptySlotRow(Rect row, int index)
		{
			Widgets.DrawHighlightIfMouseover(row);
			float buttonWidth = Mathf.Min(160f, row.width * 0.32f);
			Rect button = new Rect(row.xMax - buttonWidth - 6f, row.y + 11f, buttonWidth, 32f);
			UiText.Draw(new Rect(row.x + 8f, row.y, Mathf.Max(button.x - row.x - 16f, 40f), row.height),
				"DreamsOutposts.Ui.EmptySlotHint".Translate(index + 1),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			if (Widgets.ButtonText(button, "DreamsOutposts.InstallFacility".Translate()))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null && outpost.extensionSlots != null && index >= 0 && index < outpost.extensionSlots.Count)
				{
					shell.OpenInstallModal(outpost.extensionSlots[index], index);
				}
			}
		}

		/// <summary>
		/// 旧版原版风设施布局，保留供对照；当前 Vanilla 已切换到上面的原版列表式布局。
		/// </summary>
		private float LayoutVanilla(Rect rect, bool draw)
		{
			OutpostUiCache cache = Cache;
			if (draw)
			{
				ObserveLevelChange();
				ObserveBuiltFacility(cache);
			}
			float width = rect.width;
			if (width < 80f)
			{
				return 1f;
			}
			float y = rect.y;
			// 等级卡
			float levelHeight = MeasureLevelCard(width, cache);
			if (draw)
			{
				DrawLevelCard(new Rect(rect.x, y, width, levelHeight), cache);
			}
			y += levelHeight + SectionGap;
			// 核心设施
			y += SectionHead(rect.x, y, width, "DreamsOutposts.CoreFacility".Translate(),
				null, draw, true);
			UiFacilityView core = cache.Core;
			if (core != null)
			{
				float cardHeight = MeasureFacilityCard(core, width);
				if (draw)
				{
					DrawFacilityCard(new Rect(rect.x, y, width, cardHeight), core);
				}
				y += cardHeight;
			}
			else
			{
				if (draw)
				{
					DrawEmptyHintRow(new Rect(rect.x, y, width, 0f), "DreamsOutposts.FacilityNoDef".Translate());
				}
				y += EmptyHintRowHeight();
			}
			// 扩建设施
			y += SectionGap;
			y += SectionHead(rect.x, y, width, "DreamsOutposts.ExtensionFacilities".Translate(cache.UsedSlots, cache.SlotCount),
				"DreamsOutposts.Ui.ExtensionHint".Translate(), draw);
			if (cache.SlotCount == 0)
			{
				if (draw)
				{
					DrawEmptyHintRow(new Rect(rect.x, y, width, 0f), "DreamsOutposts.NoExtensionSlots".Translate());
				}
				y += EmptyHintRowHeight();
			}
			else
			{
				int columns = UiMetrics.GridColumns(width, UiMetrics.SlotGridMinCell, UiMetrics.SlotGridGap);
				float cellWidth = UiMetrics.GridCellWidth(width, columns, UiMetrics.SlotGridGap);
				int rowCount = Mathf.CeilToInt((float)cache.Slots.Count / columns);
				for (int row = 0; row < rowCount; row++)
				{
					float rowHeight = 0f;
					for (int column = 0; column < columns; column++)
					{
						int index = row * columns + column;
						if (index >= cache.Slots.Count)
						{
							break;
						}
						UiFacilityView view = cache.Slots[index];
						float height = (view != null) ? MeasureFacilityCard(view, cellWidth) : UiMetrics.EmptyCardMinHeight;
						rowHeight = Mathf.Max(rowHeight, height);
					}
					if (draw)
					{
						for (int column = 0; column < columns; column++)
						{
							int index = row * columns + column;
							if (index >= cache.Slots.Count)
							{
								break;
							}
							Rect cellRect = new Rect(rect.x + (cellWidth + UiMetrics.SlotGridGap) * column, y, cellWidth, rowHeight);
							UiFacilityView view = cache.Slots[index];
							if (view != null)
							{
								DrawFacilityCard(cellRect, view);
								if (index == builtFlashSlotIndex)
								{
									DrawUpgradeFlash(cellRect, builtFlashStartedAt);
								}
							}
							else
							{
								DrawEmptySlotCard(cellRect, index);
							}
						}
					}
					y += rowHeight + UiMetrics.SlotGridGap;
				}
				y -= UiMetrics.SlotGridGap;
			}
			return Mathf.Max(y - rect.y, 1f);
		}

		// ---------------------------------------------------------------
		// 现代科技风设施页
		// ---------------------------------------------------------------

		private float LayoutModernTech(Rect rect, bool draw)
		{
			OutpostUiCache cache = Cache;
			if (draw)
			{
				ObserveLevelChange();
				ObserveBuiltFacility(cache);
			}

			float width = rect.width;
			if (width < 80f)
			{
				return 1f;
			}

			float y = rect.y;

			float levelHeight = MeasureModernTechLevelArea(width, cache);
			if (draw)
			{
				DrawModernTechLevelArea(new Rect(rect.x, y, width, levelHeight), cache);
			}
			y += levelHeight + UiMetrics.ModernTechSectionSpacing;

			y += ModernTechSectionHead(
				rect.x,
				y,
				width,
				"DreamsOutposts.CoreFacility".Translate(),
				null,
				draw, true);

			UiFacilityView core = cache.Core;
			if (core != null)
			{
				float cardHeight = MeasureModernTechFacilityCard(core, width);
				if (draw)
				{
					DrawModernTechFacilityCard(new Rect(rect.x, y, width, cardHeight), core);
				}
				y += cardHeight;
			}
			else
			{
				if (draw)
				{
					DrawEmptyHintRow(
						new Rect(rect.x, y, width, 0f),
						"DreamsOutposts.FacilityNoDef".Translate());
				}
				y += EmptyHintRowHeight();
			}

			y += UiMetrics.ModernTechSectionSpacing;
			y += ModernTechSectionHead(
				rect.x,
				y,
				width,
				"DreamsOutposts.ExtensionFacilities".Translate(cache.UsedSlots, cache.SlotCount),
				"DreamsOutposts.Ui.ExtensionHint".Translate(),
				draw);

			if (cache.SlotCount == 0)
			{
				if (draw)
				{
					DrawEmptyHintRow(
						new Rect(rect.x, y, width, 0f),
						"DreamsOutposts.NoExtensionSlots".Translate());
				}
				y += EmptyHintRowHeight();
			}
			else
			{
				int columns = UiMetrics.GridColumns(
					width,
					UiMetrics.SlotGridMinCell,
					UiMetrics.ModernTechSlotGridGap);

				float cellWidth = UiMetrics.GridCellWidth(
					width,
					columns,
					UiMetrics.ModernTechSlotGridGap);

				int rowCount = Mathf.CeilToInt((float)cache.Slots.Count / columns);

				for (int row = 0; row < rowCount; row++)
				{
					float rowHeight = 0f;

					for (int column = 0; column < columns; column++)
					{
						int index = row * columns + column;
						if (index >= cache.Slots.Count)
						{
							break;
						}

						UiFacilityView view = cache.Slots[index];
						float height = (view != null)
							? MeasureModernTechFacilityCard(view, cellWidth)
							: UiMetrics.ModernTechEmptyCardMinHeight;

						rowHeight = Mathf.Max(rowHeight, height);
					}

					if (draw)
					{
						for (int column = 0; column < columns; column++)
						{
							int index = row * columns + column;
							if (index >= cache.Slots.Count)
							{
								break;
							}

							Rect cellRect = new Rect(
								rect.x + (cellWidth + UiMetrics.ModernTechSlotGridGap) * column,
								y,
								cellWidth,
								rowHeight);

							UiFacilityView view = cache.Slots[index];
							if (view != null)
							{
								DrawModernTechFacilityCard(cellRect, view);

								if (index == builtFlashSlotIndex)
								{
									DrawUpgradeFlash(cellRect, builtFlashStartedAt);
								}
							}
							else
							{
								DrawModernTechEmptySlotCard(cellRect, index);
							}
						}
					}

					y += rowHeight + UiMetrics.ModernTechSlotGridGap;
				}

				y -= UiMetrics.ModernTechSlotGridGap;
			}

			return Mathf.Max(y - rect.y, 1f);
		}

		private static float ModernTechSectionHead(
			float x,
			float y,
			float width,
			string title,
			string hint,
			bool draw,
			bool showHelp = false)
		{
			float titleHeight = UiText.LineHeight(UiFont.Heading);
			float hintHeight = UiText.LineHeight(UiFont.Body);
			float lineHeight = Mathf.Max(titleHeight, hintHeight);

			if (draw)
			{
				UiText.Draw(
					new Rect(x, y, width * 0.60f, titleHeight),
					title,
					UiFont.Heading,
					UiPalette.Ink,
					TextAnchor.UpperLeft,
					true,
					false,
					true);

				if (showHelp)
				{
					DrawFacilityHelp(x, y, title, lineHeight);
				}
				else if (!string.IsNullOrEmpty(hint))
				{
					float hintWidth = Mathf.Max(
						width * 0.40f - UiMetrics.SectionHeadGap,
						40f);

					UiText.Draw(
						new Rect(x + width - hintWidth, y, hintWidth, lineHeight),
						hint,
						UiFont.Body,
						UiPalette.Ink2,
						TextAnchor.UpperRight,
						false,
						false,
						true);
				}

				float ruleY = y + titleHeight + 5f;
				UiDraw.Solid(new Rect(x, ruleY, width, 1f), UiPalette.LineStrong);
			}

			return titleHeight + UiMetrics.ModernTechSectionHeadMarginBottom;
		}

		private static bool ModernTechLevelStacked(float width)
		{
			return width < UiMetrics.ModernTechLevelStackBreakpoint;
		}

		private static float MeasureModernTechLevelArea(float width, OutpostUiCache cache)
		{
			Rect level, facts, upgrade;
			return ModernTechOverviewLayout(new Rect(0f, 0f, width, 0f), cache,
				out level, out facts, out upgrade);
		}

		// Measurement and drawing share the same geometry, including narrow windows.
		private static float ModernTechOverviewLayout(Rect rect, OutpostUiCache cache,
			out Rect level, out Rect facts, out Rect upgrade)
		{
			bool stacked = ModernTechLevelStacked(rect.width);
			float rightWidth = stacked ? rect.width : Mathf.Clamp(
				rect.width * UiMetrics.ModernTechLevelRightRatio,
				UiMetrics.ModernTechLevelRightMinWidth, UiMetrics.ModernTechLevelRightMaxWidth);
			float leftWidth = stacked ? rect.width : rect.width - rightWidth - UiMetrics.ModernTechLevelGap;
			float levelHeight = MeasureModernTechLevelLeft(leftWidth, cache);
			float factsHeight = UiText.LineHeight(UiFont.Body) * 2f + 10f;
			level = new Rect(rect.x, rect.y, leftWidth, levelHeight);
			facts = new Rect(rect.x, level.yMax + 18f, leftWidth, factsHeight);
			float rightHeight = MeasureLevelRight(Mathf.Max(rightWidth -
				UiMetrics.ModernTechLevelPanelPadding * 2f, 60f), cache)
				+ UiMetrics.ModernTechLevelPanelPadding * 2f;
			upgrade = new Rect(stacked ? rect.x : rect.xMax - rightWidth,
				stacked ? facts.yMax + UiMetrics.ModernTechLevelGap : rect.y,
				rightWidth, rightHeight);
			return Mathf.Max(facts.yMax, upgrade.yMax) - rect.y;
		}

		private void DrawModernTechLevelArea(Rect rect, OutpostUiCache cache)
		{
			Rect level, facts, upgrade;
			ModernTechOverviewLayout(rect, cache, out level, out facts, out upgrade);
			DrawModernTechLevelLeft(level, cache, outpost.level);
			DrawModernTechLevelFacts(facts, cache);
			DrawModernTechUpgradePanel(upgrade, cache);
		}

		private void DrawModernTechUpgradePanel(Rect rect, OutpostUiCache cache)
		{
			UiDraw.Solid(rect, UiPalette.WithAlpha(UiPalette.PanelGlassStrong, 0.52f));
			UiDraw.Solid(new Rect(rect.x, rect.y, rect.width, 1f), UiPalette.LineStrong);
			Rect inner = rect.ContractedBy(UiMetrics.ModernTechLevelPanelPadding);
			DrawLevelRight(
				new Rect(inner.x, inner.y, inner.width, 0f),
				cache);

			DrawUpgradeFlash(rect, upgradeFlashStartedAt);
		}


		// ---------------------------------------------------------------
		// 区块标题
		// ---------------------------------------------------------------

		private static float SectionHeadHeight()
		{
			return UiText.LineHeight(UiFont.Heading) + UiMetrics.SectionHeadMarginBottom;
		}

		private static void DrawFacilityHelp(float x, float y, string title, float lineHeight)
		{
			string label = "DreamsOutposts.Ui.Hint".Translate();
			float hintWidth = UiText.Width(label, UiFont.Body) + 4f;
			float hintHeight = UiDraw.HintHeight();
			// 紧贴标题文字右侧，而不是贴区块右端
			float hintX = x + UiText.Width(title, UiFont.Heading) + UiMetrics.SectionHeadGap;
			Rect hintRect = new Rect(hintX, y + (lineHeight - hintHeight) * 0.5f, hintWidth, hintHeight);
			if (UiDraw.Hint(hintRect, label,
				UiPalette.WithAlpha(UiPalette.Ink3, 0.82f), UiPalette.WithAlpha(UiPalette.Ink2, 0.82f)))
			{
				UiOutpostHelpWindow.Open(HelpSections);
			}
		}

		private static float SectionHead(float x, float y, float width, string title, string hint, bool draw, bool showHelp = false)
		{
			float titleHeight = UiText.LineHeight(UiFont.Heading);
			float hintHeight = UiText.LineHeight(UiFont.Body);
			float lineHeight = Mathf.Max(titleHeight, hintHeight);

			if (draw)
			{
				UiText.Draw(new Rect(x, y, width * 0.6f, titleHeight), title,
					UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);

				if (showHelp)
				{
					DrawFacilityHelp(x, y, title, lineHeight);
				}
				else if (!string.IsNullOrEmpty(hint))
				{
					float hintWidth = Mathf.Max(width * 0.4f - UiMetrics.SectionHeadGap, 40f);
					UiText.Draw(new Rect(x + width - hintWidth, y, hintWidth, lineHeight), hint,
						UiFont.Body, UiPalette.Ink2, TextAnchor.UpperRight, false, false, true);
				}

				UiDebug.Scope("section.head", new Rect(x, y, width, lineHeight));
			}

			return SectionHeadHeight();
		}

		private static float EmptyHintRowHeight()
		{
			return 14f + UiText.LineHeight(UiFont.Body) + 14f;
		}

		private static void DrawEmptyHintRow(Rect rect, string text)
		{
			Rect row = new Rect(rect.x, rect.y, rect.width, EmptyHintRowHeight());
			UiText.Draw(row, text, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
		}

		// ---------------------------------------------------------------
		// 等级卡
		// ---------------------------------------------------------------

		private static bool LevelCardStacked(float width)
		{
			return width < UiMetrics.StackBreakpoint;
		}

		private static float MeasureLevelCard(float width, OutpostUiCache cache)
		{
			float cardWidth = width;
			bool stacked = LevelCardStacked(cardWidth);
			float innerWidth = cardWidth - UiMetrics.LevelCardPaddingH * 2f;
			float leftWidth = stacked ? innerWidth : Mathf.Max(innerWidth - UiMetrics.LevelCardGap - UiMetrics.LevelRightWidth, 120f);
			float rightWidth = stacked ? innerWidth : UiMetrics.LevelRightWidth;
			float leftHeight = MeasureLevelLeft(leftWidth, cache);
			float rightHeight = MeasureLevelRight(rightWidth, cache);
			float contentHeight = stacked ? (leftHeight + 16f + rightHeight) : Mathf.Max(leftHeight, rightHeight);
			return contentHeight + UiMetrics.LevelCardPaddingV * 2f;
		}

		private static float MeasureLevelLeft(float width, OutpostUiCache cache)
		{
			float labelHeight = UiText.LineHeight(UiFont.Body);
			float digitRowHeight = UiMetrics.LevelCurrentDigitHeight;
			return labelHeight
				+ 4f
				+ digitRowHeight
				+ UiMetrics.LevelDigitRowGap
				+ UiMetrics.PipHeight
				+ UiMetrics.LevelFactsMarginTop
				+ UiMetrics.LevelFactHeight;
		}

		private static float MeasureLevelRight(float width, OutpostUiCache cache)
		{
			float height = UiText.LineHeight(UiFont.Body) + 10f;
			if (cache.Upgrade.IsMaxLevel)
			{
				return height;
			}
			float rowHeight = Mathf.Max(Mathf.Max(UiMetrics.ReqTickSize, UiMetrics.MatIconSize), UiText.LineHeight(UiFont.Body));
			height += cache.Upgrade.Checks.Count * (rowHeight + UiMetrics.ReqListGap);
			height += UiMetrics.ReqListMarginBottom;
			height += DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla)
				? UiWidgets.ButtonHeight(UiButtonSize.Normal)
				: UiMetrics.UpgradeButtonHeight * 1.5f;
			return height;
		}

		private void DrawLevelCard(Rect rect, OutpostUiCache cache)
		{
			UiDebug.Scope("level.card", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.PanelGlassStrong, UiPalette.Line);
			UiDraw.Solid(new Rect(rect.x, rect.y, Mathf.Min(84f, rect.width), 3f), UiPalette.Brand);
			DrawUpgradeFlash(rect, upgradeFlashStartedAt);

			bool stacked = LevelCardStacked(rect.width);
			float innerX = rect.x + UiMetrics.LevelCardPaddingH;
			float innerWidth = rect.width - UiMetrics.LevelCardPaddingH * 2f;
			float innerY = rect.y + UiMetrics.LevelCardPaddingV;
			float leftWidth = stacked
				? innerWidth
				: Mathf.Max(innerWidth - UiMetrics.LevelCardGap - UiMetrics.LevelRightWidth, 120f);

			DrawLevelLeft(new Rect(innerX, innerY, leftWidth, 0f), cache, outpost.level);

			if (stacked)
			{
				float leftHeight = MeasureLevelLeft(leftWidth, cache);
				float rightTop = innerY + leftHeight + 18f;
				UiDraw.Divider(new Rect(innerX, rightTop - 9f, innerWidth, 1f), UiPalette.Line);
				DrawLevelRight(new Rect(innerX, rightTop, innerWidth, 0f), cache);
			}
			else
			{
				float rightX = innerX + leftWidth + UiMetrics.LevelCardGap;
				UiDraw.Divider(new Rect(rightX - UiMetrics.LevelCardGap * 0.5f, innerY, 1f,
					rect.height - UiMetrics.LevelCardPaddingV * 2f), UiPalette.Line);
				DrawLevelRight(new Rect(rightX + UiMetrics.LevelCardGap * 0.5f, innerY,
					innerWidth - leftWidth - UiMetrics.LevelCardGap * 1.5f, 0f), cache);
			}
		}

		private void ObserveLevelChange()
		{
			int currentLevel = outpost?.level ?? 1;
			if (observedLevel >= 0 && currentLevel > observedLevel)
			{
				upgradeFlashStartedAt = Time.realtimeSinceStartup;
			}
			observedLevel = currentLevel;
		}

		/// <summary>槽位从空变成有设施（或换成了别的设施）时，记下时间和槽位号，让那张卡片闪一次光效。</summary>
		private void ObserveBuiltFacility(OutpostUiCache cache)
		{
			int count = cache.Slots.Count;
			if (observedSlotFacilities == null || observedSlotFacilities.Length != count)
			{
				// 首次观测或槽位数变化（据点升级）：只记快照，不闪光
				observedSlotFacilities = new string[count];
				for (int i = 0; i < count; i++)
				{
					observedSlotFacilities[i] = SlotFacilityKey(cache.Slots[i]);
				}
				return;
			}
			for (int i = 0; i < count; i++)
			{
				string key = SlotFacilityKey(cache.Slots[i]);
				if (!string.IsNullOrEmpty(key) && key != observedSlotFacilities[i])
				{
					builtFlashSlotIndex = i;
					builtFlashStartedAt = Time.realtimeSinceStartup;
				}
				observedSlotFacilities[i] = key;
			}
		}

		private static string SlotFacilityKey(UiFacilityView view)
		{
			return view?.Facility?.def?.defName;
		}

		private void DrawUpgradeFlash(Rect rect, float startedAt)
		{
			float elapsed = Time.realtimeSinceStartup - startedAt;
			float totalDuration = UiMetrics.LevelUpgradeSweepDuration + UiMetrics.LevelUpgradeFlashDuration;
			if (elapsed < 0f || elapsed >= totalDuration)
			{
				return;
			}
			float fadeElapsed = Mathf.Max(elapsed - UiMetrics.LevelUpgradeSweepDuration, 0f);
			float remaining = 1f - fadeElapsed / UiMetrics.LevelUpgradeFlashDuration;
			float strength = remaining * remaining;
			Color previous = GUI.color;
			GUI.color = new Color(previous.r, previous.g, previous.b,
				previous.a * UiMetrics.LevelUpgradeFlashFillAlpha * strength);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.BrandTint, UiPalette.Clear);
			if (elapsed < UiMetrics.LevelUpgradeSweepDuration)
			{
				DrawLevelUpgradeSweep(rect, elapsed / UiMetrics.LevelUpgradeSweepDuration, previous);
			}
			GUI.color = new Color(previous.r, previous.g, previous.b,
				previous.a * UiMetrics.LevelUpgradeFlashBorderAlpha * strength);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Clear, UiPalette.Brand);
			GUI.color = previous;
		}

		private static void DrawLevelUpgradeSweep(Rect rect, float progress, Color previous)
		{
			Texture2D texture = UiTex.LevelUpgradeSweepTexture();
			if (texture == null)
			{
				return;
			}
			float t = Mathf.Clamp01(progress);
			float eased = t * t * (3f - 2f * t);
			float bandHeight = Mathf.Min(UiMetrics.LevelUpgradeSweepHeight, rect.height);
			float localY = rect.height + bandHeight * 0.5f - eased * (rect.height + bandHeight * 2f);
			float envelope = Mathf.Sin(t * Mathf.PI);
			GUI.BeginGroup(rect);
			GUI.color = new Color(previous.r, previous.g, previous.b,
				previous.a * UiMetrics.LevelUpgradeSweepAlpha * envelope);
			GUI.DrawTexture(new Rect(0f, localY, rect.width, bandHeight), texture, ScaleMode.StretchToFill, true);
			GUI.EndGroup();
			GUI.color = previous;
		}

		private static float MeasureModernTechLevelLeft(float width, OutpostUiCache cache)
		{
			float labelHeight = UiText.LineHeight(UiFont.Body);

			return labelHeight
				+ 4f
				+ UiMetrics.ModernTechLevelCurrentDigitHeight
				+ UiMetrics.LevelDigitRowGap
				+ UiMetrics.ModernTechPipHeight;
		}

		private void DrawModernTechLevelLeft(Rect rect, OutpostUiCache cache, int level)
		{
			float y = rect.y;

			// “等级 / Level”仍从现有本地化模板中提取。
			const string currentMarker = "__DO_LEVEL__";
			const string maxMarker = "__DO_MAX__";
			string levelTemplate =
				"DreamsOutposts.Level".Translate(currentMarker, maxMarker).ToString();

			int currentMarkerIndex =
				levelTemplate.IndexOf(
					currentMarker,
					System.StringComparison.Ordinal);

			string levelLabel = currentMarkerIndex >= 0
				? levelTemplate.Substring(0, currentMarkerIndex).Trim()
				: string.Empty;

			float labelHeight = UiText.LineHeight(UiFont.Body);

			if (!string.IsNullOrEmpty(levelLabel))
			{
				UiText.Draw(
					new Rect(rect.x, y, rect.width, labelHeight),
					levelLabel,
					UiFont.Body,
					UiPalette.Ink,
					TextAnchor.UpperLeft,
					false,
					false,
					true);
			}

			y += labelHeight + 4f;

			Texture2D currentDigit = UiTex.LevelDigitTexture(level);
			Texture2D maxDigit = UiTex.LevelDigitTexture(cache.MaxLevel);

			float currentHeight =
				UiMetrics.ModernTechLevelCurrentDigitHeight;

			float currentWidth =
				DigitWidth(currentDigit, currentHeight, 68f);

			float maxHeight =
				UiMetrics.ModernTechLevelMaxDigitHeight;

			float maxWidth =
				DigitWidth(maxDigit, maxHeight, 38f);

			Rect currentRect = new Rect(
				rect.x,
				y,
				currentWidth,
				currentHeight);

			DrawLevelDigit(
				currentRect,
				currentDigit,
				level.ToString(),
				Color.white,
				1f);

			float slashWidth = 28f;
			float slashX =
				currentRect.xMax +
				UiMetrics.ModernTechLevelDigitGap;

			Rect slashRect = new Rect(
				slashX,
				y + currentHeight - maxHeight - 4f,
				slashWidth,
				maxHeight);

			UiText.Draw(
				slashRect,
				"/",
				UiFont.Heading,
				UiPalette.Ink,
				TextAnchor.MiddleCenter,
				false,
				false,
				false);

			float maxX =
				slashRect.xMax +
				UiMetrics.ModernTechLevelDigitGap;

			Rect maxRect = new Rect(
				maxX,
				y + currentHeight - maxHeight,
				maxWidth,
				maxHeight);

			DrawLevelDigit(
				maxRect,
				maxDigit,
				cache.MaxLevel.ToString(),
				Color.white,
				0.72f);

			y +=
				currentHeight +
				UiMetrics.LevelDigitRowGap;

			// 第一层只保留等级进度条，不再把四项状态塞在这里。
			int pipCount = cache.Pips.Count;
			float totalGap =
				Mathf.Max(pipCount - 1, 0) *
				UiMetrics.ModernTechPipGap;

			float pipWidth = pipCount > 0
				? Mathf.Max(
					(rect.width - totalGap) / pipCount,
					20f)
				: rect.width;

			float x = rect.x;

			for (int i = 0; i < pipCount; i++)
			{
				UiLevelPip pip = cache.Pips[i];

				Color fill;
				if (pip.State == UiPipState.Done)
				{
					fill = UiPalette.BrandLine;
				}
				else if (pip.State == UiPipState.Current)
				{
					fill = UiPalette.Brand;
				}
				else
				{
					fill = UiPalette.WithAlpha(
						UiPalette.Ink3,
						0.58f);
				}

				Rect pipRect = new Rect(
					x,
					y,
					pipWidth,
					UiMetrics.ModernTechPipHeight);

				UiDraw.Solid(pipRect, fill);

				Color outline = pip.State == UiPipState.Current
					? UiPalette.Brand
					: UiPalette.WithAlpha(UiPalette.Ink, 0.32f);

				UiDraw.Solid(
					new Rect(
						pipRect.x,
						pipRect.y,
						pipRect.width,
						1f),
					outline);

				UiDraw.Solid(
					new Rect(
						pipRect.x,
						pipRect.yMax - 1f,
						pipRect.width,
						1f),
					outline);

				UiDraw.Solid(
					new Rect(
						pipRect.x,
						pipRect.y,
						1f,
						pipRect.height),
					outline);

				UiDraw.Solid(
					new Rect(
						pipRect.xMax - 1f,
						pipRect.y,
						1f,
						pipRect.height),
					outline);

				UiWidgets.Tip(
					new Rect(
						pipRect.x,
						pipRect.y - 4f,
						pipRect.width,
						pipRect.height + 8f),
					pip.Tooltip,
					GenText.StableStringHash(
						"moderntech-pip-" + pip.Level));

				x +=
					pipWidth +
					UiMetrics.ModernTechPipGap;
			}
		}

		private void DrawModernTechLevelFacts(
			Rect rect,
			OutpostUiCache cache)
		{
			string slotsLabel, slotsValue;
			BuildFact(
				"DreamsOutposts.Ui.Chip.CurrentSlots"
					.Translate(FactMarker)
					.ToString(),
				outpost.SlotCountForLevel.ToString(),
				out slotsLabel,
				out slotsValue);

			string coreLabel, coreValue;
			BuildFact(
				"DreamsOutposts.Ui.Chip.CoreFacility"
					.Translate(FactMarker)
					.ToString(),
				cache.Core?.Label ??
					"DreamsOutposts.None".Translate().ToString(),
				out coreLabel,
				out coreValue);

			string daysLabel, daysValue;
			BuildFact(
				"DreamsOutposts.Ui.DaysSinceEstablished"
					.Translate(FactMarker)
					.ToString(),
				outpost.DaysSinceEstablished.ToString("0.#"),
				out daysLabel,
				out daysValue);

			string defenseLabel, defenseValue;
			BuildFact(
				"DreamsOutposts.Ui.Chip.DefenseValue"
					.Translate(FactMarker)
					.ToString(),
				outpost.Defense.ToString("0.#"),
				out defenseLabel,
				out defenseValue);

			DrawModernTechLevelFactRow(
				rect,
				slotsLabel,
				slotsValue,
				coreLabel,
				coreValue,
				daysLabel,
				daysValue,
				defenseLabel,
				defenseValue);
		}

		private static void DrawModernTechLevelFactRow(
			Rect rect,
			string label0,
			string value0,
			string label1,
			string value1,
			string label2,
			string value2,
			string label3,
			string value3)
		{
			string[] labels =
				{ label0, label1, label2, label3 };

			string[] values =
				{ value0, value1, value2, value3 };

			float gap = UiMetrics.LevelFactColumnGap;
			float columnWidth = Mathf.Max(
				(rect.width - gap * 3f) / 4f,
				70f);

			float labelHeight =
				UiText.LineHeight(UiFont.Body);

			float valueHeight =
				UiText.LineHeight(UiFont.Body);

			for (int i = 0; i < 4; i++)
			{
				Rect column = new Rect(
					rect.x + i * (columnWidth + gap),
					rect.y,
					columnWidth,
					rect.height);

				// 第二层信息只留非常轻的品牌色标记。
				UiDraw.Solid(
					new Rect(
						column.x,
						column.y + 4f,
						2f,
						Mathf.Max(column.height - 8f, 1f)),
					UiPalette.WithAlpha(
						UiPalette.LineStrong,
						0.65f));

				float textX =
					column.x +
					UiMetrics.ModernTechLevelFactPaddingLeft;

				float textWidth =
					Mathf.Max(
						column.xMax - textX,
						20f);

				UiText.Draw(
					new Rect(
						textX,
						column.y,
						textWidth,
						labelHeight),
					labels[i],
					UiFont.Body,
					UiPalette.Ink3,
					TextAnchor.UpperLeft,
					false,
					false,
					true);

				UiText.Draw(
					new Rect(
						textX,
						column.y + labelHeight + 3f,
						textWidth,
						valueHeight),
					values[i],
					UiFont.Body,
					UiPalette.Ink2,
					TextAnchor.UpperLeft,
					true,
					false,
					true);
			}
		}

		private void DrawLevelLeft(Rect rect, OutpostUiCache cache, int level)
		{
			float y = rect.y;

			// 从本地化模板里取“等级 / Level”这一段，不硬编码语言。
			const string currentMarker = "__DO_LEVEL__";
			const string maxMarker = "__DO_MAX__";
			string levelTemplate = "DreamsOutposts.Level".Translate(currentMarker, maxMarker).ToString();
			int currentMarkerIndex = levelTemplate.IndexOf(currentMarker, System.StringComparison.Ordinal);
			string levelLabel = currentMarkerIndex >= 0
				? levelTemplate.Substring(0, currentMarkerIndex).Trim()
				: string.Empty;

			float labelHeight = UiText.LineHeight(UiFont.Body);
			if (!string.IsNullOrEmpty(levelLabel))
			{
				UiText.Draw(new Rect(rect.x, y, rect.width, labelHeight), levelLabel,
					UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			}
			y += labelHeight + 4f;

			// 当前等级和最大等级都使用同一套白色 PNG，只用 tint / alpha 区分。
			Texture2D currentDigit = UiTex.LevelDigitTexture(level);
			Texture2D maxDigit = UiTex.LevelDigitTexture(cache.MaxLevel);

			// 原版风：当前等级 / 最大等级使用完全相同的文字尺寸和垂直空间，
			// 保证视觉上就是规整的 “1 / 4”，不再让当前等级因为 Rect 更高而上浮。
			float digitHeight = UiMetrics.LevelCurrentDigitHeight;
			float digitWidth = 48f;
			float slashWidth = 24f;

			Rect currentRect = new Rect(
				rect.x,
				y,
				digitWidth,
				digitHeight);

			DrawLevelDigit(
				currentRect,
				currentDigit,
				level.ToString(),
				Color.white,
				1f);

			float slashX =
				currentRect.xMax +
				UiMetrics.LevelDigitGap;

			Rect slashRect = new Rect(
				slashX,
				y,
				slashWidth,
				digitHeight);

			UiText.Draw(
				slashRect,
				"/",
				UiFont.Heading,
				Color.white,
				TextAnchor.MiddleCenter,
				false,
				false,
				false);

			float maxX =
				slashRect.xMax +
				UiMetrics.LevelDigitGap;

			Rect maxRect = new Rect(
				maxX,
				y,
				digitWidth,
				digitHeight);

			DrawLevelDigit(
				maxRect,
				maxDigit,
				cache.MaxLevel.ToString(),
				Color.white,
				1f);

			y += digitHeight + UiMetrics.LevelDigitRowGap;

			// 等级进度线继续使用细分段。
			int pipCount = cache.Pips.Count;
			float totalGap = Mathf.Max(pipCount - 1, 0) * UiMetrics.PipGap;
			float pipWidth = pipCount > 0
				? Mathf.Max((rect.width - totalGap) / pipCount, 20f)
				: rect.width;

			float x = rect.x;
			for (int i = 0; i < pipCount; i++)
			{
				UiLevelPip pip = cache.Pips[i];
				Color color = UiPalette.Track;
				if (pip.State == UiPipState.Done) color = UiPalette.BrandLine;
				else if (pip.State == UiPipState.Current) color = UiPalette.Brand;

				Rect pipRect = new Rect(x, y, pipWidth, UiMetrics.PipHeight);
				UiDraw.Solid(pipRect, color);
				// 细描边：让 6px 高的等级进度段更清晰，同时保持轻量感。
				UiDraw.Solid(new Rect(pipRect.x, pipRect.y, pipRect.width, 1f), UiPalette.Line);
				UiDraw.Solid(new Rect(pipRect.x, pipRect.yMax - 1f, pipRect.width, 1f), UiPalette.Line);
				UiDraw.Solid(new Rect(pipRect.x, pipRect.y, 1f, pipRect.height), UiPalette.Line);
				UiDraw.Solid(new Rect(pipRect.xMax - 1f, pipRect.y, 1f, pipRect.height), UiPalette.Line);
				UiWidgets.Tip(new Rect(pipRect.x, pipRect.y - 4f, pipRect.width, pipRect.height + 8f),
					pip.Tooltip, GenText.StableStringHash("pip-" + pip.Level));
				x += pipWidth + UiMetrics.PipGap;
			}

			y += UiMetrics.PipHeight + UiMetrics.LevelFactsMarginTop;

			string slotsLabel, slotsValue;
			BuildFact("DreamsOutposts.Ui.Chip.CurrentSlots".Translate(FactMarker).ToString(),
				outpost.SlotCountForLevel.ToString(), out slotsLabel, out slotsValue);

			string coreLabel, coreValue;
			BuildFact("DreamsOutposts.Ui.Chip.CoreFacility".Translate(FactMarker).ToString(),
				cache.Core?.Label ?? "DreamsOutposts.None".Translate().ToString(), out coreLabel, out coreValue);

			string daysLabel, daysValue;
			BuildFact("DreamsOutposts.Ui.DaysSinceEstablished".Translate(FactMarker).ToString(),
				outpost.DaysSinceEstablished.ToString("0.#"), out daysLabel, out daysValue);

			string defenseLabel, defenseValue;
			BuildFact("DreamsOutposts.Ui.Chip.DefenseValue".Translate(FactMarker).ToString(),
				outpost.Defense.ToString("0.#"), out defenseLabel, out defenseValue);

			DrawLevelFactRow(new Rect(rect.x, y, rect.width, UiMetrics.LevelFactHeight),
				slotsLabel, slotsValue,
				coreLabel, coreValue,
				daysLabel, daysValue,
				defenseLabel, defenseValue);
		}

		private static float DigitWidth(Texture2D texture, float height, float fallbackWidth)
		{
			if (texture == null || texture.height <= 0)
			{
				return fallbackWidth;
			}
			return Mathf.Max(height * texture.width / texture.height, 1f);
		}

		private static void DrawLevelDigit(Rect rect, Texture2D texture, string fallback,
			Color tint, float alpha)
		{
			// 原版深色主题下，等级数字需要直接以亮白绘制。
			// 不再继承外层 GUI.color 的 RGB 乘色，否则白色 PNG 会被压暗。
			// 同时把次级数字（原 alpha=0.58）略微提亮到约 0.70；
			// 当前等级 alpha=1 仍保持 1。
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				float vanillaAlpha = Mathf.Clamp01(alpha);

				UiText.Draw(
					rect,
					fallback,
					UiFont.Heading,
					new Color(1f, 1f, 1f, vanillaAlpha),
					TextAnchor.MiddleCenter,
					true,
					false,
					true);

				return;
			}

			// 现代风保持原有表现。
			if (texture == null)
			{
				UiText.Draw(
					rect,
					fallback,
					UiFont.Heading,
					UiPalette.WithAlpha(tint, alpha),
					TextAnchor.MiddleCenter,
					true,
					false,
					true);
				return;
			}

			Color modernPrevious = GUI.color;
			GUI.color = new Color(
				modernPrevious.r * tint.r,
				modernPrevious.g * tint.g,
				modernPrevious.b * tint.b,
				modernPrevious.a * tint.a * alpha);
			GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
			GUI.color = modernPrevious;
		}

		private const string FactMarker = "__DO_VALUE__";

		private static void BuildFact(string translatedTemplate, string actualValue,
			out string label, out string displayValue)
		{
			int markerIndex = translatedTemplate.IndexOf(FactMarker, System.StringComparison.Ordinal);
			if (markerIndex < 0)
			{
				label = translatedTemplate;
				displayValue = actualValue;
				return;
			}

			string prefix = translatedTemplate.Substring(0, markerIndex).Trim();
			string suffix = translatedTemplate.Substring(markerIndex + FactMarker.Length).Trim();
			label = prefix.TrimEnd(':', '：', '-', '—', '/', '|');
			displayValue = string.IsNullOrEmpty(suffix) ? actualValue : actualValue + " " + suffix;
		}

		private static void DrawLevelFactRow(Rect rect,
			string label0, string value0,
			string label1, string value1,
			string label2, string value2,
			string label3, string value3)
		{
			string[] labels = { label0, label1, label2, label3 };
			string[] values = { value0, value1, value2, value3 };

			float gap = UiMetrics.LevelFactColumnGap;
			float columnWidth = Mathf.Max((rect.width - gap * 3f) / 4f, 70f);

			for (int i = 0; i < 4; i++)
			{
				Rect column = new Rect(
					rect.x + i * (columnWidth + gap),
					rect.y,
					columnWidth,
					rect.height);

				float separatorHeight = Mathf.Max(column.height - 4f, 1f);
				float separatorPadding = UiMetrics.LevelFactPaddingLeft;

				if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
				{
					// 原版风格不读取 sidebar.png：只保留干净的白色竖线。
					UiDraw.Solid(
						new Rect(
							column.x,
							column.y + 2f,
							UiMetrics.LevelFactLineWidth,
							separatorHeight),
						Color.white);
				}
				else
				{
					Texture2D separator = UiTex.SidebarSeparatorTexture();
					if (separator != null && separator.width > 0 && separator.height > 0)
					{
						float separatorWidth =
							separatorHeight * separator.width / separator.height;

						GUI.DrawTexture(
							new Rect(
								column.x,
								column.y + 2f,
								separatorWidth,
								separatorHeight),
							separator,
							ScaleMode.StretchToFill,
							true);

						separatorPadding = Mathf.Max(
							separatorPadding,
							separatorWidth + 7f);
					}
					else
					{
						UiDraw.Solid(
							new Rect(
								column.x,
								column.y + 2f,
								UiMetrics.LevelFactLineWidth,
								separatorHeight),
							UiPalette.Brand);
					}
				}

				float textX = column.x + separatorPadding;
				float textWidth = Mathf.Max(column.xMax - textX, 20f);
				float labelHeight = UiText.LineHeight(UiFont.Body);
				float valueHeight = UiText.LineHeight(UiFont.Heading);

				UiText.Draw(
					new Rect(textX, column.y, textWidth, labelHeight),
					labels[i],
					UiFont.Body,
					UiPalette.Ink2,
					TextAnchor.UpperLeft,
					false,
					false,
					true);

				UiText.Draw(
					new Rect(
						textX,
						column.y + labelHeight + 3f,
						textWidth,
						valueHeight),
					values[i],
					UiFont.Heading,
					UiPalette.Ink,
					TextAnchor.UpperLeft,
					true,
					false,
					true);
			}
		}

		private void DrawLevelRight(Rect rect, OutpostUiCache cache)
		{
			float y = rect.y;
			if (cache.Upgrade.IsMaxLevel)
			{
				// 满级：只留一行文字说明
				UiText.Draw(new Rect(rect.x, y, rect.width, UiText.LineHeight(UiFont.Body)), cache.Upgrade.MaxLevelText,
					UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
				return;
			}
			if (!string.IsNullOrEmpty(cache.Upgrade.Title))
			{
				UiText.Draw(new Rect(rect.x, y, rect.width, UiText.LineHeight(UiFont.Body)), cache.Upgrade.Title,
					UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			}
			y += UiText.LineHeight(UiFont.Body) + 10f;
			float rowHeight = Mathf.Max(Mathf.Max(UiMetrics.ReqTickSize, UiMetrics.MatIconSize), UiText.LineHeight(UiFont.Body));
			for (int i = 0; i < cache.Upgrade.Checks.Count; i++)
			{
				UiUpgradeCheck check = cache.Upgrade.Checks[i];
				Rect row = new Rect(rect.x, y, rect.width, rowHeight);
				float valueWidth = Mathf.Max(UiText.Width(check.ValueText, UiFont.Body, true), 60f);
				if (check.Thing != null)
				{
					Rect iconRect = new Rect(row.x, row.y + (row.height - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
					float nameX = iconRect.xMax + 8f;
					Rect nameRect = new Rect(nameX, row.y, Mathf.Max(row.width - (nameX - row.x) - valueWidth - 8f, 30f), row.height);
					UiDraw.ThingInfoLink(new Rect(iconRect.x, row.y, nameRect.xMax - iconRect.x, row.height), iconRect, nameRect,
						check.Thing, check.Name, UiFont.Body, UiPalette.Ink2);
				}
				else
				{
					float tickY = row.y + (row.height - UiMetrics.ReqTickSize) * 0.5f;
					Rect tickRect = new Rect(row.x, tickY, UiMetrics.ReqTickSize, UiMetrics.ReqTickSize);
					UiDraw.Box(tickRect, (int)UiMetrics.RadiusSm2, check.Ok ? UiPalette.GoodBg : UiPalette.BadBg, check.Ok ? UiPalette.GoodLine : UiPalette.BadLine);
					float glyph = Mathf.Round(UiMetrics.ReqTickSize * 0.62f);
					UiDraw.Icon(new Rect(tickRect.center.x - glyph * 0.5f, tickRect.center.y - glyph * 0.5f, glyph, glyph),
						check.Ok ? UiIcon.Check : UiIcon.Cross, check.Ok ? UiPalette.Good : UiPalette.Bad);
					float nameX = tickRect.xMax + 8f;
					UiText.Draw(new Rect(nameX, row.y, Mathf.Max(row.width - (nameX - row.x) - valueWidth - 8f, 30f), row.height), check.Name,
						UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				}
				UiText.Draw(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height), check.ValueText,
					UiFont.Body, check.Ok ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleRight, true);
				y += rowHeight + UiMetrics.ReqListGap;
			}
			y += UiMetrics.ReqListMarginBottom - UiMetrics.ReqListGap;
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				string upgradeLabel =
					"DreamsOutposts.Ui.Upgrade.Button".Translate().ToString();

				string vanillaTip = cache.Upgrade.CanUpgrade
					? "DreamsOutposts.Ui.Upgrade.ConfirmTip".Translate().ToString()
					: cache.Upgrade.Reason;

				float vanillaButtonHeight =
					UiWidgets.ButtonHeight(UiButtonSize.Normal);

				float vanillaButtonWidth = Mathf.Min(
					UiWidgets.ButtonWidth(
						upgradeLabel,
						UiButtonSize.Normal),
					rect.width);

				Rect vanillaButtonRect = new Rect(
					rect.center.x - vanillaButtonWidth * 0.5f,
					y,
					vanillaButtonWidth,
					vanillaButtonHeight);

				if (UiWidgets.Button(
					vanillaButtonRect,
					upgradeLabel,
					UiButtonKind.Primary,
					cache.Upgrade.CanUpgrade,
					cache.Upgrade.Reason,
					UiButtonSize.Normal,
					vanillaTip))
				{
					if (OutpostUpgradeUtility.TryUpgrade(outpost))
					{
						Window_OutpostManage shell = Shell;
						if (shell != null)
						{
							shell.Cache.Invalidate();
						}
					}
				}

				return;
			}
			const float upgradeButtonScale = 1.5f;
			float buttonHeight = UiMetrics.UpgradeButtonHeight * upgradeButtonScale;
			Texture2D upgradeTexture = UiTex.UpgradeButtonTexture();
			float buttonWidth = (upgradeTexture != null && upgradeTexture.height > 0)
				? buttonHeight * upgradeTexture.width / upgradeTexture.height
				: 173f * upgradeButtonScale;
			float actualButtonWidth = Mathf.Min(buttonWidth, rect.width);
			Rect buttonRect = new Rect(
				rect.center.x - actualButtonWidth * 0.5f,
				y,
				actualButtonWidth,
				buttonHeight);

			// upgrade.png 是 790×164 的成品图；文本区域从原图 x=145 开始。
			string pendingTip = cache.Upgrade.CanUpgrade
				? "DreamsOutposts.Ui.Upgrade.ConfirmTip".Translate().ToString()
				: cache.Upgrade.Reason;

			if (DrawUpgradeArtworkButton(
				buttonRect,
				"DreamsOutposts.Ui.Upgrade.Button".Translate().ToString(),
				upgradeTexture,
				cache.Upgrade.CanUpgrade,
				cache.Upgrade.Reason,
				pendingTip))
			{
				if (OutpostUpgradeUtility.TryUpgrade(outpost))
				{
					Window_OutpostManage shell = Shell;
					if (shell != null)
					{
						shell.Cache.Invalidate();
					}
				}
			}
		}

		private static bool DrawUpgradeArtworkButton(
			Rect rect,
			string label,
			Texture2D texture,
			bool enabled,
			string disabledReason,
			string tooltip)
		{
			const float sourceWidth = 790f;
			const float sourceLabelX = 145f;
			const float sourceLabelWidth = 645f;

			Rect labelRect = new Rect(
				rect.x + rect.width * sourceLabelX / sourceWidth,
				rect.y,
				rect.width * sourceLabelWidth / sourceWidth,
				rect.height);

			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				// 原版风保留 RimWorld 自己的 enabled / disabled 按钮状态。
				// 这里只接管文字排版，不再使用现代风的 DisabledAlpha。
				bool clicked = UiWidgets.Button(
					rect,
					string.Empty,
					UiButtonKind.Primary,
					enabled,
					disabledReason,
					UiButtonSize.Normal,
					tooltip);

				Color textColor = enabled
					? Color.white
					: new Color(0.72f, 0.72f, 0.72f, 1f);

				UiText.Draw(
					labelRect,
					label,
					UiFont.Heading,
					textColor,
					TextAnchor.MiddleLeft,
					true,
					false,
					true);

				return clicked;
			}

			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}

			bool hovered = enabled && Mouse.IsOver(rect);
			bool pressed = hovered &&
				(Event.current.type == EventType.MouseDown ||
				 Event.current.type == EventType.MouseDrag);

			Rect drawRect = rect;
			if (pressed)
			{
				float shrinkX = Mathf.Max(rect.width * 0.025f, 1f);
				float shrinkY = Mathf.Max(rect.height * 0.05f, 1f);
				drawRect = new Rect(
					rect.x + shrinkX,
					rect.y + shrinkY,
					Mathf.Max(rect.width - shrinkX * 2f, 1f),
					Mathf.Max(rect.height - shrinkY * 2f, 1f));
			}

			// 现代风：
			// 可升级 + hover -> 整体透明度降低 30%；
			// 不可升级 -> 保持 100%，不再做灰化或禁用态透明处理。
			float alpha = hovered ? 0.7f : 1f;

			if (texture != null)
			{
				Color previous = GUI.color;
				GUI.color = new Color(
					previous.r,
					previous.g,
					previous.b,
					previous.a * alpha);

				GUI.DrawTexture(
					drawRect,
					texture,
					ScaleMode.StretchToFill,
					true);

				GUI.color = previous;
			}

			Rect drawnLabelRect = new Rect(
				drawRect.x + drawRect.width * sourceLabelX / sourceWidth,
				drawRect.y,
				drawRect.width * sourceLabelWidth / sourceWidth,
				drawRect.height);

			UiText.Draw(
				drawnLabelRect,
				label,
				UiFont.Heading,
				UiPalette.WithAlpha(UiPalette.OnAccent, alpha),
				TextAnchor.MiddleLeft,
				true,
				false,
				true);

			string tip = (!enabled && !string.IsNullOrEmpty(disabledReason))
				? disabledReason
				: tooltip;

			if (!string.IsNullOrEmpty(tip))
			{
				TooltipHandler.TipRegion(
					rect,
					new TipSignal(tip, rect.GetHashCode()));
			}

			if (!enabled)
			{
				return false;
			}

			return Widgets.ButtonInvisible(rect);
		}
		// ---------------------------------------------------------------
		// 现代科技风设施卡
		// ---------------------------------------------------------------

		/// <summary>
		/// 把文本压成真正的一行；超出指定宽度时在末尾添加省略号。
		/// 现代科技风窄卡片不能依赖 GUI 自己裁切，否则文字会画出卡片边界。
		/// </summary>
		private static string FitSingleLine(
			string text,
			UiFont font,
			float maxWidth,
			bool bold = false)
		{
			if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
			{
				return string.Empty;
			}

			// Def description 里如果本身带换行，也压成一行。
			string source = text
				.Replace("\r", " ")
				.Replace("\n", " ")
				.Trim();

			while (source.Contains("  "))
			{
				source = source.Replace("  ", " ");
			}

			if (UiText.Width(source, font, bold) <= maxWidth)
			{
				return source;
			}

			const string ellipsis = "…";
			float ellipsisWidth = UiText.Width(ellipsis, font, bold);

			if (ellipsisWidth >= maxWidth)
			{
				return ellipsis;
			}

			int low = 0;
			int high = source.Length;

			while (low < high)
			{
				int mid = (low + high + 1) / 2;
				string candidate =
					source.Substring(0, mid).TrimEnd() + ellipsis;

				if (UiText.Width(candidate, font, bold) <= maxWidth)
				{
					low = mid;
				}
				else
				{
					high = mid - 1;
				}
			}

			return source.Substring(0, low).TrimEnd() + ellipsis;
		}
		private float MeasureModernTechFacilityCard(UiFacilityView view, float width)
		{
			return LayoutModernTechFacilityCard(
				new Rect(0f, 0f, width, 0f),
				view,
				false);
		}

		private void DrawModernTechFacilityCard(Rect rect, UiFacilityView view)
		{
			LayoutModernTechFacilityCard(rect, view, true);
		}

		private float LayoutModernTechFacilityCard(
			Rect rect,
			UiFacilityView view,
			bool draw)
		{
			float innerX = rect.x + UiMetrics.ModernTechCardPaddingH;
			float innerWidth = Mathf.Max(
				rect.width - UiMetrics.ModernTechCardPaddingH * 2f,
				30f);

			float y = rect.y + UiMetrics.ModernTechCardPaddingTop;
			bool hovered = draw && Mouse.IsOver(rect);

			float imageSize = UiMetrics.ModernTechFacilityImageSize;
			float lineHeight = UiText.LineHeight(UiFont.Heading);

			float textX = innerX + imageSize + UiMetrics.ModernTechFacilityImageGap;
			float textWidth = Mathf.Max(
				rect.xMax - UiMetrics.ModernTechCardPaddingH - textX,
				30f);

			// 核心设施卡不再显示「核心设施 · 不可拆除」这个右上角标签
			//（这串说明只在详情弹窗与防御页保留），标题因此可以使用整行文本宽度。
			float titleWidth = textWidth;

			float descriptionWidth = textWidth;

			// 卡片上只给一行：描述里带换行时只取第一行（补「...」），再按宽度截断
			string displayDescription = FitSingleLine(
				UiText.FirstLine(view.Description),
				UiFont.Body,
				descriptionWidth);

			float descriptionHeight =
				string.IsNullOrEmpty(displayDescription)
					? 0f
					: lineHeight;

			float textHeadHeight =
				lineHeight +
				UiMetrics.ModernTechDescriptionGap +
				descriptionHeight;

			float headHeight = Mathf.Max(
				imageSize,
				textHeadHeight);

			if (draw)
			{
				// 与空槽位使用相同的绿色悬停底色；常态保持透明。
				if (hovered)
				{
					UiDraw.Solid(rect, UiPalette.WithAlpha(UiPalette.BrandTint, 0.68f));
				}

				// 扩展设施卡与空槽位共用同一套四角标记；
				// 核心设施卡不加（它在页面上是单张的，不需要跟空槽位对齐轮廓）。
				if (!view.IsCore)
				{
					UiDraw.InstrumentFrame(
						rect,
						hovered ? UiPalette.BrandLine : UiPalette.LineStrong,
						hovered ? 24f : 14f);
				}

				Rect imageRect = new Rect(
					innerX,
					y,
					imageSize,
					imageSize);

				Texture2D image = UiTex.FacilityPlaceholderTexture();
				if (image != null)
				{
					Color previous = GUI.color;
					GUI.color = new Color(1f, 1f, 1f, previous.a);

					GUI.DrawTexture(
						imageRect,
						image,
						ScaleMode.ScaleAndCrop,
						true);

					GUI.color = previous;

					float glyph = UiMetrics.ModernTechFacilityImageGlyph;
					UiDraw.Icon(
						new Rect(
							imageRect.center.x - glyph * 0.5f,
							imageRect.center.y - glyph * 0.5f,
							glyph,
							glyph),
						view.Icon,
						UiPalette.Light);
				}
				else
				{
					UiDraw.Box(
						imageRect,
						(int)UiMetrics.RadiusSm,
						UiPalette.ArtBlock,
						UiPalette.LineStrong);

					float glyph = UiMetrics.ModernTechFacilityImageGlyph;
					UiDraw.Icon(
						new Rect(
							imageRect.center.x - glyph * 0.5f,
							imageRect.center.y - glyph * 0.5f,
							glyph,
							glyph),
						view.Icon,
						UiPalette.Light);
				}

				UiDraw.Box(
					imageRect,
					(int)UiMetrics.RadiusSm,
					UiPalette.Clear,
					hovered ? UiPalette.BrandLine : UiPalette.LineStrong);

				UiText.Draw(
					new Rect(
						textX,
						y + 2f,
						titleWidth,
						lineHeight),
					view.Label,
					UiFont.Heading,
					UiPalette.Ink,
					TextAnchor.MiddleLeft,
					true,
					false,
					true);

				if (!string.IsNullOrEmpty(displayDescription) &&
					descriptionHeight > 0f)
				{
					UiText.Draw(
						new Rect(
							textX,
							y + lineHeight + UiMetrics.ModernTechDescriptionGap,
							descriptionWidth,
							descriptionHeight),
						displayDescription,
						UiFont.Body,
						UiPalette.Ink2,
						TextAnchor.UpperLeft,
						false,
						false,
						true);
				}

				Rect headRect = new Rect(
					innerX,
					y,
					innerWidth,
					headHeight);

				UiWidgets.Tip(
					headRect,
					view.TooltipGetter,
					view.TooltipId);

				UiDebug.Scope("facility.moderntech.rect", rect);
			}

			y += headHeight + UiMetrics.ModernTechCardGap;

			float sectionsTop = y;

			if (view.InfoGroups.Count > 0)
			{
				if (draw)
				{
					UiDraw.Solid(
						new Rect(
							innerX,
							y,
							innerWidth,
							1f),
						UiPalette.WithAlpha(UiPalette.Line, 0.82f));
				}

				y += UiMetrics.ModernTechFacilitySectionTopGap;
			}

			for (int i = 0; i < view.InfoGroups.Count; i++)
			{
				if (i > 0)
				{
					y += UiMetrics.ProdListGap;
				}

				y += LayoutFacilityInfoGroup(
					innerX,
					y,
					innerWidth,
					view.InfoGroups[i],
					draw) + UiMetrics.ModernTechCardGap;
			}

			y = Mathf.Max(
				y,
				sectionsTop + UiMetrics.ModernTechFacilityBodyMinHeight);

			float footerHeight =
				LayoutModernTechFacilityFooter(
					rect,
					view,
					y,
					draw);

			float natural =
				y +
				footerHeight -
				rect.y +
				UiMetrics.ModernTechCardPaddingBottom;

			if (!draw)
			{
				return natural;
			}

			if (Event.current.mousePosition.y >= viewportTop &&
				Event.current.mousePosition.y <= viewportBottom &&
				Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null)
				{
					shell.OpenDetailsModal(view);
				}
			}

			return Mathf.Max(rect.height, natural);
		}

		private float LayoutModernTechFacilityFooter(
			Rect cardRect,
			UiFacilityView view,
			float y,
			bool draw)
		{
			float innerX =
				cardRect.x +
				UiMetrics.ModernTechCardPaddingH;

			float innerWidth = Mathf.Max(
				cardRect.width -
					UiMetrics.ModernTechCardPaddingH * 2f,
				30f);

			float chipsHeight = (view.ModernDisplayChips.Count > 0)
				? UiDraw.ChipsHeight(
					view.ModernDisplayChips,
					innerWidth,
					true)
				: 0f;

			if (!draw || view.ModernDisplayChips.Count == 0)
			{
				return chipsHeight;
			}

			float footerY = Mathf.Max(
				cardRect.yMax -
					UiMetrics.ModernTechCardPaddingBottom -
					chipsHeight,
				y);

			UiDraw.Chips(
				new Rect(
					innerX,
					footerY,
					innerWidth,
					chipsHeight),
				view.ModernDisplayChips,
				true);

			return chipsHeight;
		}

		private void DrawModernTechEmptySlotCard(Rect rect, int index)
		{
			bool hovered = Mouse.IsOver(rect);
			Color line = hovered
				? UiPalette.BrandLine
				: UiPalette.LineStrong;

			// 与扩展设施卡保持一致：常态完全透明，只在悬停时铺一层淡绿底。
			if (hovered)
			{
				UiDraw.Solid(
					rect,
					UiPalette.WithAlpha(
						UiPalette.BrandTint,
						0.68f));
			}

			UiDraw.InstrumentFrame(rect, line, hovered ? 24f : 14f);


			string slot = (index + 1).ToString("D2");
			float slotWidth = Mathf.Max(
				UiText.Width(slot, UiFont.Body, true),
				28f);

			UiText.Draw(
				new Rect(
					rect.xMax -
						UiMetrics.SlotTagRight -
						slotWidth,
					rect.y +
						UiMetrics.SlotTagTop,
					slotWidth,
					UiText.LineHeight(UiFont.Body)),
				slot,
				UiFont.Body,
				hovered
					? UiPalette.BrandText
					: UiPalette.Ink3,
				TextAnchor.MiddleRight,
				true);

			float plusSize = UiMetrics.ModernTechEmptyPlusSize;
			float labelHeight = UiText.LineHeight(UiFont.Body);
			float hintHeight = UiText.LineHeight(UiFont.Body);

			float contentHeight =
				plusSize +
				UiMetrics.ModernTechEmptyContentGap +
				labelHeight +
				hintHeight;

			float contentY =
				rect.y +
				(rect.height - contentHeight) * 0.5f;

			Rect plusRect = new Rect(
				rect.center.x - plusSize * 0.5f,
				contentY,
				plusSize,
				plusSize);

			UiDraw.Box(
				plusRect,
				0,
				hovered
					? UiPalette.PanelGlassStrong
					: UiPalette.Clear,
				line);

			float glyph = Mathf.Round(
				plusSize * 0.42f);

			UiDraw.Icon(
				new Rect(
					plusRect.center.x - glyph * 0.5f,
					plusRect.center.y - glyph * 0.5f,
					glyph,
					glyph),
				UiIcon.Plus,
				hovered
					? UiPalette.Brand
					: UiPalette.Ink2);

			UiText.Draw(
				new Rect(
					rect.x,
					plusRect.yMax +
						UiMetrics.ModernTechEmptyContentGap,
					rect.width,
					labelHeight),
				"DreamsOutposts.EmptySlot".Translate(),
				UiFont.Body,
				hovered
					? UiPalette.BrandText
					: UiPalette.Ink,
				TextAnchor.MiddleCenter,
				true);

			UiText.Draw(
				new Rect(
					rect.x,
					plusRect.yMax +
						UiMetrics.ModernTechEmptyContentGap +
						labelHeight,
					rect.width,
					hintHeight),
				"DreamsOutposts.Ui.EmptySlotHint".Translate(index + 1),
				UiFont.Body,
				hovered
					? UiPalette.BrandText
					: UiPalette.Ink2,
				TextAnchor.MiddleCenter,
				false,
				false,
				true);

			UiWidgets.Tip(
				rect,
				"DreamsOutposts.EmptySlotTip".Translate(),
				GenText.StableStringHash(
					"empty-slot-moderntech-" + index));

			UiDebug.Scope(
				"empty.moderntech.slot[" + index + "]",
				rect);

			if (Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null &&
					outpost.extensionSlots != null &&
					index >= 0 &&
					index < outpost.extensionSlots.Count)
				{
					shell.OpenInstallModal(
						outpost.extensionSlots[index],
						index);
				}
			}
		}

		// ---------------------------------------------------------------
		// 设施卡
		// ---------------------------------------------------------------

		private float MeasureFacilityCard(UiFacilityView view, float width)
		{
			return LayoutFacilityCard(new Rect(0f, 0f, width, 0f), view, false);
		}

		private void DrawFacilityCard(Rect rect, UiFacilityView view)
		{
			LayoutFacilityCard(rect, view, true);
		}

		private float LayoutFacilityCard(Rect rect, UiFacilityView view, bool draw)
		{
			float innerX = rect.x + UiMetrics.CardPaddingH;
			float innerWidth = Mathf.Max(rect.width - UiMetrics.CardPaddingH * 2f, 30f);
			float y = rect.y + UiMetrics.CardPaddingTop;
			bool hovered = draw && Mouse.IsOver(rect);
			float headHeight = Mathf.Max(UiMetrics.FacilityImageSize,
				UiText.LineHeight(UiFont.Body) * 2f + 8f);

			if (draw)
			{
				Color fill = hovered
					? UiPalette.PanelGlassStrong
					: (view.IsCore ? UiPalette.PanelGlassStrong : UiPalette.PanelGlass);
				Color line = hovered ? UiPalette.BrandLine : UiPalette.Line;
				UiDraw.Box(rect, (int)UiMetrics.RadiusSm, fill, line);

				// 设施档案卡：左侧是主视觉图，当前统一使用黑方块占位图。
				Rect imageRect = new Rect(innerX, y, UiMetrics.FacilityImageSize, UiMetrics.FacilityImageSize);
				Texture2D image = UiTex.FacilityPlaceholderTexture();
				if (image != null)
				{
					Color previous = GUI.color;
					GUI.color = new Color(1f, 1f, 1f, previous.a);
					GUI.DrawTexture(imageRect, image, ScaleMode.ScaleAndCrop, true);
					GUI.color = previous;

					float glyph = UiMetrics.FacilityImageGlyph;
					UiDraw.Icon(new Rect(imageRect.center.x - glyph * 0.5f, imageRect.center.y - glyph * 0.5f,
						glyph, glyph), view.Icon, UiPalette.Light);
				}
				else
				{
					UiDraw.Box(imageRect, (int)UiMetrics.RadiusSm, UiPalette.ArtBlock, UiPalette.LineStrong);
					float glyph = UiMetrics.FacilityImageGlyph;
					UiDraw.Icon(new Rect(imageRect.center.x - glyph * 0.5f, imageRect.center.y - glyph * 0.5f,
						glyph, glyph), view.Icon, UiPalette.Light);
				}
				UiDraw.Box(imageRect, (int)UiMetrics.RadiusSm, UiPalette.Clear,
					hovered ? UiPalette.BrandLine : UiPalette.LineStrong);

				float nameX = imageRect.xMax + 14f;
				float reservedTag = view.IsCore ? 0f : 42f;
				float nameWidth = Mathf.Max(rect.xMax - UiMetrics.CardPaddingH - nameX - reservedTag, 30f);
				float lineHeight = UiText.LineHeight(UiFont.Body);

				UiText.Draw(new Rect(nameX, y + 4f, nameWidth, lineHeight), view.Label,
					UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
				UiText.Draw(new Rect(nameX, y + 4f + lineHeight, nameWidth, lineHeight), view.SubLabel,
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);

				UiDraw.Solid(new Rect(nameX, y + 4f + lineHeight * 2f + 7f, Mathf.Min(42f, nameWidth), 2f),
					UiPalette.Brand);

				if (!view.IsCore)
				{
					string slot = (view.SlotIndex + 1).ToString("D2");
					float tagWidth = Mathf.Max(UiText.Width(slot, UiFont.Body, true), 26f);
					UiText.Draw(new Rect(rect.xMax - UiMetrics.SlotTagRight - tagWidth,
						rect.y + UiMetrics.SlotTagTop, tagWidth, lineHeight),
						slot, UiFont.Body, hovered ? UiPalette.BrandText : UiPalette.Ink3,
						TextAnchor.MiddleRight, true);
				}

				Rect headRect = new Rect(innerX, y, innerWidth, headHeight);
				UiWidgets.Tip(headRect, view.TooltipGetter, view.TooltipId);
				UiDebug.Scope("facility.rect", rect);
			}

			y += headHeight + UiMetrics.CardGap;

			float sectionsTop = y;
			for (int i = 0; i < view.InfoGroups.Count; i++)
			{
				if (i > 0)
				{
					y += UiMetrics.ProdListGap;
				}
				y += LayoutFacilityInfoGroup(innerX, y, innerWidth, view.InfoGroups[i], draw) + UiMetrics.CardGap;
			}

			y = Mathf.Max(y, sectionsTop + UiMetrics.FacilityBodyMinHeight);

			float footerHeight = LayoutFacilityFooter(rect, view, y, draw);
			float natural = y + footerHeight - rect.y + UiMetrics.CardPaddingBottom;
			if (!draw)
			{
				return natural;
			}

			if (Event.current.mousePosition.y >= viewportTop &&
				Event.current.mousePosition.y <= viewportBottom &&
				Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null)
				{
					shell.OpenDetailsModal(view);
				}
			}
			return Mathf.Max(rect.height, natural);
		}

		private float LayoutModernFacilityInfoGroup(float x, float y, float width, UiFacilityInfoGroup group, bool draw)
		{
			if (group == null) return 0f;
			List<UiFacilityInfoItem> items = group.CardItems;
			if (items.Count == 0) return 0f;
			UiFacilityInfoItem headline = items.Find(item => item.CardPlacement == UiFacilityCardPlacement.Header);
			float innerX = x + UiMetrics.ProdPaddingH;
			float innerWidth = Mathf.Max(width - UiMetrics.ProdPaddingH * 2f, 20f);
			float cursor = y + UiMetrics.ProdPaddingV;
			float topHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body));
			if (draw)
			{
				float textX = innerX;
				if (group.IconThing != null)
				{
					Widgets.ThingIcon(new Rect(innerX, cursor + (topHeight - UiMetrics.MatIconSize) * 0.5f,
						UiMetrics.MatIconSize, UiMetrics.MatIconSize), group.IconThing);
					textX += UiMetrics.MatIconSize + 8f;
				}
				string mainText = headline?.DisplayText ?? string.Empty;
				float mainWidth = string.IsNullOrEmpty(mainText) ? 0f
					: Mathf.Min(Mathf.Max(UiText.Width(mainText, UiFont.Number, true) + 6f, 48f), innerWidth * 0.42f);
				float titleWidth = Mathf.Max(innerX + innerWidth - textX - mainWidth, 20f);
				Rect titleRect = new Rect(textX, cursor, titleWidth, topHeight);
				UiText.Draw(titleRect, FitSingleLine(group.Title ?? string.Empty, UiFont.Body, titleWidth),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
				UiWidgets.Tip(titleRect, group.Title + (string.IsNullOrEmpty(group.Tooltip) ? string.Empty : "\n\n" + group.Tooltip), titleRect.GetHashCode());
				if (mainWidth > 0f)
				{
					Rect mainRect = new Rect(innerX + innerWidth - mainWidth, cursor, mainWidth, topHeight);
					UiText.Draw(mainRect, FitSingleLine(mainText, UiFont.Number, mainWidth, true),
						UiFont.Number, UiPalette.Ink, TextAnchor.MiddleRight, true);
					UiWidgets.Tip(mainRect, mainText + (string.IsNullOrEmpty(headline.Tooltip) ? string.Empty : "\n\n" + headline.Tooltip), mainRect.GetHashCode());
				}
			}
			cursor += topHeight;
			foreach (UiFacilityInfoItem item in items)
			{
				if (ReferenceEquals(item, headline)) continue;
				if (item.CardPlacement == UiFacilityCardPlacement.Action)
				{
					if (item.Action == null) continue;
					cursor += UiMetrics.ProdGap;
					float height = UiWidgets.ButtonHeight(UiButtonSize.Small);
					if (draw && UiWidgets.Button(new Rect(innerX, cursor, innerWidth, height),
						item.ActionLabel ?? "DreamsOutposts.Ui.Switch".Translate(), UiButtonKind.Secondary,
						true, null, UiButtonSize.Small, item.ActionTooltip)) item.Action();
					cursor += height;
				}
				else if (item.CardPlacement == UiFacilityCardPlacement.Progress)
				{
					cursor += UiMetrics.ProdGap;
					if (draw)
					{
						Color fill = item.Tone == UiChipKind.Good ? UiPalette.Good
							: item.Tone == UiChipKind.Warn ? UiPalette.Warn
							: item.Tone == UiChipKind.Bad ? UiPalette.Bad : UiPalette.Accent;
						Rect bar = new Rect(innerX, cursor, innerWidth, UiMetrics.BarHeight);
						UiDraw.Bar(bar, Mathf.Clamp01(item.Progress), fill, UiPalette.Track);
						UiWidgets.Tip(bar, item.ProgressText, bar.GetHashCode());
					}
					cursor += UiMetrics.BarHeight;
					// Wrap each summary rather than overwriting another fact or clipping its value.
					cursor += LayoutModernInfoPair(innerX, cursor, innerWidth, item.LeftText, item.RightText, item.Tooltip, draw);
				}
				else if (item.CardPlacement == UiFacilityCardPlacement.Chip)
				{
					var chips = new List<UiChipView> { new UiChipView(item.DisplayText, item.Tone, item.Tooltip) };
					cursor += UiMetrics.ProdGap;
					float height = UiDraw.ChipsHeight(chips, innerWidth, true);
					if (draw) UiDraw.Chips(new Rect(innerX, cursor, innerWidth, height), chips, true);
					cursor += height;
				}
				else
				{
					cursor += LayoutModernInfoText(innerX, cursor, innerWidth, item.DisplayText, item.Tooltip, draw);
				}
			}
			return cursor + UiMetrics.ProdPaddingV - y;
		}

		private static float LayoutModernInfoPair(float x, float y, float width, string left, string right, string tooltip, bool draw)
		{
			float leftWidth = string.IsNullOrEmpty(left) ? 0f : UiText.Width(left, UiFont.Body);
			float rightWidth = string.IsNullOrEmpty(right) ? 0f : UiText.Width(right, UiFont.Body);
			if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
				leftWidth + rightWidth + UiMetrics.ProdGap * 2f <= width)
			{
				float height = UiText.LineHeight(UiFont.Body);
				if (draw)
				{
					Rect row = new Rect(x, y + UiMetrics.ProdGap, width, height);
					UiText.Draw(new Rect(row.x, row.y, width - rightWidth - UiMetrics.ProdGap, height),
						left, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
					UiText.Draw(new Rect(row.xMax - rightWidth, row.y, rightWidth, height),
						right, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight);
					if (!string.IsNullOrEmpty(tooltip)) UiWidgets.Tip(row, tooltip, row.GetHashCode());
				}
				return UiMetrics.ProdGap + height;
			}
			float used = LayoutModernInfoText(x, y, width, left, tooltip, draw);
			return used + LayoutModernInfoText(x, y + used, width, right, tooltip, draw);
		}

		private static float LayoutModernInfoText(float x, float y, float width, string text, string tooltip, bool draw)
		{
			if (string.IsNullOrEmpty(text)) return 0f;
			float height = UiText.Height(text, UiFont.Body, width);
			if (draw)
			{
				Rect row = new Rect(x, y + UiMetrics.ProdGap, width, height);
				UiText.Draw(row, text, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
				if (!string.IsNullOrEmpty(tooltip)) UiWidgets.Tip(row, tooltip, row.GetHashCode());
			}
			return UiMetrics.ProdGap + height;
		}

		private float LayoutFacilityInfoGroup(float x, float y, float width, UiFacilityInfoGroup group, bool draw)
		{
			if (DreamsOutpostsMod.UsesModernTechLayout)
				return LayoutModernFacilityInfoGroup(x, y, width, group, draw);

			if (group == null)
			{
				return 0f;
			}

			UiFacilityInfoItem primary = null;
			UiFacilityInfoItem progressItem = null;
			UiFacilityInfoItem actionItem = null;
			string leftText = null;
			string rightText = null;
			List<UiChipView> compactChips = new List<UiChipView>();

			for (int i = 0; i < group.Items.Count; i++)
			{
				UiFacilityInfoItem item = group.Items[i];
				if (item == null) continue;

				if (item.Kind == UiFacilityInfoKind.Progress && progressItem == null)
				{
					progressItem = item;
					continue;
				}
				if (item.Kind == UiFacilityInfoKind.Action && actionItem == null)
				{
					actionItem = item;
					continue;
				}
				if (item.Kind == UiFacilityInfoKind.Compact)
				{
					compactChips.Add(new UiChipView(item.DisplayText, item.Tone, item.Tooltip));
					continue;
				}
				if (item.Importance == UiFacilityInfoImportance.Primary && primary == null)
				{
					primary = item;
					continue;
				}
				if (item.Importance == UiFacilityInfoImportance.Supporting && string.IsNullOrEmpty(leftText))
				{
					leftText = item.DisplayText;
				}
				else if (item.Importance == UiFacilityInfoImportance.Detail && string.IsNullOrEmpty(rightText))
				{
					rightText = item.DisplayText;
				}
			}

			if (progressItem != null)
			{
				if (!string.IsNullOrEmpty(progressItem.LeftText)) leftText = progressItem.LeftText;
				if (!string.IsNullOrEmpty(progressItem.RightText)) rightText = progressItem.RightText;
			}

			string mainText = primary?.DisplayText ?? string.Empty;
			string tooltip = !string.IsNullOrEmpty(group.Tooltip)
				? group.Tooltip
				: (progressItem?.Tooltip ?? primary?.Tooltip);

			float innerX = x + UiMetrics.ProdPaddingH;
			float innerWidth = Mathf.Max(width - UiMetrics.ProdPaddingH * 2f, 20f);
			float cursor = y + UiMetrics.ProdPaddingV;
			float topHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body));

			if (draw)
			{
				float textX = innerX;
				if (group.IconThing != null)
				{
					Rect icon = new Rect(innerX, cursor + (topHeight - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
					Widgets.ThingIcon(icon, group.IconThing);
					textX += UiMetrics.MatIconSize + 8f;
				}

				bool modernTech = DreamsOutpostsMod.UsesModernTechLayout;
				float mainWidth = string.IsNullOrEmpty(mainText)
					? 0f
					: Mathf.Max(UiText.Width(mainText, UiFont.Number, true) + 6f, 48f);

				if (modernTech)
				{
					mainWidth = Mathf.Min(mainWidth, innerWidth * 0.42f);
				}

				float titleWidth = Mathf.Max(innerX + innerWidth - textX - mainWidth, 20f);
				string groupTitle = modernTech
					? FitSingleLine(group.Title ?? string.Empty, UiFont.Body, titleWidth)
					: group.Title ?? string.Empty;

				UiText.Draw(
					new Rect(textX, cursor, titleWidth, topHeight),
					groupTitle,
					UiFont.Body,
					UiPalette.Ink2,
					TextAnchor.MiddleLeft,
					false,
					false,
					true);

				if (mainWidth > 0f)
				{
					string fittedMain = modernTech
						? FitSingleLine(mainText, UiFont.Number, mainWidth, true)
						: mainText;
					UiText.Draw(
						new Rect(innerX + innerWidth - mainWidth, cursor, mainWidth, topHeight),
						fittedMain,
						UiFont.Number,
						UiPalette.Ink,
						TextAnchor.MiddleRight,
						true);
				}
			}

			cursor += topHeight;

			if (progressItem != null)
			{
				cursor += UiMetrics.ProdGap;
				if (draw)
				{
					Color fill = progressItem.Tone == UiChipKind.Good
						? UiPalette.Good
						: progressItem.Tone == UiChipKind.Warn
							? UiPalette.Warn
							: progressItem.Tone == UiChipKind.Bad
								? UiPalette.Bad
								: UiPalette.Accent;
					UiDraw.Bar(new Rect(innerX, cursor, innerWidth, UiMetrics.BarHeight), Mathf.Clamp01(progressItem.Progress), fill, UiPalette.Track);
				}
				cursor += UiMetrics.BarHeight;
			}

			if (!string.IsNullOrEmpty(leftText) || !string.IsNullOrEmpty(rightText))
			{
				cursor += UiMetrics.ProdGap;
				if (draw)
				{
					float h = UiText.LineHeight(UiFont.Body);
					bool modernTech = DreamsOutpostsMod.UsesModernTechLayout;
					float rightWidth = string.IsNullOrEmpty(rightText)
						? 0f
						: Mathf.Max(UiText.Width(rightText, UiFont.Body) + 4f, 60f);

					if (modernTech)
					{
						rightWidth = Mathf.Min(rightWidth, innerWidth * 0.46f);
					}

					float leftWidth = Mathf.Max(innerWidth - rightWidth, 20f);
					string fittedLeft = modernTech
						? FitSingleLine(leftText ?? string.Empty, UiFont.Body, leftWidth)
						: leftText ?? string.Empty;

					UiText.Draw(
						new Rect(innerX, cursor, leftWidth, h),
						fittedLeft,
						UiFont.Body,
						UiPalette.Ink2,
						TextAnchor.MiddleLeft,
						false,
						false,
						true);

					if (rightWidth > 0f)
					{
						string fittedRight = modernTech
							? FitSingleLine(rightText, UiFont.Body, rightWidth)
							: rightText;
						UiText.Draw(
							new Rect(innerX + innerWidth - rightWidth, cursor, rightWidth, h),
							fittedRight,
							UiFont.Body,
							UiPalette.Ink2,
							TextAnchor.MiddleRight,
							false,
							false,
							true);
					}
				}
				cursor += UiText.LineHeight(UiFont.Body);
			}

			if (actionItem?.Action != null)
			{
				cursor += UiMetrics.ProdGap;
				float h = UiWidgets.ButtonHeight(UiButtonSize.Small);
				if (draw && UiWidgets.Button(
					new Rect(innerX, cursor, innerWidth, h),
					actionItem.ActionLabel ?? "DreamsOutposts.Ui.Switch".Translate(),
					UiButtonKind.Secondary,
					true,
					null,
					UiButtonSize.Small,
					actionItem.ActionTooltip))
				{
					actionItem.Action();
				}
				cursor += h;
			}

			if (compactChips.Count > 0)
			{
				cursor += UiMetrics.ProdGap;
				float h = UiDraw.ChipsHeight(compactChips, innerWidth, true);
				if (draw) UiDraw.Chips(new Rect(innerX, cursor, innerWidth, h), compactChips, true);
				cursor += h;
			}

			if (draw && !string.IsNullOrEmpty(tooltip))
			{
				UiWidgets.Tip(
					new Rect(x, y, width, cursor + UiMetrics.ProdPaddingV - y),
					tooltip,
					GenText.StableStringHash(group.Title ?? "facility-info-group"));
			}

			return cursor + UiMetrics.ProdPaddingV - y;
		}

		private float LayoutProductionBlock(float x, float y, float width, UiFacilityView view, UiProductionView production, bool draw)
		{
			float innerX = x + UiMetrics.ProdPaddingH;
			float innerWidth = Mathf.Max(width - UiMetrics.ProdPaddingH * 2f, 20f);
			float cursor = y + UiMetrics.ProdPaddingV;
			float topHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body));
			if (draw)
			{
				Rect iconRect = new Rect(innerX, cursor + (topHeight - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
				float textX = innerX + ((production.Product != null) ? UiMetrics.MatIconSize + 8f : 0f);
				string amount = "×" + production.Output.ToString("0.#");
				float amountWidth = Mathf.Max(UiText.Width(amount, UiFont.Number, true) + 6f, 48f);
				Rect nameRect = new Rect(textX, cursor, Mathf.Max(innerX + innerWidth - textX - amountWidth, 20f), topHeight);
				if (production.Product != null)
				{
					UiDraw.ThingInfoLink(new Rect(iconRect.x, cursor, nameRect.xMax - iconRect.x, topHeight), iconRect, nameRect,
						production.Product, production.ProductLabel, UiFont.Body, UiPalette.Ink2);
				}
				else
				{
					UiText.Draw(nameRect, production.ProductLabel, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				}
				UiText.Draw(new Rect(innerX + innerWidth - amountWidth, cursor, amountWidth, topHeight), amount,
					UiFont.Number, UiPalette.Ink, TextAnchor.MiddleRight, true);
			}
			cursor += topHeight + UiMetrics.ProdGap;
			if (draw)
			{
				float fraction = production.HasProgress ? production.Progress : 0f;
				Color fill = (production.HasProgress && production.Progress > 0f) ? UiPalette.Bad : UiPalette.ScrollThumb;
				UiDraw.Bar(new Rect(innerX, cursor, innerWidth, UiMetrics.BarHeight), fraction, fill, UiPalette.Track);
			}
			cursor += UiMetrics.BarHeight + UiMetrics.ProdGap;
			if (draw)
			{
				float metaHeight = UiText.LineHeight(UiFont.Body);
				string every = "DreamsOutposts.Ui.ProductionEvery".Translate(production.IntervalText);
				float everyWidth = Mathf.Max(UiText.Width(every, UiFont.Body) + 4f, 60f);
				UiText.Draw(new Rect(innerX, cursor, Mathf.Max(innerWidth - everyWidth, 20f), metaHeight), production.MetaText,
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				UiText.Draw(new Rect(innerX + innerWidth - everyWidth, cursor, everyWidth, metaHeight), every,
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
			}
			cursor += UiText.LineHeight(UiFont.Body);
			// 可配置生产规则：整行可点击，沿用生产 worker 提供的选择菜单。
			if (production.HasConfiguration)
			{
				cursor += UiMetrics.ProdGap;
				float rowHeight = Mathf.Max(UiText.LineHeight(UiFont.Body), 16f) + 10f;
				if (draw)
				{
					Rect row = new Rect(innerX, cursor, innerWidth, rowHeight);
					bool hovered = Mouse.IsOver(row);
					UiDraw.Box(row, (int)UiMetrics.RadiusXs, hovered ? UiPalette.Hover : UiPalette.Raised, UiPalette.Line);
					string configuration = production.ConfigurationSummary ?? string.Empty;
					UiText.Draw(new Rect(row.x + 8f, row.y, Mathf.Max(row.width - 16f - 40f, 20f), row.height), configuration,
						UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
					UiText.Draw(new Rect(row.xMax - 8f - 34f, row.y, 34f, row.height), "DreamsOutposts.Ui.Switch".Translate(),
						UiFont.Body, hovered ? UiPalette.Ink : UiPalette.Ink2, TextAnchor.MiddleRight);
					UiWidgets.Tip(row, production.Props.Worker.ConfigurationTip(production.Props), GenText.StableStringHash("production-config-" + view.SlotIndex + "-" + production.Props.id));
					if (Widgets.ButtonInvisible(row))
					{
						production.Props.Worker.OpenConfiguration(production.Props, view.Facility.GetProductionState(production.Props.id), Cache.Invalidate);
					}
				}
				cursor += rowHeight;
			}
			if (production.ModifierChips.Count > 0)
			{
				cursor += UiMetrics.ProdGap;
				float chipsHeight = UiDraw.ChipsHeight(production.ModifierChips, innerWidth, true);
				if (draw)
				{
					UiDraw.Chips(new Rect(innerX, cursor, innerWidth, chipsHeight), production.ModifierChips, true);
				}
				cursor += chipsHeight;
			}
			return cursor + UiMetrics.ProdPaddingV - y;
		}

		private float LayoutFacilityFooter(Rect cardRect, UiFacilityView view, float y, bool draw)
		{
			float innerX = cardRect.x + UiMetrics.CardPaddingH;
			float innerWidth = Mathf.Max(cardRect.width - UiMetrics.CardPaddingH * 2f, 30f);
			float chipsHeight = (view.DisplayChips.Count > 0) ? UiDraw.ChipsHeight(view.DisplayChips, innerWidth, true) : 0f;
			if (!draw || view.DisplayChips.Count == 0)
			{
				return chipsHeight;
			}
			// 从底部钉住（等价 .fc-foot 的 margin-top: auto）
			float footerY = Mathf.Max(cardRect.yMax - UiMetrics.CardPaddingBottom - chipsHeight, y);
			UiDraw.Chips(new Rect(innerX, footerY, innerWidth, chipsHeight), view.DisplayChips, true);
			return chipsHeight;
		}

		// ---------------------------------------------------------------
		// 空槽位
		// ---------------------------------------------------------------

		private void DrawEmptySlotCard(Rect rect, int index)
		{
			bool hovered = Mouse.IsOver(rect);
			Color line = hovered ? UiPalette.BrandLine : UiPalette.LineStrong;

			UiDraw.Box(rect, (int)UiMetrics.RadiusSm,
				hovered ? UiPalette.BrandTint : UiPalette.PanelGlass,
				UiPalette.Clear);
			UiDraw.DashedBox(rect, (int)UiMetrics.RadiusSm, line, 7f, 5f);

			string slot = (index + 1).ToString("D2");
			float slotWidth = Mathf.Max(UiText.Width(slot, UiFont.Body, true), 26f);
			UiText.Draw(new Rect(rect.xMax - UiMetrics.SlotTagRight - slotWidth,
				rect.y + UiMetrics.SlotTagTop, slotWidth, UiText.LineHeight(UiFont.Body)),
				slot, UiFont.Body, hovered ? UiPalette.BrandText : UiPalette.Ink3,
				TextAnchor.MiddleRight, true);

			Color ink = hovered ? UiPalette.BrandText : UiPalette.Ink2;
			float plusSize = 42f;
			float labelHeight = UiText.LineHeight(UiFont.Body);
			float hintHeight = UiText.LineHeight(UiFont.Body);
			float contentHeight = plusSize + UiMetrics.EmptyCardGap + labelHeight + hintHeight;
			float contentY = rect.y + (rect.height - contentHeight) * 0.5f;

			Rect plusRect = new Rect(rect.center.x - plusSize * 0.5f, contentY, plusSize, plusSize);
			UiDraw.Box(plusRect, (int)UiMetrics.RadiusSm,
				hovered ? UiPalette.PanelGlassStrong : UiPalette.Clear, line);

			float glyph = Mathf.Round(plusSize * 0.48f);
			UiDraw.Icon(new Rect(plusRect.center.x - glyph * 0.5f, plusRect.center.y - glyph * 0.5f,
				glyph, glyph), UiIcon.Plus, hovered ? UiPalette.Brand : UiPalette.Ink2);

			UiText.Draw(new Rect(rect.x, plusRect.yMax + UiMetrics.EmptyCardGap, rect.width, labelHeight),
				"DreamsOutposts.EmptySlot".Translate(), UiFont.Body,
				hovered ? UiPalette.BrandText : UiPalette.Ink, TextAnchor.MiddleCenter, true);

			UiText.Draw(new Rect(rect.x, plusRect.yMax + UiMetrics.EmptyCardGap + labelHeight,
				rect.width, hintHeight),
				"DreamsOutposts.Ui.EmptySlotHint".Translate(index + 1), UiFont.Body,
				ink, TextAnchor.MiddleCenter, false, false, true);

			UiWidgets.Tip(rect, "DreamsOutposts.EmptySlotTip".Translate(),
				GenText.StableStringHash("empty-slot-" + index));
			UiDebug.Scope("empty.slot[" + index + "]", rect);

			if (Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null && outpost.extensionSlots != null &&
					index >= 0 && index < outpost.extensionSlots.Count)
				{
					shell.OpenInstallModal(outpost.extensionSlots[index], index);
				}
			}
		}
	}
}
