using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostInventory : OutpostManagePage, IUiShellPage
	{
		private const float PanelMinHeight = 260f;

		private const float PanelBodyMaxHeight = 330f;

		private const float PanelHeadPaddingH = 13f;

		private const float PanelHeadPaddingV = 11f;

		private const float PanelBodyPadding = 6f;

		private const float RowPaddingH = 8f;

		private const float RowPaddingV = 7f;

		private const float RowGap = 9f;

		private const float RowRadius = 6f;

		private const float IconSize = 30f;

		private const float BadgePaddingH = 7f;

		private const float BadgeMinWidth = 20f;

		private const float ColumnsGap = 16f;

		/// <summary>窗口够宽时三栏并排，窄了才塌成上下三块（断点见 UiMetrics.StackBreakpoint）。</summary>
		private const float ThreeColumnMinWidth = UiMetrics.StackBreakpoint;

		private const float ItemsColumnWeight = 1.15f;

		private Vector2 colonistsScroll;

		private Vector2 otherPawnsScroll;

		private Vector2 itemsScroll;
		private Vector2 vehiclesScroll;

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

		public override string Label => "DreamsOutposts.Ui.Warehouse".Translate();

		public string NavSummary => "DreamsOutposts.Ui.Nav.Items".Translate(Cache.Inventory.Count).ToString();

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
			bool stacked = rect.width < ThreeColumnMinWidth;
			bool hasVehicles = cache.Vehicles.Count > 0;
			if (stacked)
			{
				// 窄屏堆叠：各栏按内容高度（三栏都撑满会把页面顶出去）
				float y = rect.y;
				y += Column(new Rect(rect.x, y, rect.width, 0f), cache, 0, draw);
				y += ColumnsGap;
				y += Column(new Rect(rect.x, y, rect.width, 0f), cache, 1, draw);
				y += ColumnsGap;
				y += Column(new Rect(rect.x, y, rect.width, 0f), cache, 2, draw);
				if (hasVehicles)
				{
					y += ColumnsGap;
					y += Column(new Rect(rect.x, y, rect.width, 0f), cache, 3, draw);
				}
				return Mathf.Max(y - rect.y, 1f);
			}
			float weightTotal = 1f + 1f + ItemsColumnWeight + (hasVehicles ? 1f : 0f);
			float available = rect.width - ColumnsGap * (hasVehicles ? 3f : 2f);
			float firstWidth = available * (1f / weightTotal);
			float secondWidth = available * (1f / weightTotal);
			float thirdWidth = available * (ItemsColumnWeight / weightTotal);
			Rect firstRect = new Rect(rect.x, rect.y, firstWidth, 0f);
			Rect secondRect = new Rect(firstRect.xMax + ColumnsGap, rect.y, secondWidth, 0f);
			Rect thirdRect = new Rect(secondRect.xMax + ColumnsGap, rect.y, thirdWidth, 0f);
			float firstHeight = ColumnHeight(cache, 0);
			float secondHeight = ColumnHeight(cache, 1);
			float thirdHeight = ColumnHeight(cache, 2);
			float fourthHeight = hasVehicles ? ColumnHeight(cache, 3) : 0f;
			// 自动撑到可视区底部；内容更高时用内容高度（页面滚动）
			float rowHeight = Mathf.Max(Mathf.Max(Mathf.Max(firstHeight, secondHeight), Mathf.Max(thirdHeight, fourthHeight)),
				Mathf.Max(availableHeight, PanelMinHeight));
			if (draw)
			{
				DrawColumn(new Rect(firstRect.x, firstRect.y, firstRect.width, rowHeight), cache, 0);
				DrawColumn(new Rect(secondRect.x, secondRect.y, secondRect.width, rowHeight), cache, 1);
				DrawColumn(new Rect(thirdRect.x, thirdRect.y, thirdRect.width, rowHeight), cache, 2);
				if (hasVehicles)
					DrawColumn(new Rect(thirdRect.xMax + ColumnsGap, rect.y, firstWidth, rowHeight), cache, 3);
			}
			return rowHeight;
		}

		private float Column(Rect rect, OutpostUiCache cache, int index, bool draw)
		{
			float height = ColumnHeight(cache, index);
			if (draw)
			{
				DrawColumn(new Rect(rect.x, rect.y, rect.width, height), cache, index);
			}
			return height;
		}

		private float ColumnHeight(OutpostUiCache cache, int index)
		{
			return PanelHeadHeight() + PanelBodyHeight(RowCount(cache, index));
		}

		private static int RowCount(OutpostUiCache cache, int index)
		{
			switch (index)
			{
			case 0:
				return cache.Colonists.Count;
			case 1:
				return cache.OtherPawns.Count;
			case 2:
				return cache.Inventory.Count;
			default:
				return cache.Vehicles.Count;
			}
		}

		private static string ColumnTitle(int index)
		{
			switch (index)
			{
			case 0:
				return "DreamsOutposts.Colonists".Translate().ToString();
			case 1:
				return "DreamsOutposts.OtherPawns".Translate().ToString();
			case 2:
				return "DreamsOutposts.Items".Translate().ToString();
			default:
				return "DreamsOutposts.Ui.Vehicles".Translate().ToString();
			}
		}

		private static float PanelHeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + PanelHeadPaddingV * 2f;
		}

		private static float RowHeight()
		{
			return Mathf.Max(IconSize, UiText.LineHeight(UiFont.Body)) + RowPaddingV * 2f;
		}

		private static float PanelBodyHeight(int rowCount)
		{
			float needed = PanelBodyPadding * 2f + ((rowCount > 0) ? rowCount * (RowHeight() + RowGap) - RowGap : UiText.LineHeight(UiFont.Body) + 14f);
			float minimum = PanelMinHeight - PanelHeadHeight();
			return Mathf.Clamp(needed, minimum, PanelBodyMaxHeight);
		}

		private void DrawColumn(Rect rect, OutpostUiCache cache, int index)
		{
			UiDebug.Scope("warehouse.panel[" + index + "]", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			float headHeight = PanelHeadHeight();
			Rect head = new Rect(rect.x, rect.y, rect.width, headHeight);
			float titleWidth = Mathf.Max(rect.width - PanelHeadPaddingH * 2f - 60f, 30f);
			UiText.Draw(new Rect(head.x + PanelHeadPaddingH, head.y, titleWidth, head.height), ColumnTitle(index),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			string countText = "(" + RowCount(cache, index) + ")";
			float countWidth = UiText.Width(countText, UiFont.Body) + 4f;
			UiText.Draw(new Rect(head.xMax - PanelHeadPaddingH - countWidth, head.y, countWidth, head.height), countText,
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight);
			UiDraw.Divider(new Rect(rect.x, head.yMax - 1f, rect.width, 1f), UiPalette.Line);
			Rect bodyOuter = new Rect(rect.x, head.yMax, rect.width, Mathf.Max(rect.height - headHeight, 0f));
			Rect bodyInner = new Rect(bodyOuter.x + PanelBodyPadding, bodyOuter.y + PanelBodyPadding,
				Mathf.Max(bodyOuter.width - PanelBodyPadding * 2f, 20f), Mathf.Max(bodyOuter.height - PanelBodyPadding * 2f, 20f));
			float contentHeight = RowContentHeight(RowCount(cache, index));
			bool scroll = contentHeight > bodyInner.height + 0.5f;
			int id = GetHashCode() * 4 + index + 1;
			switch (index)
			{
			case 0:
				UiWidgets.ScrollView(bodyInner, ref colonistsScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawPawnRows(contentRect, cache.Colonists);
				}, scroll, id, scroll);
				break;
			case 1:
				UiWidgets.ScrollView(bodyInner, ref otherPawnsScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawPawnRows(contentRect, cache.OtherPawns);
				}, scroll, id, scroll);
				break;
			case 2:
				UiWidgets.ScrollView(bodyInner, ref itemsScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawItemRows(contentRect, cache.Inventory);
				}, scroll, id, scroll);
				break;
			default:
				UiWidgets.ScrollView(bodyInner, ref vehiclesScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawPawnRows(contentRect, cache.Vehicles);
				}, scroll, id, scroll);
				break;
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

		/// <summary>人员行：头像 + 名字，整行可打开信息卡。</summary>
		private void DrawPawnRows(Rect rect, List<UiPawnView> rows)
		{
			if (rows.Count == 0)
			{
				DrawEmpty(rect);
				return;
			}
			float rowHeight = RowHeight();
			for (int i = 0; i < rows.Count; i++)
			{
				UiPawnView view = rows[i];
				Rect row = new Rect(rect.x, rect.y + (rowHeight + RowGap) * i, rect.width, rowHeight);
				if (Mouse.IsOver(row))
				{
					UiDraw.Box(row, (int)RowRadius, UiPalette.Hover);
				}
				float x = row.x + RowPaddingH;
				// 原版人物小像（PortraitsCache 渲染，取景参数见 UiDraw.PawnPortrait）
				UiDraw.PawnPortrait(new Rect(x, row.y + (row.height - IconSize) * 0.5f, IconSize, IconSize), view.Pawn);
				x += IconSize + RowGap;
				float nameWidth = Mathf.Max(row.xMax - RowPaddingH - x, 30f);
				UiText.Draw(new Rect(x, row.y, nameWidth, row.height), view.Name, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
				if (view.Pawn != null && Widgets.ButtonInvisible(row))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(view.Pawn));
				}
			}
		}

		/// <summary>物品行：原版物品图标 + 名字 + DefName + 总数徽标，整行可打开信息卡。</summary>
		private void DrawItemRows(Rect rect, List<UiItemStackView> rows)
		{
			if (rows.Count == 0)
			{
				DrawEmpty(rect);
				return;
			}
			float rowHeight = RowHeight();
			for (int i = 0; i < rows.Count; i++)
			{
				UiItemStackView view = rows[i];
				Rect row = new Rect(rect.x, rect.y + (rowHeight + RowGap) * i, rect.width, rowHeight);
				if (Mouse.IsOver(row))
				{
					UiDraw.Box(row, (int)RowRadius, UiPalette.Hover);
				}
				float x = row.x + RowPaddingH;
				Rect iconRect = new Rect(x, row.y + (row.height - IconSize) * 0.5f, IconSize, IconSize);
				x += IconSize + RowGap;
				// 总数徽标
				string countText = view.Count.ToString();
				float badgeWidth = Mathf.Max(UiText.Width(countText, UiFont.Body, true) + BadgePaddingH * 2f, BadgeMinWidth);
				float badgeHeight = UiText.LineHeight(UiFont.Body) + 4f;
				Rect badgeRect = new Rect(row.xMax - RowPaddingH - badgeWidth, row.y + (row.height - badgeHeight) * 0.5f, badgeWidth, badgeHeight);
				UiDraw.Box(badgeRect, (int)UiMetrics.RadiusXs, UiPalette.Raised, UiPalette.Line);
				UiText.Draw(badgeRect, countText, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleCenter, true);
				// 名字 + DefName 副行
				float nameHeight = UiText.LineHeight(UiFont.Body);
				float subHeight = UiText.LineHeight(UiFont.Body);
				float textY = row.y + (row.height - (nameHeight + subHeight)) * 0.5f;
				float textWidth = Mathf.Max(badgeRect.x - 6f - x, 30f);
				Rect nameRect = new Rect(x, textY, textWidth, nameHeight);
				Widgets.ThingIcon(iconRect, view.Def);
				UiText.Draw(nameRect, (view.Def != null) ? view.Def.LabelCap.ToString() : "-", UiFont.Body, UiPalette.Ink,
					TextAnchor.MiddleLeft, false, false, true);
				string sub = (view.Def != null) ? view.Def.defName : null;
				UiText.Draw(new Rect(x, textY + nameHeight, textWidth, subHeight), sub, UiFont.Body, UiPalette.Ink2,
					TextAnchor.MiddleLeft, false, false, true);
				if (view.Def != null && Widgets.ButtonInvisible(row))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(view.Def));
				}
			}
		}

		private static void DrawEmpty(Rect rect)
		{
			UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body) + 14f), "DreamsOutposts.None".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
		}
	}
}
