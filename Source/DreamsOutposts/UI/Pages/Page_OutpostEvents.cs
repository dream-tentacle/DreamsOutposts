using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 「事件」页（新样式）：带剩余时间条的卡片列表 + 已排期后续事件提示。
	/// 按需求去掉：页头描述与「点开卡片可以查看并选择选项……」提示。
	/// 点开卡片打开事件弹窗（选项面板）。
	/// </summary>
	public class Page_OutpostEvents : OutpostManagePage, IUiShellPage
	{
		private const float CardPaddingH = 15f;

		private const float CardPaddingV = 14f;

		private const float CardGap = 13f;

		private const float CardHeadGap = 10f;

		private const float CardInnerGap = 7f;

		private const float ChipGap = 5f;

		private const float TimeRowGap = 10f;

		private const float TimeBarHeight = 8f;

		private const float NotePaddingH = 13f;

		private const float NotePaddingV = 10f;

		private const float NoteMarginTop = 14f;

		private const float NoteIconSize = 16f;

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

		public override string Label => "DreamsOutposts.Events".Translate();

		public string NavSummary => "DreamsOutposts.Ui.Nav.Events".Translate(Cache.Events.Count).ToString();

		public string HeadDescription => null;

		public string HeadHint => null;

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
			if (rect.width < 80f)
			{
				return 1f;
			}
			OutpostUiCache cache = Cache;
			float y = rect.y;
			if (cache.Events.Count == 0)
			{
				float emptyHeight = UiText.LineHeight(UiFont.Body) + 28f;
				if (draw)
				{
					UiText.Draw(new Rect(rect.x, y, rect.width, emptyHeight), "DreamsOutposts.NoCurrentEvents".Translate(),
						UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				}
				y += emptyHeight;
			}
			else
			{
				for (int i = 0; i < cache.Events.Count; i++)
				{
					UiEventView view = cache.Events[i];
					float cardHeight = CardHeight(view, rect.width);
					if (draw)
					{
						DrawCard(new Rect(rect.x, y, rect.width, cardHeight), view);
					}
					y += cardHeight + CardGap;
				}
				y -= CardGap;
			}
			if (cache.ScheduledCount > 0)
			{
				float noteHeight = NoteHeight(rect.width);
				if (draw)
				{
					DrawScheduledNote(new Rect(rect.x, y + NoteMarginTop, rect.width, noteHeight), cache);
				}
				y += NoteMarginTop + noteHeight;
			}
			return Mathf.Max(y - rect.y, 1f);
		}

		// ---------------------------------------------------------------
		// 事件卡
		// ---------------------------------------------------------------

		private static float CardHeight(UiEventView view, float width)
		{
			float innerWidth = Mathf.Max(width - CardPaddingH * 2f, 40f);
			float headHeight = Mathf.Max(UiText.LineHeight(UiFont.Body), UiDraw.ChipHeight(true));
			float descriptionHeight = 0f;
			if (!string.IsNullOrEmpty(view.Description))
			{
				bool truncated;
				string description = UiText.ClampLines(view.Description, UiFont.Body, innerWidth, 2, false, out truncated);
				descriptionHeight = UiText.Height(description, UiFont.Body, innerWidth);
			}
			float timeHeight = Mathf.Max(TimeBarHeight, UiText.LineHeight(UiFont.Caption));
			return CardPaddingV * 2f + headHeight + (descriptionHeight > 0f ? CardInnerGap + descriptionHeight : 0f) + CardInnerGap + timeHeight;
		}

		private void DrawCard(Rect rect, UiEventView view)
		{
			UiDebug.Scope("event.card", rect);
			bool hovered = Mouse.IsOver(rect);
			if (hovered)
			{
				UiDraw.Shadow(rect, (int)UiMetrics.RadiusSm, 0.7f);
			}
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, hovered ? UiPalette.LineStrong : UiPalette.Line);
			float innerX = rect.x + CardPaddingH;
			float innerWidth = Mathf.Max(rect.width - CardPaddingH * 2f, 40f);
			float y = rect.y + CardPaddingV;
			// 头部：标题 + 分类 chip + 剩余 chip
			float headHeight = Mathf.Max(UiText.LineHeight(UiFont.Body), UiDraw.ChipHeight(true));
			float chipY = y + (headHeight - UiDraw.ChipHeight(true)) * 0.5f;
			float rightX = innerX + innerWidth;
			UiChipView remainingChip = new UiChipView(view.RemainingChipText, UiChipKind.Warn);
			remainingChip.Small = true;
			float remainingWidth = UiDraw.ChipWidth(remainingChip);
			UiDraw.Chip(new Rect(rightX - remainingWidth, chipY, remainingWidth, UiDraw.ChipHeight(true)), remainingChip);
			rightX -= remainingWidth + ChipGap;
			if (!string.IsNullOrEmpty(view.CategoryLabel))
			{
				UiChipView categoryChip = new UiChipView(view.CategoryLabel);
				categoryChip.Small = true;
				float categoryWidth = UiDraw.ChipWidth(categoryChip);
				UiDraw.Chip(new Rect(rightX - categoryWidth, chipY, categoryWidth, UiDraw.ChipHeight(true)), categoryChip);
				rightX -= categoryWidth + ChipGap;
			}
			float titleWidth = Mathf.Max(rightX - innerX, 40f);
			UiText.Draw(new Rect(innerX, y, titleWidth, headHeight), view.Label, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			y += headHeight;
			if (!string.IsNullOrEmpty(view.Description))
			{
				y += CardInnerGap;
				bool truncated;
				string description = UiText.ClampLines(view.Description, UiFont.Body, innerWidth, 2, false, out truncated);
				float height = UiText.Height(description, UiFont.Body, innerWidth);
				UiText.Draw(new Rect(innerX, y, innerWidth, height), description, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
				y += height;
			}
			y += CardInnerGap;
			// 剩余时间：递减的进度条 + 文字
			float timeHeight = Mathf.Max(TimeBarHeight, UiText.LineHeight(UiFont.Caption));
			string timeText = "DreamsOutposts.Remaining".Translate(view.RemainingText);
			float timeWidth = Mathf.Min(UiText.Width(timeText, UiFont.Caption) + 6f, innerWidth * 0.5f);
			float barWidth = Mathf.Max(innerWidth - timeWidth - TimeRowGap, 40f);
			UiDraw.Bar(new Rect(innerX, y + (timeHeight - TimeBarHeight) * 0.5f, barWidth, TimeBarHeight),
				view.HasProgress ? view.Progress : 0f, UiPalette.Accent, UiPalette.Track);
			UiText.Draw(new Rect(innerX + barWidth + TimeRowGap, y, timeWidth, timeHeight), timeText,
				UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
			if (Widgets.ButtonInvisible(rect))
			{
				Window_OutpostManage shell = Shell;
				if (shell != null)
				{
					shell.OpenEventModal(view);
				}
			}
		}

		// ---------------------------------------------------------------
		// 已排期的后续事件
		// ---------------------------------------------------------------

		private static float NoteHeight(float width)
		{
			return NotePaddingV * 2f + UiText.LineHeight(UiFont.Body);
		}

		private void DrawScheduledNote(Rect rect, OutpostUiCache cache)
		{
			UiDraw.DashedBox(rect, (int)UiMetrics.RadiusSm, UiPalette.Line);
			float x = rect.x + NotePaddingH;
			float iconY = rect.y + (rect.height - NoteIconSize) * 0.5f;
			UiDraw.Icon(new Rect(x, iconY, NoteIconSize, NoteIconSize), UiIcon.Bell, UiPalette.BrandText);
			x += NoteIconSize + 9f;
			string text = "DreamsOutposts.Ui.ScheduledNote".Translate(cache.ScheduledCount);
			UiText.Draw(new Rect(x, rect.y, Mathf.Max(rect.xMax - NotePaddingH - x, 30f), rect.height), text,
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
		}
	}
}
