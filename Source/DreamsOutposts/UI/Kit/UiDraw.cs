using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>chip 的语义色（对应 CSS 的 .chip / .good / .bad / .warn / .info）。</summary>
	public enum UiChipKind
	{
		Neutral,
		Good,
		Bad,
		Warn,
		Info
	}

	/// <summary>一个 chip 的显示数据。</summary>
	public struct UiChipView
	{
		public string Label;

		public UiChipKind Kind;

		public string Tooltip;

		/// <summary>小号 chip（由流式布局统一设置）。</summary>
		public bool Small;

		public UiChipView(string label, UiChipKind kind = UiChipKind.Neutral, string tooltip = null)
		{
			Label = label;
			Kind = kind;
			Tooltip = tooltip;
			Small = false;
		}
	}

	/// <summary>
	/// 基础绘制：圆角盒、阴影、虚线框、图标、chip、进度条、分隔线、遮罩。
	/// 所有绘制都尊重当前 GUI.color（禁用态靠外层压 alpha 实现）。
	/// </summary>
	public static class UiDraw
	{
		/// <summary>实心矩形，保留外层 GUI.color 的乘性影响。</summary>
		public static void Solid(Rect rect, Color color)
		{
			if (rect.width <= 0f || rect.height <= 0f || color.a <= 0f)
			{
				return;
			}
			Color previous = GUI.color;
			GUI.color = new Color(color.r * previous.r, color.g * previous.g, color.b * previous.b, color.a * previous.a);
			GUI.DrawTexture(rect, BaseContent.WhiteTex);
			GUI.color = previous;
		}

		public static void Box(Rect rect, int radius, Color fill)
		{
			Box(rect, radius, fill, UiPalette.Clear);
		}

		public static void Box(Rect rect, int radius, Color fill, Color border)
		{
			Box(rect, radius, fill, border, UiCorners.All);
		}

		/// <summary>圆角盒，可指定哪些角是圆角（用于标题栏 / 侧栏这种只圆一半的面）。</summary>
		public static void Box(Rect rect, int radius, Color fill, Color border, UiCorners corners)
		{
			if (rect.width <= 0f || rect.height <= 0f || (fill.a <= 0f && border.a <= 0f))
			{
				return;
			}
			if (radius <= 0 || rect.width < 2f || rect.height < 2f)
			{
				Solid(rect, fill);
				if (border.a > 0f)
				{
					Color previous = GUI.color;
					GUI.color = new Color(border.r * previous.r, border.g * previous.g, border.b * previous.b, border.a * previous.a);
					Widgets.DrawBox(rect, 1);
					GUI.color = previous;
				}
				return;
			}
			UiTex.DrawNine(rect, UiTex.Box(radius, fill, border, (border.a > 0f) ? 1 : 0), 0.25f, radius, corners);
		}

		public static void Shadow(Rect rect, int radius, float alpha = 1f)
		{
			if (rect.width <= 0f || rect.height <= 0f || alpha <= 0f)
			{
				return;
			}
			float corner = UiTex.ShadowCornerSize(radius);
			Texture2D texture = UiTex.Shadow(radius);
			float uv = corner / (float)texture.width;
			Rect shadowRect = new Rect(rect.x - UiTex.ShadowBlur, rect.y - UiTex.ShadowBlur + UiTex.ShadowOffsetY,
				rect.width + UiTex.ShadowBlur * 2f, rect.height + UiTex.ShadowBlur * 2f);
			Color previous = GUI.color;
			GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
			UiTex.DrawNine(shadowRect, texture, uv, corner);
			GUI.color = previous;
		}

		/// <summary>面板：可选阴影 + 圆角底 + 1px 边框。</summary>
		public static void Panel(Rect rect, int radius, Color fill, Color border, bool shadow)
		{
			if (shadow)
			{
				Shadow(rect, radius);
			}
			Box(rect, radius, fill, border);
		}

		/// <summary>虚线圆角框（空槽位卡）。</summary>
		public static void DashedBox(Rect rect, int radius, Color border, float dash = 6f, float gap = 4f)
		{
			if (rect.width <= 0f || rect.height <= 0f || border.a <= 0f)
			{
				return;
			}
			int r = Mathf.Max(radius, 2);
			r = Mathf.Min(r, Mathf.FloorToInt(Mathf.Min(rect.width, rect.height) * 0.5f));
			GUI.DrawTexture(new Rect(rect.x, rect.y, r, r), UiTex.CornerArc(r, border, 0));
			GUI.DrawTexture(new Rect(rect.xMax - r, rect.y, r, r), UiTex.CornerArc(r, border, 1));
			GUI.DrawTexture(new Rect(rect.x, rect.yMax - r, r, r), UiTex.CornerArc(r, border, 2));
			GUI.DrawTexture(new Rect(rect.xMax - r, rect.yMax - r, r, r), UiTex.CornerArc(r, border, 3));
			DrawDashes(new Rect(rect.x + r, rect.y, Mathf.Max(rect.width - r * 2f, 0f), 1f), border, dash, gap);
			DrawDashes(new Rect(rect.x + r, rect.yMax - 1f, Mathf.Max(rect.width - r * 2f, 0f), 1f), border, dash, gap);
			DrawDashesVertical(new Rect(rect.x, rect.y + r, 1f, Mathf.Max(rect.height - r * 2f, 0f)), border, dash, gap);
			DrawDashesVertical(new Rect(rect.xMax - 1f, rect.y + r, 1f, Mathf.Max(rect.height - r * 2f, 0f)), border, dash, gap);
		}

		private static void DrawDashes(Rect line, Color color, float dash, float gap)
		{
			float step = Mathf.Max(dash + gap, 1f);
			// 让短划在边上居中分布
			int count = Mathf.FloorToInt((line.width + gap) / step);
			if (count <= 0)
			{
				Solid(line, color);
				return;
			}
			float total = count * dash + (count - 1) * gap;
			float x = line.x + (line.width - total) * 0.5f;
			for (int i = 0; i < count; i++)
			{
				Solid(new Rect(x, line.y, dash, line.height), color);
				x += step;
			}
		}

		private static void DrawDashesVertical(Rect line, Color color, float dash, float gap)
		{
			float step = Mathf.Max(dash + gap, 1f);
			int count = Mathf.FloorToInt((line.height + gap) / step);
			if (count <= 0)
			{
				Solid(line, color);
				return;
			}
			float total = count * dash + (count - 1) * gap;
			float y = line.y + (line.height - total) * 0.5f;
			for (int i = 0; i < count; i++)
			{
				Solid(new Rect(line.x, y, line.width, dash), color);
				y += step;
			}
		}

		/// <summary>
		/// 画图标。贴图来自 Textures/DreamsOutposts/Ui/*.png（白色 + alpha），
		/// 这里用 GUI.color 乘算着色，尺寸任意（贴图带 mipmap，缩小时平滑）。
		/// </summary>
		public static void Icon(Rect rect, UiIcon icon, Color color)
		{
			if (icon == UiIcon.None || rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}
			Texture2D texture = UiTex.IconTexture(icon);
			if (texture == null)
			{
				return;
			}
			Color previous = GUI.color;
			GUI.color = new Color(color.r * previous.r, color.g * previous.g, color.b * previous.b, color.a * previous.a);
			GUI.DrawTexture(rect, texture);
			GUI.color = previous;
		}

		public static void Divider(Rect rect, Color color)
		{
			Solid(rect, color);
		}

		public static void Scrim(Rect rect)
		{
			if (rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}
			Color previous = GUI.color;
			Color scrim = UiPalette.Scrim;
			GUI.color = new Color(scrim.r, scrim.g, scrim.b, scrim.a * previous.a);
			GUI.DrawTexture(rect, BaseContent.WhiteTex);
			GUI.color = previous;
		}

		public static void Bar(Rect rect, float fraction, Color fill, Color track)
		{
			int radius = Mathf.RoundToInt(Mathf.Min(UiMetrics.BarRadius, rect.height * 0.5f));
			Box(rect, radius, track);
			float clamped = Mathf.Clamp01(fraction);
			if (clamped <= 0f)
			{
				return;
			}
			float width = rect.width * clamped;
			if (width < 1f)
			{
				return;
			}
			Rect fillRect = new Rect(rect.x, rect.y, width, rect.height);
			if (width < rect.height)
			{
				// 极小进度画成圆角胶囊，避免出现方角
				Box(fillRect, Mathf.Max(Mathf.RoundToInt(width * 0.5f), 1), fill);
			}
			else
			{
				Box(fillRect, radius, fill);
			}
		}

		// ---------------- chip ----------------

		public static Color ChipTextColor(UiChipKind kind)
		{
			switch (kind)
			{
			case UiChipKind.Good:
				return UiPalette.Good;
			case UiChipKind.Bad:
				return UiPalette.Bad;
			case UiChipKind.Warn:
				return UiPalette.Warn;
			case UiChipKind.Info:
				return UiPalette.Info;
			default:
				return UiPalette.Ink2;
			}
		}

		public static Color ChipFillColor(UiChipKind kind)
		{
			switch (kind)
			{
			case UiChipKind.Good:
				return UiPalette.GoodBg;
			case UiChipKind.Bad:
				return UiPalette.BadBg;
			case UiChipKind.Warn:
				return UiPalette.WarnBg;
			case UiChipKind.Info:
				return UiPalette.InfoBg;
			default:
				return UiPalette.Raised;
			}
		}

		public static Color ChipLineColor(UiChipKind kind)
		{
			switch (kind)
			{
			case UiChipKind.Good:
				return UiPalette.GoodLine;
			case UiChipKind.Bad:
				return UiPalette.BadLine;
			case UiChipKind.Warn:
				return UiPalette.WarnLine;
			case UiChipKind.Info:
				return UiPalette.InfoLine;
			default:
				return UiPalette.Line;
			}
		}

		/// <summary>good/bad 用勾叉图标（对应 CSS 的 ::before）。</summary>
		public static bool ChipHasGlyph(UiChipKind kind)
		{
			return kind == UiChipKind.Good || kind == UiChipKind.Bad;
		}

		public static float ChipHeight(bool small)
		{
			float padding = small ? UiMetrics.ChipSmallPaddingV : UiMetrics.ChipPaddingV;
			return UiText.LineHeight(UiFont.Caption) + padding * 2f;
		}

		public static float ChipWidth(UiChipView chip)
		{
			float padding = chip.Small ? UiMetrics.ChipSmallPaddingH : UiMetrics.ChipPaddingH;
			float width = padding * 2f + UiText.Width(chip.Label, UiFont.Caption);
			if (ChipHasGlyph(chip.Kind))
			{
				width += UiMetrics.ChipGlyphSize + UiMetrics.ChipGap;
			}
			return width;
		}

		public static void Chip(Rect rect, UiChipView chip)
		{
			Box(rect, (int)UiMetrics.RadiusXs, ChipFillColor(chip.Kind), ChipLineColor(chip.Kind));
			float padding = chip.Small ? UiMetrics.ChipSmallPaddingH : UiMetrics.ChipPaddingH;
			float x = rect.x + padding;
			if (ChipHasGlyph(chip.Kind))
			{
				Rect glyphRect = new Rect(x, rect.y + (rect.height - UiMetrics.ChipGlyphSize) * 0.5f, UiMetrics.ChipGlyphSize, UiMetrics.ChipGlyphSize);
				Icon(glyphRect, (chip.Kind == UiChipKind.Good) ? UiIcon.Check : UiIcon.Cross, ChipTextColor(chip.Kind));
				x += UiMetrics.ChipGlyphSize + UiMetrics.ChipGap;
			}
			Rect textRect = new Rect(x, rect.y, Mathf.Max(rect.xMax - padding - x, 0f), rect.height);
			UiText.Draw(textRect, chip.Label, UiFont.Caption, ChipTextColor(chip.Kind), TextAnchor.MiddleLeft);
			if (!string.IsNullOrEmpty(chip.Tooltip))
			{
				TooltipHandler.TipRegion(rect, new TipSignal(chip.Tooltip, rect.GetHashCode()));
			}
		}

		/// <summary>chip 流式布局（自动换行）的总高度。</summary>
		public static float ChipsHeight(List<UiChipView> chips, float width, bool small)
		{
			return ChipsInternal(new Rect(0f, 0f, width, 0f), chips, small, false);
		}

		/// <summary>画 chip 流式布局，返回占用高度。</summary>
		public static float Chips(Rect rect, List<UiChipView> chips, bool small)
		{
			return ChipsInternal(rect, chips, small, true);
		}

		private static float ChipsInternal(Rect rect, List<UiChipView> chips, bool small, bool draw)
		{
			int count = (chips == null) ? 0 : chips.Count;
			if (count == 0 || rect.width <= 4f)
			{
				return 0f;
			}
			float x = 0f;
			float y = 0f;
			float rowHeight = 0f;
			float gap = UiMetrics.ChipFlowGap;
			for (int i = 0; i < count; i++)
			{
				UiChipView chip = chips[i];
				chip.Small = small;
				float width = ChipWidth(chip);
				float height = ChipHeight(small);
				if (x > 0f && x + width > rect.width)
				{
					x = 0f;
					y += rowHeight + gap;
					rowHeight = 0f;
				}
				if (draw)
				{
					Chip(new Rect(rect.x + x, rect.y + y, width, height), chip);
				}
				rowHeight = Mathf.Max(rowHeight, height);
				x += width + gap;
			}
			return y + rowHeight;
		}

		/// <summary>原版物品图标（材料、产物）。</summary>
		public static void ThingIcon(Rect rect, ThingDef def)
		{
			if (def == null)
			{
				return;
			}
			Color previous = GUI.color;
			GUI.color = Color.white;
			Widgets.ThingIcon(rect, def);
			GUI.color = previous;
		}

		/// <summary>
		/// 统一的物品信息入口：图标与名称共用一个点击/悬停区域。
		/// 悬停颜色默认使用品牌绿，调用方可覆盖。
		/// </summary>
		public static void ThingInfoLink(Rect hitRect, Rect iconRect, Rect labelRect, ThingDef def, string label,
			UiFont font = UiFont.Body, Color? normalColor = null, Color? hoverColor = null, bool bold = false)
		{
			bool hovered = def != null && Mouse.IsOver(hitRect);
			ThingIcon(iconRect, def);
			UiText.Draw(labelRect, label, font, hovered ? (hoverColor ?? UiPalette.BrandText) : (normalColor ?? UiPalette.Ink),
				TextAnchor.MiddleLeft, bold, false, true);
			if (def != null && Widgets.ButtonInvisible(hitRect))
			{
				Find.WindowStack.Add(new Dialog_InfoCard(def));
			}
		}
	}
}
