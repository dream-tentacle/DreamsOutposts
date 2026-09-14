using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 「设施」页（新样式）：等级卡 + 核心设施 + 扩建设施槽位。
	/// 只负责画，所有数据来自 OutpostUiCache。
	/// </summary>
	public class Page_OutpostFacilities : OutpostManagePage, IUiShellPage
	{
		private const float SectionGap = UiMetrics.SectionSpacing;

		private const float CardHoverShadowAlpha = 0.7f;

		private OutpostUiCache fallbackCache;

		private int observedLevel = -1;

		private float upgradeFlashStartedAt = float.NegativeInfinity;

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

		public string HeadDescription => def?.description;

		public string HeadHint => "DreamsOutposts.Ui.FacilitiesHint".Translate();

		/// <summary>旧入口：本页由新外壳驱动，这里直接画正文以兼容其它宿主。</summary>
		public override void DoContents(Rect rect)
		{
			DrawBody(rect, rect.height);
		}

		public float BodyHeight(float width, float availableHeight)
		{
			return Layout(new Rect(0f, 0f, width, 0f), false);
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			Layout(rect, true);
		}

		// ---------------------------------------------------------------

		private float Layout(Rect rect, bool draw)
		{
			OutpostUiCache cache = Cache;
			if (draw)
			{
				ObserveLevelChange();
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
			return UiText.LineHeight(UiFont.Body) + UiMetrics.SectionHeadMarginBottom;
		}

		private static float SectionHead(float x, float y, float width, string title, string hint, bool draw)
		{
			float lineHeight = UiText.LineHeight(UiFont.Body);
			if (draw)
			{
				UiText.Draw(new Rect(x, y, width * 0.6f, lineHeight), title, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true, false, true);
				if (!string.IsNullOrEmpty(hint))
				{
					float hintWidth = Mathf.Max(width * 0.4f - UiMetrics.SectionHeadGap, 40f);
					UiText.Draw(new Rect(x + width - hintWidth, y, hintWidth, lineHeight), hint, UiFont.Caption, UiPalette.Ink2,
						TextAnchor.UpperRight, false, false, true);
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
			float height = UiMetrics.PipHeight + UiMetrics.PipMarginBottom;
			height += UiText.LineHeight(UiFont.Heading);
			height += UiMetrics.LevelFactsMarginTop + UiDraw.ChipsHeight(cache.LevelChips, width, false);
			return height;
		}

		private static float MeasureLevelRight(float width, OutpostUiCache cache)
		{
			float height = UiText.LineHeight(UiFont.Caption) + 10f;
			if (cache.Upgrade.IsMaxLevel)
			{
				return height;
			}
			float rowHeight = Mathf.Max(Mathf.Max(UiMetrics.ReqTickSize, UiMetrics.MatIconSize), UiText.LineHeight(UiFont.Body));
			height += cache.Upgrade.Checks.Count * (rowHeight + UiMetrics.ReqListGap);
			height += UiMetrics.ReqListMarginBottom;
			height += UiWidgets.ButtonHeight(UiButtonSize.Normal);
			return height;
		}

		private void DrawLevelCard(Rect rect, OutpostUiCache cache)
		{
			UiDebug.Scope("level.card", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.Line);
			DrawLevelUpgradeFlash(rect);
			bool stacked = LevelCardStacked(rect.width);
			float innerX = rect.x + UiMetrics.LevelCardPaddingH;
			float innerWidth = rect.width - UiMetrics.LevelCardPaddingH * 2f;
			float innerY = rect.y + UiMetrics.LevelCardPaddingV;
			float leftWidth = stacked ? innerWidth : Mathf.Max(innerWidth - UiMetrics.LevelCardGap - UiMetrics.LevelRightWidth, 120f);
			DrawLevelLeft(new Rect(innerX, innerY, leftWidth, 0f), cache, outpost.level);
			if (stacked)
			{
				float leftHeight = MeasureLevelLeft(leftWidth, cache);
				float rightTop = innerY + leftHeight + 16f;
				UiDraw.Divider(new Rect(innerX, rightTop - 8f, innerWidth, 1f), UiPalette.Line);
				DrawLevelRight(new Rect(innerX, rightTop, innerWidth, 0f), cache);
			}
			else
			{
				float rightX = innerX + leftWidth + UiMetrics.LevelCardGap;
				UiDraw.Divider(new Rect(rightX - UiMetrics.LevelCardGap * 0.5f, innerY, 1f, rect.height - UiMetrics.LevelCardPaddingV * 2f), UiPalette.Line);
				DrawLevelRight(new Rect(rightX + UiMetrics.LevelCardGap * 0.5f, innerY, innerWidth - leftWidth - UiMetrics.LevelCardGap * 1.5f, 0f), cache);
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

		private void DrawLevelUpgradeFlash(Rect rect)
		{
			float elapsed = Time.realtimeSinceStartup - upgradeFlashStartedAt;
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
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.OnAccent, UiPalette.Clear);
			if (elapsed < UiMetrics.LevelUpgradeSweepDuration)
			{
				DrawLevelUpgradeSweep(rect, elapsed / UiMetrics.LevelUpgradeSweepDuration, previous);
			}
			GUI.color = new Color(previous.r, previous.g, previous.b,
				previous.a * UiMetrics.LevelUpgradeFlashBorderAlpha * strength);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Clear, UiPalette.OnAccent);
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

		private static void DrawLevelLeft(Rect rect, OutpostUiCache cache, int level)
		{
			float x = rect.x;
			float y = rect.y;
			// 等级 pip
			for (int i = 0; i < cache.Pips.Count; i++)
			{
				UiLevelPip pip = cache.Pips[i];
				Color color = UiPalette.Track;
				if (pip.State == UiPipState.Done)
				{
					color = UiPalette.BrandLine;
				}
				else if (pip.State == UiPipState.Current)
				{
					color = UiPalette.Brand;
				}
				UiDraw.Box(new Rect(x, y, UiMetrics.PipWidth, UiMetrics.PipHeight), (int)UiMetrics.RadiusXs2, color);
				UiWidgets.Tip(new Rect(x, y - 4f, UiMetrics.PipWidth, UiMetrics.PipHeight + 8f), pip.Tooltip, GenText.StableStringHash("pip-" + pip.Level));
				x += UiMetrics.PipWidth + UiMetrics.PipGap;
			}
			y += UiMetrics.PipHeight + UiMetrics.PipMarginBottom;
			UiText.Draw(new Rect(rect.x, y, rect.width, UiText.LineHeight(UiFont.Heading)),
				"DreamsOutposts.Level".Translate(level, cache.MaxLevel), UiFont.Heading, UiPalette.Ink, TextAnchor.UpperLeft, true);
			y += UiText.LineHeight(UiFont.Heading) + UiMetrics.LevelFactsMarginTop;
			float chipsHeight = UiDraw.ChipsHeight(cache.LevelChips, rect.width, false);
			UiDraw.Chips(new Rect(rect.x, y, rect.width, chipsHeight), cache.LevelChips, false);
		}

		private void DrawLevelRight(Rect rect, OutpostUiCache cache)
		{
			float y = rect.y;
			if (cache.Upgrade.IsMaxLevel)
			{
				// 满级：只留一行文字说明，不再显示绿色「最高等级」条
				UiText.Draw(new Rect(rect.x, y, rect.width, UiText.LineHeight(UiFont.Caption)), cache.Upgrade.MaxLevelText,
					UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
				return;
			}
			if (!string.IsNullOrEmpty(cache.Upgrade.Title))
			{
				UiText.Draw(new Rect(rect.x, y, rect.width, UiText.LineHeight(UiFont.Caption)), cache.Upgrade.Title,
					UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
			}
			y += UiText.LineHeight(UiFont.Caption) + 10f;
			float rowHeight = Mathf.Max(Mathf.Max(UiMetrics.ReqTickSize, UiMetrics.MatIconSize), UiText.LineHeight(UiFont.Body));
			for (int i = 0; i < cache.Upgrade.Checks.Count; i++)
			{
				UiUpgradeCheck check = cache.Upgrade.Checks[i];
				Rect row = new Rect(rect.x, y, rect.width, rowHeight);
				float valueWidth = Mathf.Max(UiText.Width(check.ValueText, UiFont.Caption, true), 60f);
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
					UiFont.Caption, check.Ok ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleRight, true);
				y += rowHeight + UiMetrics.ReqListGap;
			}
			y += UiMetrics.ReqListMarginBottom - UiMetrics.ReqListGap;
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			Rect buttonRect = new Rect(rect.x, y, UiWidgets.ButtonWidth(cache.Upgrade.ButtonLabel), buttonHeight);
			// 点「升级」：天数与材料都满足时真正升级（槽位数量随等级变化，升级后会重建缓存）
			string pendingTip = cache.Upgrade.CanUpgrade ? "DreamsOutposts.Ui.Upgrade.ConfirmTip".Translate().ToString() : cache.Upgrade.Reason;
			if (UiWidgets.Button(buttonRect, cache.Upgrade.ButtonLabel, UiButtonKind.Primary, cache.Upgrade.CanUpgrade,
				cache.Upgrade.CanUpgrade ? null : cache.Upgrade.Reason, UiButtonSize.Normal, pendingTip))
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
			float headHeight = Mathf.Max(UiMetrics.CardIconSize, UiText.LineHeight(UiFont.Body) + UiText.LineHeight(UiFont.Caption));
			// 头部（图标 + 名称 + 副标题）：整块可点，点开原版信息面板
			if (draw)
			{
				Rect headRect = new Rect(innerX, y, innerWidth, headHeight);
				bool headHovered = Mouse.IsOver(headRect);
				if (hovered)
				{
					UiDraw.Shadow(rect, (int)UiMetrics.RadiusSm, CardHoverShadowAlpha);
				}
				UiDraw.Box(rect, (int)UiMetrics.RadiusSm, view.IsCore ? UiPalette.Raised : UiPalette.Card, hovered ? UiPalette.LineStrong : UiPalette.Line);
				Rect iconRect = new Rect(innerX, y, UiMetrics.CardIconSize, UiMetrics.CardIconSize);
				UiDraw.Box(iconRect, (int)UiMetrics.RadiusSm, view.IsCore ? UiPalette.Card : UiPalette.Raised, UiPalette.Line);
				float glyph = UiMetrics.CardIconGlyph;
				UiDraw.Icon(new Rect(iconRect.center.x - glyph * 0.5f, iconRect.center.y - glyph * 0.5f, glyph, glyph), view.Icon, UiPalette.Ink);
				float nameX = iconRect.xMax + UiMetrics.CardGap;
				float nameWidth = Mathf.Max(rect.xMax - UiMetrics.CardPaddingH - nameX, 30f);
				UiText.Draw(new Rect(nameX, y, nameWidth, UiText.LineHeight(UiFont.Body)), view.Label, UiFont.Body,
					headHovered ? UiPalette.BrandText : UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
				UiText.Draw(new Rect(nameX, y + UiText.LineHeight(UiFont.Body), nameWidth, UiText.LineHeight(UiFont.Caption)), view.SubLabel,
					UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				UiWidgets.Tip(headRect, view.TooltipGetter, view.TooltipId);
				if (!view.IsCore)
				{
					float tagWidth = UiText.Width(view.SlotIndex + 1 + "", UiFont.Caption);
					UiText.Draw(new Rect(rect.xMax - UiMetrics.SlotTagRight - tagWidth, rect.y + UiMetrics.SlotTagTop, tagWidth, UiText.LineHeight(UiFont.Caption)),
						(view.SlotIndex + 1).ToString(), UiFont.Caption, UiPalette.Ink2);
				}
				UiDebug.Scope("facility.rect", rect);
				// 点名称/图标 → 原版信息面板（设施的 Def）
				if (view.Facility?.def != null && Widgets.ButtonInvisible(headRect))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(view.Facility.def));
				}
			}
			y += headHeight + UiMetrics.CardGap;
			// 设施组件声明的功能区块；没有区块时中间直接留空，不再放「没有生产」占位文案
			for (int i = 0; i < view.Sections.Count; i++)
			{
				if (i > 0)
				{
					y += UiMetrics.ProdListGap;
				}
				y += LayoutFacilitySection(innerX, y, innerWidth, view.Sections[i], draw) + UiMetrics.CardGap;
			}
			// footer
			float footerHeight = LayoutFacilityFooter(rect, view, y, draw);
			float natural = y + footerHeight - rect.y + UiMetrics.CardPaddingBottom;
			if (!draw)
			{
				return natural;
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
					float h = UiText.LineHeight(UiFont.Caption);
					float rightWidth = string.IsNullOrEmpty(section.RightText) ? 0f : Mathf.Max(UiText.Width(section.RightText, UiFont.Caption) + 4f, 60f);
					UiText.Draw(new Rect(innerX, cursor, Mathf.Max(innerWidth - rightWidth, 20f), h), section.LeftText ?? string.Empty, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
					if (rightWidth > 0f) UiText.Draw(new Rect(innerX + innerWidth - rightWidth, cursor, rightWidth, h), section.RightText, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
				}
				cursor += UiText.LineHeight(UiFont.Caption);
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
				Color fill = (production.HasProgress && production.Progress > 0f) ? UiPalette.Accent : UiPalette.ScrollThumb;
				UiDraw.Bar(new Rect(innerX, cursor, innerWidth, UiMetrics.BarHeight), fraction, fill, UiPalette.Track);
			}
			cursor += UiMetrics.BarHeight + UiMetrics.ProdGap;
			if (draw)
			{
				float metaHeight = UiText.LineHeight(UiFont.Caption);
				string every = "DreamsOutposts.Ui.ProductionEvery".Translate(production.IntervalText);
				float everyWidth = Mathf.Max(UiText.Width(every, UiFont.Caption) + 4f, 60f);
				UiText.Draw(new Rect(innerX, cursor, Mathf.Max(innerWidth - everyWidth, 20f), metaHeight), production.MetaText,
					UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
				UiText.Draw(new Rect(innerX + innerWidth - everyWidth, cursor, everyWidth, metaHeight), every,
					UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
			}
			cursor += UiText.LineHeight(UiFont.Caption);
			// 可配置生产规则：整行可点击，沿用生产 worker 提供的选择菜单。
			if (production.HasConfiguration)
			{
				cursor += UiMetrics.ProdGap;
				float rowHeight = Mathf.Max(UiText.LineHeight(UiFont.Caption), 16f) + 10f;
				if (draw)
				{
					Rect row = new Rect(innerX, cursor, innerWidth, rowHeight);
					bool hovered = Mouse.IsOver(row);
					UiDraw.Box(row, (int)UiMetrics.RadiusXs, hovered ? UiPalette.Hover : UiPalette.Raised, UiPalette.Line);
					string configuration = production.ConfigurationSummary ?? string.Empty;
					UiText.Draw(new Rect(row.x + 8f, row.y, Mathf.Max(row.width - 16f - 40f, 20f), row.height), configuration,
						UiFont.Caption, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
					UiText.Draw(new Rect(row.xMax - 8f - 34f, row.y, 34f, row.height), "DreamsOutposts.Ui.Switch".Translate(),
						UiFont.Caption, hovered ? UiPalette.Ink : UiPalette.Ink2, TextAnchor.MiddleRight);
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
			string detailsLabel = "DreamsOutposts.Details".Translate();
			// 「详情」用安装弹窗里那个「建造」按钮的同款底图与同款尺寸算法，只把底图换成信息图标（InfoButton）。
			// 槽位卡原来的「拆除」按钮已删除：拆除入口保留在详情弹窗底部。
			float buttonHeight = (UiWidgets.ButtonHeight(UiButtonSize.Small) + 4f) * 1.2f;
			Texture2D detailsTexture = UiTex.InfoButtonTexture();
			float detailsWidth = ((detailsTexture != null) && detailsTexture.height > 0f)
				? buttonHeight * detailsTexture.width / detailsTexture.height
				: Mathf.Max(UiWidgets.ButtonWidth(detailsLabel, UiButtonSize.Small), 64f) * 3f;
			float chipsLimit = Mathf.Max(innerWidth - detailsWidth - UiMetrics.FootGap, 40f);
			float singleRowChipsHeight = UiDraw.ChipsHeight(view.Chips, chipsLimit, true);
			bool singleRow = singleRowChipsHeight <= UiDraw.ChipHeight(true) + 0.5f;
			float chipsHeight = singleRow ? singleRowChipsHeight : UiDraw.ChipsHeight(view.Chips, innerWidth, true);
			float rowHeight = Mathf.Max(chipsHeight, buttonHeight);
			if (!draw)
			{
				return rowHeight;
			}
			// 从底部钉住（等价 .fc-foot 的 margin-top: auto）
			float footerY = cardRect.yMax - UiMetrics.CardPaddingBottom - rowHeight;
			if (footerY < y)
			{
				footerY = y;
			}
			if (view.Chips.Count > 0)
			{
				UiDraw.Chips(new Rect(innerX, footerY + (rowHeight - chipsHeight) * 0.5f, singleRow ? chipsLimit : innerWidth, chipsHeight), view.Chips, true);
			}
			float buttonY = footerY + (rowHeight - buttonHeight) * 0.5f;
			Rect detailsRect = new Rect(innerX + innerWidth - detailsWidth, buttonY, detailsWidth, buttonHeight);
			// 贴图里的标签区（原始像素坐标）与建造按钮一致
			if (UiWidgets.TexturedPrimaryButton(detailsRect, detailsLabel, detailsTexture, 420f, 300f, UiButtonSize.Normal))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null)
				{
					shell.OpenDetailsModal(view);
				}
			}
			return rowHeight;
		}

		// ---------------------------------------------------------------
		// 空槽位
		// ---------------------------------------------------------------

		private void DrawEmptySlotCard(Rect rect, int index)
		{
			bool hovered = Mouse.IsOver(rect);
			Color line = hovered ? UiPalette.Brand : UiPalette.Line;
			if (hovered)
			{
				UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.BrandTint, UiPalette.Brand);
			}
			else
			{
				UiDraw.DashedBox(rect, (int)UiMetrics.RadiusSm, line);
			}
			Color ink = hovered ? UiPalette.BrandText : UiPalette.Ink2;
			float plusSize = UiMetrics.CardIconSize;
			float labelHeight = UiText.LineHeight(UiFont.Body);
			float hintHeight = UiText.LineHeight(UiFont.Caption);
			float contentHeight = plusSize + UiMetrics.EmptyCardGap + labelHeight + hintHeight;
			float contentY = rect.y + (rect.height - contentHeight) * 0.5f;
			Rect plusRect = new Rect(rect.center.x - plusSize * 0.5f, contentY, plusSize, plusSize);
			if (hovered)
			{
				UiDraw.Box(plusRect, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.BrandLine);
			}
			else
			{
				UiDraw.DashedBox(plusRect, (int)UiMetrics.RadiusSm, line);
			}
			float glyph = Mathf.Round(plusSize * 0.55f);
			UiDraw.Icon(new Rect(plusRect.center.x - glyph * 0.5f, plusRect.center.y - glyph * 0.5f, glyph, glyph), UiIcon.Plus, ink);
			UiText.Draw(new Rect(rect.x, plusRect.yMax + UiMetrics.EmptyCardGap, rect.width, labelHeight), "DreamsOutposts.EmptySlot".Translate(),
				UiFont.Body, hovered ? UiPalette.BrandText : UiPalette.Ink, TextAnchor.MiddleCenter, true);
			UiText.Draw(new Rect(rect.x, plusRect.yMax + UiMetrics.EmptyCardGap + labelHeight, rect.width, hintHeight),
				"DreamsOutposts.Ui.EmptySlotHint".Translate(index + 1), UiFont.Caption, ink, TextAnchor.MiddleCenter, false, false, true);
			UiWidgets.Tip(rect, "DreamsOutposts.EmptySlotTip".Translate(), GenText.StableStringHash("empty-slot-" + index));
			UiDebug.Scope("empty.slot[" + index + "]", rect);
			if (Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null && outpost.extensionSlots != null && index >= 0 && index < outpost.extensionSlots.Count)
				{
					shell.OpenInstallModal(outpost.extensionSlots[index], index);
				}
			}
		}
	}
}
