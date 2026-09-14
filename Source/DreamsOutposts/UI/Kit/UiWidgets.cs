using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public enum UiButtonKind
	{
		Secondary,
		Primary,
		Danger,
		Ghost
	}

	public enum UiButtonSize
	{
		Normal,
		Small
	}

	/// <summary>
	/// 交互控件：按钮、图标按钮、导航项、复选框、细滚动条。
	/// hover / 按下 / 禁用 三种状态都在这里统一处理。
	/// </summary>
	public static class UiWidgets
	{
		private sealed class ScrollDragState
		{
			public bool Dragging;

			public float GrabOffset;
		}

		private static readonly Dictionary<int, ScrollDragState> dragStates = new Dictionary<int, ScrollDragState>();

		// ---------------- 按钮 ----------------

		public static bool Button(Rect rect, string label, UiButtonKind kind = UiButtonKind.Secondary,
			bool enabled = true, string disabledReason = null, UiButtonSize size = UiButtonSize.Normal, string tooltip = null)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = enabled && Mouse.IsOver(rect);
			bool pressed = hovered && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag);
			Color fill;
			Color border;
			Color textColor;
			switch (kind)
			{
			case UiButtonKind.Primary:
				fill = hovered ? UiPalette.BrandHover : UiPalette.Brand;
				border = fill;
				textColor = UiPalette.OnAccent;
				break;
			case UiButtonKind.Danger:
				fill = hovered ? UiPalette.DangerHover : UiPalette.Danger;
				border = fill;
				textColor = UiPalette.OnAccent;
				break;
			case UiButtonKind.Ghost:
				fill = hovered ? UiPalette.Hover : UiPalette.Clear;
				border = UiPalette.Clear;
				textColor = UiPalette.Ink;
				break;
			default:
				fill = hovered ? UiPalette.Hover : UiPalette.Raised;
				border = UiPalette.Line;
				textColor = UiPalette.Ink;
				break;
			}
			Rect drawRect = rect;
			if (pressed)
			{
				// 等价 CSS transform: scale(.95)
				float shrinkX = Mathf.Max(rect.width * 0.025f, 1f);
				float shrinkY = Mathf.Max(rect.height * 0.05f, 1f);
				drawRect = new Rect(rect.x + shrinkX, rect.y + shrinkY, Mathf.Max(rect.width - shrinkX * 2f, 1f), Mathf.Max(rect.height - shrinkY * 2f, 1f));
			}
			Color previous = GUI.color;
			if (!enabled)
			{
				GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * UiPalette.DisabledAlpha);
			}
			UiDraw.Box(drawRect, (int)UiMetrics.RadiusSm, fill, border);
			UiText.Draw(drawRect, label, (size == UiButtonSize.Small) ? UiFont.Caption : UiFont.Body, textColor, TextAnchor.MiddleCenter, false, false, true);
			GUI.color = previous;
			string tip = (!enabled && !string.IsNullOrEmpty(disabledReason)) ? disabledReason : tooltip;
			Tip(rect, tip);
			if (!enabled)
			{
				return false;
			}
			return Widgets.ButtonInvisible(rect);
		}

		public static float ButtonWidth(string label, UiButtonSize size = UiButtonSize.Normal)
		{
			float padding = (size == UiButtonSize.Small) ? UiMetrics.ButtonSmallPaddingH : UiMetrics.ButtonPaddingH;
			return UiText.Width(label, (size == UiButtonSize.Small) ? UiFont.Caption : UiFont.Body) + padding * 2f;
		}

		public static float ButtonHeight(UiButtonSize size = UiButtonSize.Normal)
		{
			float padding = (size == UiButtonSize.Small) ? UiMetrics.ButtonSmallPaddingV : UiMetrics.ButtonPaddingV;
			return UiText.LineHeight((size == UiButtonSize.Small) ? UiFont.Caption : UiFont.Body) + padding * 2f;
		}

		/// <summary>使用白色透明底图染色的主按钮；文字位置使用底图原始像素坐标。</summary>
		public static bool TexturedPrimaryButton(Rect rect, string label, Texture2D texture,
			float sourceLabelX, float sourceLabelWidth,
			UiButtonSize size = UiButtonSize.Normal, string tooltip = null)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = Mouse.IsOver(rect);
			bool pressed = hovered && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag);
			Rect drawRect = rect;
			if (pressed)
			{
				float shrinkX = Mathf.Max(rect.width * 0.025f, 1f);
				float shrinkY = Mathf.Max(rect.height * 0.05f, 1f);
				drawRect = new Rect(rect.x + shrinkX, rect.y + shrinkY,
					Mathf.Max(rect.width - shrinkX * 2f, 1f), Mathf.Max(rect.height - shrinkY * 2f, 1f));
			}
			if (texture != null)
			{
				Color previous = GUI.color;
				GUI.color = hovered ? UiPalette.BrandHover : UiPalette.Brand;
				GUI.DrawTexture(drawRect, texture, ScaleMode.StretchToFill, true);
				GUI.color = previous;
			}
			float sourceWidth = (texture != null) ? texture.width : Mathf.Max(sourceLabelX + sourceLabelWidth, 1f);
			Rect drawnLabelRect = new Rect(drawRect.x + drawRect.width * sourceLabelX / sourceWidth,
				drawRect.y, drawRect.width * sourceLabelWidth / sourceWidth, drawRect.height);
			UiText.Draw(drawnLabelRect, label, (size == UiButtonSize.Small) ? UiFont.Caption : UiFont.Body,
				UiPalette.OnAccent, TextAnchor.MiddleCenter, false, false, true);
			Tip(rect, tooltip);
			return Widgets.ButtonInvisible(rect);
		}

		public static bool IconButton(Rect rect, UiIcon icon, string tooltip = null, bool enabled = true)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = enabled && Mouse.IsOver(rect);
			Color previous = GUI.color;
			if (!enabled)
			{
				GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * UiPalette.DisabledAlpha);
			}
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, hovered ? UiPalette.Hover : UiPalette.Raised, UiPalette.Line);
			float glyph = Mathf.Round(rect.height * 0.54f);
			UiDraw.Icon(new Rect(rect.center.x - glyph * 0.5f, rect.center.y - glyph * 0.5f, glyph, glyph), icon,
				hovered ? UiPalette.Ink : UiPalette.Ink2);
			GUI.color = previous;
			Tip(rect, tooltip);
			return enabled && Widgets.ButtonInvisible(rect);
		}

		public static bool CloseButton(Rect rect, string tooltip = null)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = Mouse.IsOver(rect);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, hovered ? UiPalette.Hover : UiPalette.Raised, UiPalette.Line);
			float glyph = Mathf.Round(rect.height * 0.5f);
			UiDraw.Icon(new Rect(rect.center.x - glyph * 0.5f, rect.center.y - glyph * 0.5f, glyph, glyph), UiIcon.Close,
				hovered ? UiPalette.Ink : UiPalette.Ink2);
			Tip(rect, tooltip);
			return Widgets.ButtonInvisible(rect);
		}

		/// <summary>横向分页使用的分段标签。视觉语义与管理页侧栏选中项保持一致。</summary>
		public static bool SegmentTab(Rect rect, string label, bool active)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = Mouse.IsOver(rect);
			Color fill = active ? UiPalette.BrandTint : (hovered ? UiPalette.Hover : UiPalette.Clear);
			Color border = active ? UiPalette.BrandLine : (hovered ? UiPalette.Line : UiPalette.Clear);
			Color ink = active ? UiPalette.BrandText : (hovered ? UiPalette.Ink : UiPalette.Ink2);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, fill, border);
			UiText.Draw(rect, label, UiFont.Body, ink, TextAnchor.MiddleCenter, active, false, true);
			return Widgets.ButtonInvisible(rect);
		}

		// ---------------- 侧栏导航项 ----------------

		public static float NavItemHeight()
		{
			return UiText.LineHeight(UiFont.Body) + UiText.LineHeight(UiFont.Caption) + UiMetrics.NavItemPaddingV * 2f;
		}

		public static bool NavItem(Rect rect, UiIcon icon, string label, string sub, bool active, int badge = 0, string tooltip = null)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = Mouse.IsOver(rect);
			Color fill;
			Color border;
			Color inkColor;
			if (active)
			{
				fill = UiPalette.BrandTint;
				border = UiPalette.BrandLine;
				inkColor = UiPalette.BrandText;
			}
			else if (hovered)
			{
				fill = UiPalette.Raised;
				border = UiPalette.Line;
				inkColor = UiPalette.Ink;
			}
			else
			{
				fill = UiPalette.Clear;
				border = UiPalette.Clear;
				inkColor = UiPalette.Ink2;
			}
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, fill, border);
			if (active)
			{
				UiDraw.CornerAccents(rect, (int)UiMetrics.NavActiveArcRadius, UiMetrics.NavActiveArcGap);
			}
			float x = rect.x + UiMetrics.NavItemPaddingH;
			float contentTop = rect.y + UiMetrics.NavItemPaddingV;
			Rect iconRect = new Rect(x, contentTop + (UiText.LineHeight(UiFont.Body) - UiMetrics.NavIconSize) * 0.5f, UiMetrics.NavIconSize, UiMetrics.NavIconSize);
			UiDraw.Icon(iconRect, icon, active ? UiPalette.BrandText : inkColor);
			x += UiMetrics.NavIconSize + UiMetrics.NavItemGap;
			float badgeWidth = 0f;
			if (badge > 0)
			{
				badgeWidth = Mathf.Max(UiMetrics.NavBadgeMinWidth, UiText.Width(badge.ToString(), UiFont.Caption, true) + UiMetrics.NavBadgePaddingH * 2f);
			}
			float textWidth = Mathf.Max(rect.xMax - UiMetrics.NavItemPaddingH - x - ((badgeWidth > 0f) ? badgeWidth + 8f : 0f), 10f);
			UiText.Draw(new Rect(x, contentTop, textWidth, UiText.LineHeight(UiFont.Body)), label, UiFont.Body, inkColor, TextAnchor.MiddleLeft, active, false, true);
			if (!string.IsNullOrEmpty(sub))
			{
				UiText.Draw(new Rect(x, contentTop + UiText.LineHeight(UiFont.Body), textWidth, UiText.LineHeight(UiFont.Caption)), sub,
					UiFont.Caption, active ? UiPalette.BrandText : UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			}
			if (badge > 0)
			{
				float height = UiText.LineHeight(UiFont.Caption) + UiMetrics.ChipSmallPaddingV * 2f;
				Rect badgeRect = new Rect(rect.xMax - UiMetrics.NavItemPaddingH - badgeWidth, rect.y + (rect.height - height) * 0.5f, badgeWidth, height);
				UiDraw.Box(badgeRect, (int)UiMetrics.RadiusXs, UiPalette.BadBg, UiPalette.BadLine);
				UiText.Draw(badgeRect, badge.ToString(), UiFont.Caption, UiPalette.Bad, TextAnchor.MiddleCenter, true);
			}
			Tip(rect, tooltip);
			return Widgets.ButtonInvisible(rect);
		}

		// ---------------- 复选框 ----------------

		public static float CheckboxSize => 16f;

		public static bool Checkbox(Rect rect, ref bool value, string label, string tooltip = null)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return false;
			}
			bool hovered = Mouse.IsOver(rect);
			float boxSize = CheckboxSize;
			Rect boxRect = new Rect(rect.x, rect.y + (rect.height - boxSize) * 0.5f, boxSize, boxSize);
			if (value)
			{
				UiDraw.Box(boxRect, (int)UiMetrics.RadiusSm2, UiPalette.Brand, UiPalette.Brand);
				float glyph = Mathf.Round(boxSize * 0.7f);
				UiDraw.Icon(new Rect(boxRect.center.x - glyph * 0.5f, boxRect.center.y - glyph * 0.5f, glyph, glyph), UiIcon.Check, UiPalette.OnAccent);
			}
			else
			{
				UiDraw.Box(boxRect, (int)UiMetrics.RadiusSm2, hovered ? UiPalette.Hover : UiPalette.Raised, UiPalette.LineStrong);
			}
			float textX = boxRect.xMax + 8f;
			if (!string.IsNullOrEmpty(label))
			{
				UiText.Draw(new Rect(textX, rect.y, Mathf.Max(rect.xMax - textX, 10f), rect.height), label, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, false, false, true);
			}
			Tip(rect, tooltip);
			if (Widgets.ButtonInvisible(rect))
			{
				value = !value;
				return true;
			}
			return false;
		}

		// ---------------- 滚动视图 ----------------

		public static bool ScrollView(Rect outRect, ref Vector2 scroll, float contentHeight, Action<Rect> drawContent,
			bool reserveBar = true, int id = 0, bool drawBar = true)
		{
			if (outRect.width <= 0f || outRect.height <= 0f)
			{
				return false;
			}
			float gutter = reserveBar ? UiMetrics.ScrollbarGutter : 0f;
			Rect viewport = new Rect(outRect.x, outRect.y, Mathf.Max(outRect.width - gutter, 1f), outRect.height);
			float viewHeight = Mathf.Max(contentHeight, viewport.height);
			float max = Mathf.Max(viewHeight - viewport.height, 0f);
			scroll.y = Mathf.Clamp(scroll.y, 0f, max);
			bool changed = false;
			Rect track;
			Rect thumb;
			ComputeScrollbar(outRect, viewport.height, viewHeight, max, scroll.y, drawBar, out track, out thumb);
			// 先画内容：内层滚动视图有机会先消费滚轮事件
			GUI.BeginGroup(viewport);
			if (drawContent != null)
			{
				// 让 UiDebug 知道这段内容画在哪个组里（组内坐标 → 屏幕坐标只是一次平移）
				UiDebug.PushSpace("scroll", new Vector2(viewport.x, viewport.y));
				drawContent(new Rect(0f, -scroll.y, viewport.width, viewHeight));
				UiDebug.PopSpace();
			}
			GUI.EndGroup();
			if (max > 0.01f)
			{
				ScrollDragState state = GetDragState(id);
				if (Event.current.type == EventType.ScrollWheel && Mouse.IsOver(outRect))
				{
					scroll.y = Mathf.Clamp(scroll.y + Event.current.delta.y * UiMetrics.ScrollWheelSpeed, 0f, max);
					changed = true;
					Event.current.Use();
				}
				else if (Event.current.type == EventType.MouseDown && Mouse.IsOver(thumb))
				{
					state.Dragging = true;
					state.GrabOffset = Event.current.mousePosition.y - thumb.y;
					Event.current.Use();
				}
				else if (state.Dragging && Event.current.type == EventType.MouseDrag)
				{
					float travel = Mathf.Max(track.height - thumb.height, 1f);
					scroll.y = Mathf.Clamp((Event.current.mousePosition.y - state.GrabOffset - track.y) / travel * max, 0f, max);
					changed = true;
					Event.current.Use();
				}
				else if (state.Dragging && Event.current.type == EventType.MouseUp)
				{
					state.Dragging = false;
				}
				else if (Event.current.type == EventType.MouseDown && Mouse.IsOver(track) && !Mouse.IsOver(thumb))
				{
					scroll.y = Mathf.Clamp(scroll.y + ((Event.current.mousePosition.y < thumb.y) ? -viewport.height : viewport.height), 0f, max);
					changed = true;
					Event.current.Use();
				}
			}
			if (drawBar && max > 0.01f)
			{
				ComputeScrollbar(outRect, viewport.height, viewHeight, max, scroll.y, drawBar, out track, out thumb);
				bool overThumb = Mouse.IsOver(thumb);
				UiDraw.Box(thumb, (int)(UiMetrics.ScrollbarThumbWidth * 0.5f), overThumb ? UiPalette.ScrollThumbHover : UiPalette.ScrollThumb);
			}
			return changed;
		}

		private static void ComputeScrollbar(Rect outRect, float viewportHeight, float viewHeight, float max, float scrollY, bool enabled, out Rect track, out Rect thumb)
		{
			track = new Rect(outRect.xMax - UiMetrics.ScrollbarWidth, outRect.y, UiMetrics.ScrollbarWidth, outRect.height);
			if (!enabled || max <= 0.01f)
			{
				thumb = new Rect(track.x, track.y, 0f, 0f);
				return;
			}
			float thumbHeight = Mathf.Clamp(track.height * (viewportHeight / Mathf.Max(viewHeight, 1f)), UiMetrics.ScrollbarMinThumb, track.height);
			float t = Mathf.Clamp01(scrollY / max);
			float thumbY = track.y + t * (track.height - thumbHeight);
			thumb = new Rect(track.x + (UiMetrics.ScrollbarWidth - UiMetrics.ScrollbarThumbWidth) * 0.5f, thumbY, UiMetrics.ScrollbarThumbWidth, thumbHeight);
		}

		private static ScrollDragState GetDragState(int id)
		{
			ScrollDragState state;
			if (!dragStates.TryGetValue(id, out state))
			{
				state = new ScrollDragState();
				dragStates[id] = state;
			}
			return state;
		}

		// ---------------- 其它 ----------------

		/// <summary>tooltip。动态文本请传稳定 id，否则延迟计时会被每帧重置。</summary>
		public static void Tip(Rect rect, string text, int stableId = 0)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			TooltipHandler.TipRegion(rect, new TipSignal(text, (stableId != 0) ? stableId : GenText.StableStringHash(text)));
		}

		/// <summary>tooltip（惰性文本：只在真正要显示时才拼字符串）。</summary>
		public static void Tip(Rect rect, Func<string> textGetter, int stableId)
		{
			if (textGetter == null)
			{
				return;
			}
			TooltipHandler.TipRegion(rect, new TipSignal(textGetter, stableId));
		}

		/// <summary>禁用态容器：内部所有绘制都会乘上 45% 透明度。</summary>
		public static IDisposable Disabled(float alpha = UiPalette.DisabledAlpha)
		{
			return new AlphaScope(alpha);
		}

		private sealed class AlphaScope : IDisposable
		{
			private readonly Color previous;

			public AlphaScope(float alpha)
			{
				previous = GUI.color;
				GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
			}

			public void Dispose()
			{
				GUI.color = previous;
			}
		}
	}
}
