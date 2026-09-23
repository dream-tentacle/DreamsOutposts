using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

		/// <summary>殖民者栏的「(?)提示」文字常态透明度：原版风格下 Ink3 也是纯白，只能靠压 alpha 体现「次要」。</summary>
		private const float HintAlpha = 0.82f;

		/// <summary>「(?)提示」与下面人员行之间的空隙。</summary>
		private const float HintGap = 4f;

		/// <summary>殖民者栏「(?)提示」的翻译 key 与它弹出的说明段落。</summary>
		private const string HintKey = "DreamsOutposts.Ui.Hint";

		private static readonly string[] HintSectionKeys = { "DreamsOutposts.Ui.Help.Warehouse" };

		/// <summary>殖民者栏的名字后面要显示哪个技能等级；null 表示不显示（默认）。</summary>
		private SkillDef skillFilter;

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
			return PanelHeadHeight(index) + PanelBodyHeight(index, RowCount(cache, index));
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

		/// <summary>
		/// 表头高度：殖民者栏是「标题 + (?)提示」两行加右边那颗技能按钮，比其他栏高一截。
		/// </summary>
		private static float PanelHeadHeight(int index)
		{
			if (index == 0)
			{
				return LeftHeadHeight() + PanelHeadPaddingV * 2f;
			}
			return UiText.LineHeight(UiFont.Body) + PanelHeadPaddingV * 2f;
		}

		/// <summary>殖民者栏表头左半边的高度：标题行 + 空隙 + 「(?)提示」行。</summary>
		private static float LeftHeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + HintGap + UiDraw.HintHeight();
		}

		private static float RowHeight()
		{
			return Mathf.Max(IconSize, UiText.LineHeight(UiFont.Body)) + RowPaddingV * 2f;
		}

		private static float PanelBodyHeight(int index, int rowCount)
		{
			float needed = PanelBodyPadding * 2f + ((rowCount > 0) ? rowCount * (RowHeight() + RowGap) - RowGap : UiText.LineHeight(UiFont.Body) + 14f);
			float minimum = PanelMinHeight - PanelHeadHeight(index);
			return Mathf.Clamp(needed, minimum, PanelBodyMaxHeight);
		}

		private void DrawColumn(Rect rect, OutpostUiCache cache, int index)
		{
			UiDebug.Scope("warehouse.panel[" + index + "]", rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			float headHeight = PanelHeadHeight(index);
			Rect head = new Rect(rect.x, rect.y, rect.width, headHeight);
			if (index == 0)
			{
				DrawColonistHead(head, cache);
			}
			else
			{
				float titleWidth = Mathf.Max(rect.width - PanelHeadPaddingH * 2f - 60f, 30f);
				UiText.Draw(new Rect(head.x + PanelHeadPaddingH, head.y, titleWidth, head.height), ColumnTitle(index),
					UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
				string countText = "(" + RowCount(cache, index) + ")";
				float countWidth = UiText.Width(countText, UiFont.Body) + 4f;
				UiText.Draw(new Rect(head.xMax - PanelHeadPaddingH - countWidth, head.y, countWidth, head.height), countText,
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight);
			}
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
					DrawPawnRows(contentRect, cache.Colonists, skillFilter);
				}, scroll, id, scroll);
				break;
			case 1:
				UiWidgets.ScrollView(bodyInner, ref otherPawnsScroll, contentHeight, delegate(Rect contentRect)
				{
					DrawPawnRows(contentRect, cache.OtherPawns, null);
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
					DrawPawnRows(contentRect, cache.Vehicles, null);
				}, scroll, id, scroll);
				break;
			}
		}

		/// <summary>
		/// 殖民者栏的表头：左半边是「殖民者 (N)」标题和它下面的「(?)提示」，右半边是技能筛选按钮。
		/// 按钮宽度先算出来，标题行的可用宽度再让开它，窄栏下标题被截断也不会压到按钮上。
		/// </summary>
		private void DrawColonistHead(Rect head, OutpostUiCache cache)
		{
			float lineHeight = UiText.LineHeight(UiFont.Body);
			string buttonLabel = SkillFilterLabel();
			float buttonWidth = Mathf.Min(UiWidgets.ButtonWidth(buttonLabel, UiButtonSize.Small), Mathf.Max(head.width * 0.6f, 40f));
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Small);
			Rect buttonRect = new Rect(head.xMax - PanelHeadPaddingH - buttonWidth,
				head.y + (head.height - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
			float textX = head.x + PanelHeadPaddingH;
			float textWidth = Mathf.Max(buttonRect.x - 6f - textX, 30f);
			string title = ColumnTitle(0) + " (" + RowCount(cache, 0) + ")";
			UiText.Draw(new Rect(textX, head.y + PanelHeadPaddingV, textWidth, lineHeight), title,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			Rect hintRect = new Rect(textX, head.y + PanelHeadPaddingV + lineHeight + HintGap, textWidth, UiDraw.HintHeight());
			if (UiDraw.Hint(hintRect, HintKey.Translate(),
				UiPalette.WithAlpha(UiPalette.Ink3, HintAlpha), UiPalette.WithAlpha(UiPalette.Ink2, HintAlpha)))
			{
				UiOutpostHelpWindow.Open(HintSectionKeys);
			}
			if (UiWidgets.Button(buttonRect, buttonLabel, UiButtonKind.Secondary, true, null, UiButtonSize.Small,
				"DreamsOutposts.Warehouse.SkillFilterTip".Translate()))
			{
				OpenSkillFilterMenu();
			}
		}

		/// <summary>技能筛选按钮的文字：当前选中的技能，或「不显示」。</summary>
		private string SkillFilterLabel()
		{
			string value = (skillFilter != null) ? skillFilter.LabelCap.ToString() : "DreamsOutposts.Warehouse.SkillNone".Translate().ToString();
			return "DreamsOutposts.Warehouse.SkillFilter".Translate(value).ToString();
		}

		/// <summary>技能筛选菜单：第一项是「不显示」，其余是原版技能表，按名称排序（与酒馆的技能偏好菜单同一套做法）。</summary>
		private void OpenSkillFilterMenu()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>
			{
				new FloatMenuOption("DreamsOutposts.Warehouse.SkillNone".Translate(), delegate
				{
					skillFilter = null;
				})
			};
			foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefsListForReading.OrderBy(s => s.LabelCap.ToString()))
			{
				SkillDef captured = skill;
				options.Add(new FloatMenuOption(skill.LabelCap, delegate
				{
					skillFilter = captured;
				}));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		/// <summary>外部跳转进来时预设技能筛选（null = 不显示）；殖民者栏滚回顶部，免得还停在上一处。</summary>
		public void SetSkillFilter(SkillDef skill)
		{
			skillFilter = skill;
			colonistsScroll = Vector2.zero;
		}

		private static float RowContentHeight(int rowCount)
		{
			if (rowCount <= 0)
			{
				return UiText.LineHeight(UiFont.Body) + 14f;
			}
			return rowCount * (RowHeight() + RowGap) - RowGap;
		}

		/// <summary>人员行：头像 + 名字，整行可打开信息卡。skill 不为空时在名字后面补一个「(等级)」。</summary>
		private void DrawPawnRows(Rect rect, List<UiPawnView> rows, SkillDef skill)
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
				UiText.Draw(new Rect(x, row.y, nameWidth, row.height), PawnRowName(view, skill), UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
				if (view.Pawn != null && Widgets.ButtonInvisible(row))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(view.Pawn));
				}
			}
		}

		/// <summary>人员行的名字：既不筛选技能、或该 pawn 没有技能表（动物、载具等）时，就只用名字。</summary>
		private static string PawnRowName(UiPawnView view, SkillDef skill)
		{
			if (skill == null)
			{
				return view.Name;
			}
			SkillRecord record = view.Pawn?.skills?.GetSkill(skill);
			return (record != null) ? (view.Name + " (" + record.Level + ")") : view.Name;
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
