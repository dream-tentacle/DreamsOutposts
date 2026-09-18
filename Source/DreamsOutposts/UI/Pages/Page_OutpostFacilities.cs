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

		private OutpostUiCache fallbackCache;

		private int observedLevel = -1;

		private float upgradeFlashStartedAt = float.NegativeInfinity;

		/// <summary>刚建成的扩展槽位：套用和等级卡同款的闪光；未触发过时槽位号是 -1。</summary>
		private string[] observedSlotFacilities;

		private int builtFlashSlotIndex = -1;

		private float builtFlashStartedAt = float.NegativeInfinity;

		/// <summary>滚动画布的可见上下边（正文坐标系），用于挡掉滚出可视区的卡片点击。</summary>
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
			// 记录滚动画布的可见上下边：滚出可视区的卡片仍会收到鼠标事件，整卡点击要靠它挡住
			viewportTop = rect.y;
			viewportBottom = rect.y + availableHeight;
			Layout(rect, true);
		}

		// ---------------------------------------------------------------

		private float Layout(Rect rect, bool draw)
		{
			switch (DreamsOutpostsMod.UiStyle)
			{
			case OutpostUiStyle.Vanilla:
				return LayoutVanilla(rect, draw);

			case OutpostUiStyle.ModernTech:
			default:
				return LayoutModernTech(rect, draw);
			}
		}

		/// <summary>
		/// 原版风设施页。
		/// 这部分故意保留改造前的布局、间距和卡片测量，现代科技风不得复用这里的几何常量。
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
			y += SectionHead(rect.x, y, width, "DreamsOutposts.CoreFacility".Translate(), null, draw);
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
				draw);

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
			bool draw)
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

				if (!string.IsNullOrEmpty(hint))
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
				float brandWidth = Mathf.Min(44f, width);
				UiDraw.Solid(
					new Rect(x, ruleY, brandWidth, 2f),
					UiPalette.Brand);

				if (width > brandWidth + 1f)
				{
					UiDraw.Solid(
						new Rect(
							x + brandWidth,
							ruleY,
							width - brandWidth,
							1f),
						UiPalette.WithAlpha(UiPalette.Line, 0.80f));
				}
			}

			return titleHeight + UiMetrics.ModernTechSectionHeadMarginBottom;
		}

		private static bool ModernTechLevelStacked(float width)
		{
			return width < UiMetrics.ModernTechLevelStackBreakpoint;
		}

		private static float MeasureModernTechLevelArea(float width, OutpostUiCache cache)
		{
			const float factsGap = 18f;
			const float indexGap = 10f;

			float factsHeight =
				UiText.LineHeight(UiFont.Body) * 2f + 10f;

			float indexHeight =
				UiText.LineHeight(UiFont.Body);

			bool stacked =
				ModernTechLevelStacked(width);

			if (stacked)
			{
				float leftHeight =
					MeasureModernTechLevelLeft(width, cache);

				float rightHeight =
					MeasureLevelRight(
						width -
							UiMetrics.ModernTechLevelPanelPadding * 2f,
						cache) +
					UiMetrics.ModernTechLevelPanelPadding * 2f;

				return leftHeight +
					UiMetrics.ModernTechLevelGap +
					rightHeight +
					factsGap +
					factsHeight +
					indexGap +
					indexHeight;
			}

			float rightWidth = Mathf.Clamp(
				width * UiMetrics.ModernTechLevelRightRatio,
				UiMetrics.ModernTechLevelRightMinWidth,
				UiMetrics.ModernTechLevelRightMaxWidth);

			float leftWidth = Mathf.Max(
				width - rightWidth - UiMetrics.ModernTechLevelGap,
				120f);

			float left =
				MeasureModernTechLevelLeft(leftWidth, cache);

			float right =
				MeasureLevelRight(
					Mathf.Max(
						rightWidth -
							UiMetrics.ModernTechLevelPanelPadding * 2f,
						60f),
					cache) +
				UiMetrics.ModernTechLevelPanelPadding * 2f;

			float firstRowHeight =
				Mathf.Max(left, right);

			return firstRowHeight +
				factsGap +
				factsHeight +
				indexGap +
				indexHeight;
		}

		private void DrawModernTechLevelArea(Rect rect, OutpostUiCache cache)
		{
			const float factsGap = 18f;
			const float indexGap = 10f;

			float factsHeight =
				UiText.LineHeight(UiFont.Body) * 2f + 10f;

			float indexHeight =
				UiText.LineHeight(UiFont.Body);

			bool stacked =
				ModernTechLevelStacked(rect.width);

			if (stacked)
			{
				float stackedLeftHeight =
					MeasureModernTechLevelLeft(
						rect.width,
						cache);

				DrawModernTechLevelLeft(
					new Rect(
						rect.x,
						rect.y,
						rect.width,
						stackedLeftHeight),
					cache,
					outpost.level);

				float panelY =
					rect.y +
					stackedLeftHeight +
					UiMetrics.ModernTechLevelGap;

				float panelHeight =
					MeasureLevelRight(
						Mathf.Max(
							rect.width -
								UiMetrics.ModernTechLevelPanelPadding * 2f,
							60f),
						cache) +
					UiMetrics.ModernTechLevelPanelPadding * 2f;

				DrawModernTechUpgradePanel(
					new Rect(
						rect.x,
						panelY,
						rect.width,
						panelHeight),
					cache);

				float stackedFactsY =
					panelY +
					panelHeight +
					factsGap;

				DrawModernTechLevelFacts(
					new Rect(
						rect.x,
						stackedFactsY,
						rect.width,
						factsHeight),
					cache);

				float stackedIndexY =
					stackedFactsY +
					factsHeight +
					indexGap;

				DrawModernTechOutpostIndex(
					new Rect(
						rect.x,
						stackedIndexY,
						rect.width,
						indexHeight));

				return;
			}

			float rightWidth = Mathf.Clamp(
				rect.width * UiMetrics.ModernTechLevelRightRatio,
				UiMetrics.ModernTechLevelRightMinWidth,
				UiMetrics.ModernTechLevelRightMaxWidth);

			float leftWidth = Mathf.Max(
				rect.width - rightWidth - UiMetrics.ModernTechLevelGap,
				120f);

			float leftHeight =
				MeasureModernTechLevelLeft(
					leftWidth,
					cache);

			float rightHeight =
				MeasureLevelRight(
					Mathf.Max(
						rightWidth -
							UiMetrics.ModernTechLevelPanelPadding * 2f,
						60f),
					cache) +
				UiMetrics.ModernTechLevelPanelPadding * 2f;

			float firstRowHeight =
				Mathf.Max(
					leftHeight,
					rightHeight);

			Rect leftRect = new Rect(
				rect.x,
				rect.y,
				leftWidth,
				firstRowHeight);

			Rect rightRect = new Rect(
				rect.xMax - rightWidth,
				rect.y,
				rightWidth,
				rightHeight);

			// 第一视觉层：等级和升级操作并列。
			DrawModernTechLevelLeft(
				leftRect,
				cache,
				outpost.level);

			UiDraw.Solid(
				new Rect(
					rightRect.x -
						UiMetrics.ModernTechLevelGap * 0.5f,
					rect.y + 4f,
					1f,
					Mathf.Max(firstRowHeight - 8f, 1f)),
				UiPalette.WithAlpha(
					UiPalette.LineStrong,
					0.42f));

			DrawModernTechUpgradePanel(
				rightRect,
				cache);

			// 第二视觉层：四项状态横跨整行，并主动弱化。
			float factsY =
				rect.y +
				firstRowHeight +
				factsGap;

			DrawModernTechLevelFacts(
				new Rect(
					rect.x,
					factsY,
					rect.width,
					factsHeight),
				cache);

			// 第三视觉层：据点编号收尾。
			float indexY =
				factsY +
					factsHeight +
					indexGap;

			DrawModernTechOutpostIndex(
				new Rect(
					rect.x,
					indexY,
					rect.width,
					indexHeight));
		}

		private void DrawModernTechUpgradePanel(Rect rect, OutpostUiCache cache)
		{
			UiDraw.Box(
				rect,
				(int)UiMetrics.RadiusSm,
				UiPalette.PanelGlassStrong,
				UiPalette.Line);

			UiDraw.Solid(
				new Rect(
					rect.x,
					rect.y,
					Mathf.Min(72f, rect.width),
					3f),
				UiPalette.Brand);

			Rect inner = rect.ContractedBy(UiMetrics.ModernTechLevelPanelPadding);
			DrawLevelRight(
				new Rect(inner.x, inner.y, inner.width, 0f),
				cache);

			DrawUpgradeFlash(rect, upgradeFlashStartedAt);
		}

		private void DrawModernTechOutpostIndex(Rect rect)
		{
			string tileId = Mathf.Max(outpost.Tile.tileId, 0).ToString("D4");
			string text = "DreamsOutposts.Ui.OutpostIndex".Translate(tileId).ToString();

			// 与事实行的标签同级：同一档字号、同一浅灰、不加粗，避免这行收尾文字比状态标签更重。
			UiText.Draw(
				new Rect(
					rect.x,
					rect.y,
					Mathf.Max(rect.width, 40f),
					rect.height),
				text,
				UiFont.Body,
				UiPalette.Ink3,
				TextAnchor.MiddleLeft,
				false,
				false,
				true);
		}

		// ---------------------------------------------------------------
		// 区块标题
		// ---------------------------------------------------------------

		private static float SectionHeadHeight()
		{
			return UiText.LineHeight(UiFont.Heading) + UiMetrics.SectionHeadMarginBottom;
		}

		private static float SectionHead(float x, float y, float width, string title, string hint, bool draw)
		{
			float titleHeight = UiText.LineHeight(UiFont.Heading);
			float hintHeight = UiText.LineHeight(UiFont.Body);
			float lineHeight = Mathf.Max(titleHeight, hintHeight);

			if (draw)
			{
				UiText.Draw(new Rect(x, y, width * 0.6f, titleHeight), title,
					UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);

				if (!string.IsNullOrEmpty(hint))
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
						UiPalette.Brand,
						0.38f));

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
			float lineHeight = UiText.LineHeight(UiFont.Body);

			float textX = innerX + imageSize + UiMetrics.ModernTechFacilityImageGap;
			float textWidth = Mathf.Max(
				rect.xMax - UiMetrics.ModernTechCardPaddingH - textX,
				30f);

			string rightTag = view.IsCore
				? view.SubLabel
				: (view.SlotIndex + 1).ToString("D2");

			float tagWidth = string.IsNullOrEmpty(rightTag)
				? 0f
				: Mathf.Clamp(
					UiText.Width(rightTag, UiFont.Body, true) + 16f,
					32f,
					Mathf.Max(textWidth * 0.46f, 32f));

			float titleWidth = Mathf.Max(
				textWidth - tagWidth -
				((tagWidth > 0f) ? UiMetrics.ModernTechFacilityTitleTagGap : 0f),
				40f);

			float descriptionWidth = textWidth;

			string displayDescription = FitSingleLine(
				view.Description,
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
				Color fill = hovered
					? UiPalette.PanelGlassStrong
					: UiPalette.PanelGlass;

				Color line = hovered
					? UiPalette.BrandLine
					: UiPalette.Line;

				UiDraw.Box(
					rect,
					(int)UiMetrics.RadiusSm,
					fill,
					line);

				UiDraw.Solid(
					new Rect(
						rect.x,
						rect.y,
						view.IsCore ? Mathf.Min(92f, rect.width) : Mathf.Min(54f, rect.width),
						3f),
					view.IsCore ? UiPalette.Brand : UiPalette.WithAlpha(UiPalette.Brand, 0.65f));

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
						UiPalette.NavActive,
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
					UiFont.Body,
					UiPalette.Ink,
					TextAnchor.MiddleLeft,
					true,
					false,
					true);

				if (tagWidth > 0f)
				{
					Rect tagRect = new Rect(
						textX + textWidth - tagWidth,
						y,
						tagWidth,
						lineHeight + 4f);

					if (view.IsCore)
					{
						// 核心设施标签使用纯平色块，避免 NineSlice 边缘造成“中浅外深”的观感。
						UiDraw.Solid(
							tagRect,
							UiPalette.Brand);

						UiText.Draw(
							tagRect,
							rightTag,
							UiFont.Body,
							Color.black,
							TextAnchor.MiddleCenter,
							true,
							false,
							true);
					}
					else
					{
						UiDraw.Box(
							tagRect,
							(int)UiMetrics.RadiusXs,
							UiPalette.Clear,
							UiPalette.LineStrong);

						UiText.Draw(
							tagRect,
							rightTag,
							UiFont.Body,
							UiPalette.Ink2,
							TextAnchor.MiddleCenter,
							true,
							false,
							true);
					}
				}

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

			if (view.Sections.Count > 0)
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

			for (int i = 0; i < view.Sections.Count; i++)
			{
				if (i > 0)
				{
					y += UiMetrics.ProdListGap;
				}

				y += LayoutFacilitySection(
					innerX,
					y,
					innerWidth,
					view.Sections[i],
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

			float chipsHeight = (view.Chips.Count > 0)
				? UiDraw.ChipsHeight(
					view.Chips,
					innerWidth,
					true)
				: 0f;

			if (!draw || view.Chips.Count == 0)
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
				view.Chips,
				true);

			return chipsHeight;
		}

		private void DrawModernTechEmptySlotCard(Rect rect, int index)
		{
			bool hovered = Mouse.IsOver(rect);
			Color line = hovered
				? UiPalette.BrandLine
				: UiPalette.LineStrong;

			UiDraw.Box(
				rect,
				(int)UiMetrics.RadiusSm,
				hovered
					? UiPalette.WithAlpha(UiPalette.BrandTint, 0.68f)
					: UiPalette.WithAlpha(UiPalette.PanelGlass, 0.72f),
				UiPalette.Clear);

			UiDraw.DashedBox(
				rect,
				(int)UiMetrics.RadiusSm,
				line,
				8f,
				6f);

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
				Mathf.RoundToInt(plusSize * 0.5f),
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
					UiDraw.Box(imageRect, (int)UiMetrics.RadiusSm, UiPalette.NavActive, UiPalette.LineStrong);
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
			for (int i = 0; i < view.Sections.Count; i++)
			{
				if (i > 0)
				{
					y += UiMetrics.ProdListGap;
				}
				y += LayoutFacilitySection(innerX, y, innerWidth, view.Sections[i], draw) + UiMetrics.CardGap;
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

		private float LayoutFacilitySection(float x, float y, float width, UiFacilitySectionView section, bool draw)
		{
			float innerX = x + UiMetrics.ProdPaddingH;
			float innerWidth = Mathf.Max(width - UiMetrics.ProdPaddingH * 2f, 20f);
			float cursor = y + UiMetrics.ProdPaddingV;
			float topHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body));
			if (draw)
			{
				float textX = innerX;
				if (section.IconThing != null)
				{
					Rect icon = new Rect(innerX, cursor + (topHeight - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
					Widgets.ThingIcon(icon, section.IconThing);
					textX += UiMetrics.MatIconSize + 8f;
				}
				bool modernTech =
					DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.ModernTech);

				float mainWidth = string.IsNullOrEmpty(section.MainText)
					? 0f
					: Mathf.Max(
						UiText.Width(section.MainText, UiFont.Number, true) + 6f,
						48f);

				if (modernTech)
				{
					mainWidth = Mathf.Min(
						mainWidth,
						innerWidth * 0.42f);
				}

				float titleWidth = Mathf.Max(
					innerX + innerWidth - textX - mainWidth,
					20f);

				string sectionTitle = modernTech
					? FitSingleLine(
						section.Title ?? string.Empty,
						UiFont.Body,
						titleWidth)
					: section.Title ?? string.Empty;

				UiText.Draw(
					new Rect(
						textX,
						cursor,
						titleWidth,
						topHeight),
					sectionTitle,
					UiFont.Body,
					UiPalette.Ink2,
					TextAnchor.MiddleLeft,
					false,
					false,
					true);

				if (mainWidth > 0f)
				{
					string mainText = modernTech
						? FitSingleLine(
							section.MainText,
							UiFont.Number,
							mainWidth,
							true)
						: section.MainText;

					UiText.Draw(
						new Rect(
							innerX + innerWidth - mainWidth,
							cursor,
							mainWidth,
							topHeight),
						mainText,
						UiFont.Number,
						UiPalette.Ink,
						TextAnchor.MiddleRight,
						true);
				}
			}
			cursor += topHeight;
			if (section.ShowProgress)
			{
				cursor += UiMetrics.ProdGap;
				if (draw)
				{
					Color fill = section.ProgressKind == UiChipKind.Good ? UiPalette.Good : section.ProgressKind == UiChipKind.Warn ? UiPalette.Warn : section.ProgressKind == UiChipKind.Bad ? UiPalette.Bad : UiPalette.Accent;
					UiDraw.Bar(new Rect(innerX, cursor, innerWidth, UiMetrics.BarHeight), Mathf.Clamp01(section.Progress), fill, UiPalette.Track);
				}
				cursor += UiMetrics.BarHeight;
			}
			if (!string.IsNullOrEmpty(section.LeftText) || !string.IsNullOrEmpty(section.RightText))
			{
				cursor += UiMetrics.ProdGap;
				if (draw)
				{
					float h = UiText.LineHeight(UiFont.Body);

					bool modernTech =
						DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.ModernTech);

					float rightWidth =
						string.IsNullOrEmpty(section.RightText)
							? 0f
							: Mathf.Max(
								UiText.Width(section.RightText, UiFont.Body) + 4f,
								60f);

					if (modernTech)
					{
						rightWidth = Mathf.Min(
							rightWidth,
							innerWidth * 0.46f);
					}

					float leftWidth =
						Mathf.Max(innerWidth - rightWidth, 20f);

					string leftText = modernTech
						? FitSingleLine(
							section.LeftText ?? string.Empty,
							UiFont.Body,
							leftWidth)
						: section.LeftText ?? string.Empty;

					UiText.Draw(
						new Rect(
							innerX,
							cursor,
							leftWidth,
							h),
						leftText,
						UiFont.Body,
						UiPalette.Ink2,
						TextAnchor.MiddleLeft,
						false,
						false,
						true);

					if (rightWidth > 0f)
					{
						string rightText = modernTech
							? FitSingleLine(
								section.RightText,
								UiFont.Body,
								rightWidth)
							: section.RightText;

						UiText.Draw(
							new Rect(
								innerX + innerWidth - rightWidth,
								cursor,
								rightWidth,
								h),
							rightText,
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
			if (section.Action != null)
			{
				cursor += UiMetrics.ProdGap;
				float h = UiWidgets.ButtonHeight(UiButtonSize.Small);
				if (draw && UiWidgets.Button(new Rect(innerX, cursor, innerWidth, h), section.ActionLabel ?? "DreamsOutposts.Ui.Switch".Translate(), UiButtonKind.Secondary, true, null, UiButtonSize.Small, section.ActionTooltip)) section.Action();
				cursor += h;
			}
			if (section.Chips.Count > 0)
			{
				cursor += UiMetrics.ProdGap;
				float h = UiDraw.ChipsHeight(section.Chips, innerWidth, true);
				if (draw) UiDraw.Chips(new Rect(innerX, cursor, innerWidth, h), section.Chips, true);
				cursor += h;
			}
			if (draw && !string.IsNullOrEmpty(section.Tooltip)) UiWidgets.Tip(new Rect(x, y, width, cursor + UiMetrics.ProdPaddingV - y), section.Tooltip, GenText.StableStringHash(section.Title ?? "facility-section"));
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
			float chipsHeight = (view.Chips.Count > 0) ? UiDraw.ChipsHeight(view.Chips, innerWidth, true) : 0f;
			if (!draw || view.Chips.Count == 0)
			{
				return chipsHeight;
			}
			// 从底部钉住（等价 .fc-foot 的 margin-top: auto）
			float footerY = Mathf.Max(cardRect.yMax - UiMetrics.CardPaddingBottom - chipsHeight, y);
			UiDraw.Chips(new Rect(innerX, footerY, innerWidth, chipsHeight), view.Chips, true);
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
