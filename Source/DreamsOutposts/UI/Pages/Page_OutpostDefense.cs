using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 「防卫」页：顶部总防卫 + 人员/设施分解条（满值 100），下面两栏明细。
	/// 数值全部来自 OutpostUiCache（本质是 OutpostDefenseUtility），本页不做计算、不改状态。
	/// 人员行整行可点击打开信息卡。
	/// </summary>
	public class Page_OutpostDefense : OutpostManagePage, IUiShellPage
	{
		private const float HeroPaddingH = 20f;

		private const float HeroPaddingV = 18f;

		private const float HeroGap = 26f;

		/// <summary>顶部堆叠条的满值：防卫 100 铺满整条，超过 100 也不画出条外。</summary>
		private const float DefenseBarMax = 100f;

		private const float StackBarHeight = 12f;

		private const float StackBarGap = 9f;

		private const float LegendGap = 16f;

		private const float ColumnsMarginTop = 16f;

		private const float ColumnsGap = 16f;

		private const float TwoColumnMinWidth = UiMetrics.StackBreakpoint;

		private const float PanelMinHeight = 260f;

		private const float PanelBodyMaxHeight = 330f;

		private const float PanelHeadPaddingH = 13f;

		private const float PanelHeadPaddingV = 11f;

		private const float PanelBodyPadding = 6f;

		private const float RowPaddingH = 8f;

		private const float RowPaddingV = 7f;

		private const float RowGap = 9f;

		private const float RowRadius = 6f;

		private const float AvatarSize = 30f;

		private const float ValueWidth = 46f;

		private Vector2 pawnsScroll;

		private Vector2 facilitiesScroll;

		private OutpostUiCache fallbackCache;

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

		public override string Label => "DreamsOutposts.Defense".Translate();

		public string NavSummary => "DreamsOutposts.Ui.Nav.Defense".Translate(Cache.Defense.ToString("0.#")).ToString();

		public string HeadDescription => null;

		public string HeadHint => null;

		public float BodyHeight(float width, float availableHeight)
		{
			return Layout(new Rect(0f, 0f, width, 0f), availableHeight, false);
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			Layout(rect, availableHeight, true);
		}

		// ---------------------------------------------------------------

		private float Layout(Rect rect, float availableHeight, bool draw)
		{
			if (rect.width < 80f)
			{
				return 1f;
			}
			OutpostUiCache cache = Cache;
			float width = rect.width;
			float y = rect.y;
			float heroHeight = HeroHeight();
			if (draw)
			{
				DrawHero(new Rect(rect.x, y, width, heroHeight), cache);
			}
			y += heroHeight + ColumnsMarginTop;
			bool stacked = width < TwoColumnMinWidth;
			if (stacked)
			{
				// 窄屏堆叠：每栏按内容高度，撑不满也无所谓（两栏都撑会把页面顶出去）
				float leftHeight = PanelNeededHeight(cache, true);
				if (draw)
				{
					DrawPanel(new Rect(rect.x, y, width, leftHeight), cache, true);
				}
				y += leftHeight + ColumnsGap;
				float rightHeight = PanelNeededHeight(cache, false);
				if (draw)
				{
					DrawPanel(new Rect(rect.x, y, width, rightHeight), cache, false);
				}
				y += rightHeight;
			}
			else
			{
				float columnWidth = (width - ColumnsGap) * 0.5f;
				float needed = Mathf.Max(PanelNeededHeight(cache, true), PanelNeededHeight(cache, false));
				// 自动撑到可视区底部；内容比可视区更高时用内容高度（页面滚动）
				float fill = Mathf.Max(availableHeight - (heroHeight + ColumnsMarginTop), PanelMinHeight);
				float rowHeight = Mathf.Max(needed, fill);
				if (draw)
				{
					DrawPanel(new Rect(rect.x, y, columnWidth, rowHeight), cache, true);
					DrawPanel(new Rect(rect.x + columnWidth + ColumnsGap, y, columnWidth, rowHeight), cache, false);
				}
				y += rowHeight;
			}
			return Mathf.Max(y - rect.y, 1f);
		}

		// ---------------------------------------------------------------
		// 顶部总览
		// ---------------------------------------------------------------

		private static float HeroHeight()
		{
			float leftHeight = UiText.LineHeight(UiFont.Number);
			float rightHeight = StackBarHeight + StackBarGap + UiText.LineHeight(UiFont.Body);
			return Mathf.Max(leftHeight, rightHeight) + HeroPaddingV * 2f;
		}

		private static void DrawHero(Rect rect, OutpostUiCache cache)
		{
			UiDebug.Scope("defense.hero", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.Line);
			float innerX = rect.x + HeroPaddingH;
			float innerWidth = Mathf.Max(rect.width - HeroPaddingH * 2f, 40f);
			float innerY = rect.y + HeroPaddingV;
			float innerHeight = Mathf.Max(rect.height - HeroPaddingV * 2f, 1f);
			// 左侧：总防卫数字 + 单位
			string totalText = cache.Defense.ToString("0.#");
			float totalWidth = UiText.Width(totalText, UiFont.Number, true);
			UiText.Draw(new Rect(innerX, innerY, totalWidth + 2f, innerHeight), totalText, UiFont.Number, UiPalette.Ink,
				TextAnchor.MiddleLeft, true);
			float unitX = innerX + totalWidth + 8f;
			UiText.Draw(new Rect(unitX, innerY, Mathf.Max(innerWidth - (unitX - innerX), 20f), innerHeight), "DreamsOutposts.Defense".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			// 右侧：分解条 + 图例，整条以 DefenseBarMax 为满值。
			float barX = unitX + UiText.Width("DreamsOutposts.Defense".Translate(), UiFont.Body) + HeroGap;
			float barWidth = Mathf.Max(rect.xMax - HeroPaddingH - barX, 60f);
			float pawnFraction = Mathf.Clamp01(cache.DefenseFromPawns / DefenseBarMax);
			float totalFraction = Mathf.Clamp01(cache.Defense / DefenseBarMax);
			float barY = innerY + (innerHeight - (StackBarHeight + StackBarGap + UiText.LineHeight(UiFont.Body))) * 0.5f;
			DrawStackBar(new Rect(barX, barY, barWidth, StackBarHeight), pawnFraction, totalFraction);
			Rect legendRect = new Rect(barX, barY + StackBarHeight + StackBarGap, barWidth, UiText.LineHeight(UiFont.Body));
			DrawLegend(legendRect, cache);
		}

		/// <summary>
		/// 分解条：轨道满值 = DefenseBarMax。人员段从左侧起，设施段紧随其后，
		/// 两段合计到 min(总防卫, 100)；没到 100 时右侧留空槽，超过 100 也只在条内画满。
		/// </summary>
		private static void DrawStackBar(Rect rect, float pawnFraction, float totalFraction)
		{
			UiDraw.Box(rect, (int)UiMetrics.BarRadius, UiPalette.Track);
			float width = Mathf.Max(rect.width, 1f);
			float filled = Mathf.Min(Mathf.Round(width * Mathf.Clamp01(totalFraction)), width);
			float pawnWidth = Mathf.Min(Mathf.Round(width * Mathf.Clamp01(pawnFraction)), filled);
			float facilityWidth = filled - pawnWidth;
			// 已填部分顶到条尾时才收右圆角，否则右端是条内的直边。
			bool reachesEnd = filled >= width - 0.5f;
			if (pawnWidth > 1f)
			{
				UiCorners corners = UiCorners.TopLeft | UiCorners.BottomLeft;
				if (reachesEnd && facilityWidth <= 1f)
				{
					corners = UiCorners.All;
				}
				UiDraw.Box(new Rect(rect.x, rect.y, pawnWidth, rect.height), (int)UiMetrics.BarRadius, UiPalette.Accent,
					UiPalette.Clear, corners);
			}
			if (facilityWidth > 1f)
			{
				UiCorners corners = reachesEnd ? (UiCorners.TopRight | UiCorners.BottomRight) : UiCorners.None;
				UiDraw.Box(new Rect(rect.x + pawnWidth, rect.y, facilityWidth, rect.height), (int)UiMetrics.BarRadius, UiPalette.Ink,
					UiPalette.Clear, corners);
			}
		}

		private static void DrawLegend(Rect rect, OutpostUiCache cache)
		{
			float x = rect.x;
			x += DrawLegendEntry(x, rect.y, rect.height, UiPalette.Accent, "DreamsOutposts.DefenseFromPawns".Translate(cache.DefenseFromPawns.ToString("0.#")));
			DrawLegendEntry(x, rect.y, rect.height, UiPalette.Ink, "DreamsOutposts.DefenseFromFacilities".Translate(cache.DefenseFromFacilities.ToString("0.#")));
		}

		private static float DrawLegendEntry(float x, float y, float height, Color dotColor, string text)
		{
			float dotSize = 8f;
			UiDraw.Box(new Rect(x, y + (height - dotSize) * 0.5f, dotSize, dotSize), (int)UiMetrics.RadiusXs2, dotColor);
			float textX = x + dotSize + 6f;
			float textWidth = UiText.Width(text, UiFont.Body);
			UiText.Draw(new Rect(textX, y, textWidth + 2f, height), text, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			return dotSize + 6f + textWidth + LegendGap;
		}

		// ---------------------------------------------------------------
		// 两栏面板
		// ---------------------------------------------------------------

		private static float PanelHeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + PanelHeadPaddingV * 2f;
		}

		private static float RowHeight()
		{
			return Mathf.Max(AvatarSize, UiText.LineHeight(UiFont.Body)) + RowPaddingV * 2f;
		}

		private static float PanelBodyHeight(int rowCount)
		{
			float needed = PanelBodyPadding * 2f + ((rowCount > 0) ? rowCount * (RowHeight() + RowGap) - RowGap : UiText.LineHeight(UiFont.Body) + 14f);
			float minimum = PanelMinHeight - PanelHeadHeight();
			return Mathf.Clamp(needed, minimum, PanelBodyMaxHeight);
		}

		private static float PanelNeededHeight(OutpostUiCache cache, bool pawns)
		{
			int rows = pawns ? cache.DefensePawns.Count : FacilityRowCount(cache);
			return PanelHeadHeight() + PanelBodyHeight(rows);
		}

		private static int FacilityRowCount(OutpostUiCache cache)
		{
			int count = 0;
			if (cache.Core != null)
			{
				count++;
			}
			for (int i = 0; i < cache.Slots.Count; i++)
			{
				if (cache.Slots[i] != null)
				{
					count++;
				}
			}
			return count;
		}

		private void DrawPanel(Rect rect, OutpostUiCache cache, bool pawns)
		{
			UiDebug.Scope(pawns ? "defense.panel.pawns" : "defense.panel.facilities", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			float headHeight = PanelHeadHeight();
			Rect head = new Rect(rect.x, rect.y, rect.width, headHeight);
			string title = pawns
				? "DreamsOutposts.DefenseFromPawns".Translate(cache.DefenseFromPawns.ToString("0.#")).ToString()
				: "DreamsOutposts.DefenseFromFacilities".Translate(cache.DefenseFromFacilities.ToString("0.#")).ToString();
			UiText.Draw(new Rect(head.x + PanelHeadPaddingH, head.y, Mathf.Max(head.width - PanelHeadPaddingH * 2f, 20f), head.height), title,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			UiDraw.Divider(new Rect(rect.x, head.yMax - 1f, rect.width, 1f), UiPalette.Line);
			Rect bodyOuter = new Rect(rect.x, head.yMax, rect.width, Mathf.Max(rect.height - headHeight, 0f));
			Rect bodyInner = new Rect(bodyOuter.x + PanelBodyPadding, bodyOuter.y + PanelBodyPadding,
				Mathf.Max(bodyOuter.width - PanelBodyPadding * 2f, 20f), Mathf.Max(bodyOuter.height - PanelBodyPadding * 2f, 20f));
			if (pawns)
			{
				float contentHeight = RowContentHeight(cache.DefensePawns.Count);
				bool scroll = contentHeight > bodyInner.height + 0.5f;
				UiWidgets.ScrollView(bodyInner, ref pawnsScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawPawnRows(contentRect, cache);
				}, scroll, GetHashCode() * 2, scroll);
			}
			else
			{
				float contentHeight = RowContentHeight(FacilityRowCount(cache));
				bool scroll = contentHeight > bodyInner.height + 0.5f;
				UiWidgets.ScrollView(bodyInner, ref facilitiesScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawFacilityRows(contentRect, cache);
				}, scroll, GetHashCode() * 2 + 1, scroll);
			}
		}

		private static float RowContentHeight(int rowCount)
		{
			if (rowCount <= 0)
			{
				return UiText.LineHeight(UiFont.Body) + 14f;
			}
			return rowCount * (RowHeight() + RowGap) - RowGap;
		}

		private void DrawPawnRows(Rect rect, OutpostUiCache cache)
		{
			if (cache.DefensePawns.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body) + 14f), "DreamsOutposts.None".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}
			float rowHeight = RowHeight();
			for (int i = 0; i < cache.DefensePawns.Count; i++)
			{
				UiPawnView view = cache.DefensePawns[i];
				Rect row = new Rect(rect.x, rect.y + (rowHeight + RowGap) * i, rect.width, rowHeight);
				bool hovered = Mouse.IsOver(row);
				if (hovered)
				{
					UiDraw.Box(row, (int)RowRadius, UiPalette.Hover);
				}
				float x = row.x + RowPaddingH;
				// 原版人物小像（PortraitsCache 渲染，取景参数见 UiDraw.PawnPortrait）
				Rect avatar = new Rect(x, row.y + (row.height - AvatarSize) * 0.5f, AvatarSize, AvatarSize);
				UiDraw.PawnPortrait(avatar, view.Pawn);
				x += AvatarSize + RowGap;
				// 防卫值
				string valueText = view.Defense.ToString();
				float valueWidth = Mathf.Max(UiText.Width(valueText, UiFont.Body, true), ValueWidth);
				Rect valueRect = new Rect(row.xMax - RowPaddingH - valueWidth, row.y, valueWidth, row.height);
				UiText.Draw(valueRect, valueText, UiFont.Body, (view.Defense == 0) ? UiPalette.Ink2 : UiPalette.Ink,
					TextAnchor.MiddleRight, view.Defense != 0);
				// 技能徽标
				float shootingWidth = SkillBadgeWidth(SkillDefOf.Shooting.LabelCap, view.Shooting);
				float meleeWidth = SkillBadgeWidth(SkillDefOf.Melee.LabelCap, view.Melee);
				float badgesWidth = shootingWidth + 5f + meleeWidth;
				float badgesX = valueRect.x - 6f - badgesWidth;
				float chipHeight = UiDraw.ChipHeight(true);
				float chipY = row.y + (row.height - chipHeight) * 0.5f;
				DrawSkillBadge(new Rect(badgesX, chipY, shootingWidth, chipHeight), SkillDefOf.Shooting.LabelCap, view.Shooting);
				DrawSkillBadge(new Rect(badgesX + shootingWidth + 5f, chipY, meleeWidth, chipHeight), SkillDefOf.Melee.LabelCap, view.Melee);
				// 名字
				float nameWidth = Mathf.Max(badgesX - 5f - x, 30f);
				UiText.Draw(new Rect(x, row.y, nameWidth, row.height), view.Name, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
				if (view.Pawn != null && Widgets.ButtonInvisible(row))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(view.Pawn));
				}
			}
		}

		private static float SkillBadgeWidth(string skillLabel, int level)
		{
			return UiDraw.ChipWidth(MakeSkillBadge(skillLabel, level));
		}

		private static void DrawSkillBadge(Rect rect, string skillLabel, int level)
		{
			UiDraw.Chip(rect, MakeSkillBadge(skillLabel, level));
		}

		private static UiChipView MakeSkillBadge(string skillLabel, int level)
		{
			UiChipView chip = (level < 0)
				? new UiChipView("DreamsOutposts.Ui.DefenseSkillDisabled".Translate(skillLabel).ToString(), UiChipKind.Bad)
				: new UiChipView("DreamsOutposts.Ui.DefenseSkill".Translate(skillLabel, level).ToString());
			chip.Small = true;
			return chip;
		}

		private void DrawFacilityRows(Rect rect, OutpostUiCache cache)
		{
			List<UiFacilityView> rows = new List<UiFacilityView>();
			if (cache.Core != null)
			{
				rows.Add(cache.Core);
			}
			for (int i = 0; i < cache.Slots.Count; i++)
			{
				if (cache.Slots[i] != null)
				{
					rows.Add(cache.Slots[i]);
				}
			}
			if (rows.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body) + 14f), "DreamsOutposts.None".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}
			float rowHeight = RowHeight();
			for (int i = 0; i < rows.Count; i++)
			{
				UiFacilityView view = rows[i];
				Rect row = new Rect(rect.x, rect.y + (rowHeight + RowGap) * i, rect.width, rowHeight);
				if (Mouse.IsOver(row))
				{
					UiDraw.Box(row, (int)RowRadius, UiPalette.Hover);
				}
				float x = row.x + RowPaddingH;
				Rect iconRect = new Rect(x, row.y + (row.height - AvatarSize) * 0.5f, AvatarSize, AvatarSize);
				UiDraw.Box(iconRect, (int)UiMetrics.RadiusXs, UiPalette.Raised, UiPalette.Line);
				float glyph = Mathf.Round(AvatarSize * 0.55f);
				UiDraw.Icon(new Rect(iconRect.center.x - glyph * 0.5f, iconRect.center.y - glyph * 0.5f, glyph, glyph), view.Icon, UiPalette.Ink);
				x += AvatarSize + RowGap;
				string valueText = view.Defense.ToString("0.#");
				float valueWidth = Mathf.Max(UiText.Width(valueText, UiFont.Body, true), ValueWidth);
				Rect valueRect = new Rect(row.xMax - RowPaddingH - valueWidth, row.y, valueWidth, row.height);
				UiText.Draw(valueRect, valueText, UiFont.Body, (view.Defense == 0f) ? UiPalette.Ink2 : UiPalette.Ink,
					TextAnchor.MiddleRight, view.Defense != 0f);
				float nameHeight = UiText.LineHeight(UiFont.Body);
				float subHeight = UiText.LineHeight(UiFont.Body);
				float textY = row.y + (row.height - (nameHeight + subHeight)) * 0.5f;
				float textWidth = Mathf.Max(valueRect.x - 6f - x, 30f);
				UiText.Draw(new Rect(x, textY, textWidth, nameHeight), view.Label, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
				UiText.Draw(new Rect(x, textY + nameHeight, textWidth, subHeight), view.SubLabel, UiFont.Body, UiPalette.Ink2,
					TextAnchor.MiddleLeft, false, false, true);
			}
		}
	}
}
