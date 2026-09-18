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
			height += UiMetrics.UpgradeButtonHeight * 1.5f;
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

			float currentHeight = UiMetrics.LevelCurrentDigitHeight;
			float currentWidth = DigitWidth(currentDigit, currentHeight, 48f);
			float maxHeight = UiMetrics.LevelMaxDigitHeight;
			float maxWidth = DigitWidth(maxDigit, maxHeight, 28f);

			float currentX = rect.x;
			Rect currentRect = new Rect(currentX, y, currentWidth, currentHeight);
			DrawLevelDigit(currentRect, currentDigit, level.ToString(), Color.white, 1f);

			float slashWidth = 24f;
			float slashX = currentRect.xMax + UiMetrics.LevelDigitGap;
			Rect slashRect = new Rect(slashX, y + currentHeight - maxHeight - 3f, slashWidth, maxHeight);
			UiText.Draw(slashRect, "/", UiFont.Heading, UiPalette.Ink2,
				TextAnchor.MiddleCenter, false, false, false);

			float maxX = slashRect.xMax + UiMetrics.LevelDigitGap;
			Rect maxRect = new Rect(maxX, y + currentHeight - maxHeight, maxWidth, maxHeight);
			DrawLevelDigit(maxRect, maxDigit, cache.MaxLevel.ToString(), Color.white, 0.58f);

			y += currentHeight + UiMetrics.LevelDigitRowGap;

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
			if (DreamsOutpostsMod.UseVanillaUi)
			{
				float vanillaAlpha = Mathf.Clamp01(alpha * 1.2f);

				if (texture == null)
				{
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

				Color previous = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, vanillaAlpha);
				GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
				GUI.color = previous;
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

				if (DreamsOutpostsMod.UseVanillaUi)
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

			if (DreamsOutpostsMod.UseVanillaUi)
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
				float mainWidth = string.IsNullOrEmpty(section.MainText) ? 0f : Mathf.Max(UiText.Width(section.MainText, UiFont.Number, true) + 6f, 48f);
				UiText.Draw(new Rect(textX, cursor, Mathf.Max(innerX + innerWidth - textX - mainWidth, 20f), topHeight), section.Title ?? string.Empty, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				if (mainWidth > 0f) UiText.Draw(new Rect(innerX + innerWidth - mainWidth, cursor, mainWidth, topHeight), section.MainText, UiFont.Number, UiPalette.Ink, TextAnchor.MiddleRight, true);
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
					float rightWidth = string.IsNullOrEmpty(section.RightText) ? 0f : Mathf.Max(UiText.Width(section.RightText, UiFont.Body) + 4f, 60f);
					UiText.Draw(new Rect(innerX, cursor, Mathf.Max(innerWidth - rightWidth, 20f), h), section.LeftText ?? string.Empty, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
					if (rightWidth > 0f) UiText.Draw(new Rect(innerX + innerWidth - rightWidth, cursor, rightWidth, h), section.RightText, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
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
