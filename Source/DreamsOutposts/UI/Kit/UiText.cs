using System.Text;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 新 UI 的字号档位。原版只有 3 档（Tiny/Small/Medium），这里把它们语义化，
	/// 页面只写档位名，不直接碰 GameFont。
	/// </summary>
	public enum UiFont
	{
		/// <summary>小字：chip、副标题、hint、元信息（设计稿 11~12px）。</summary>
		Caption,
		/// <summary>正文：描述、列表、行（设计稿 13~14px）。</summary>
		Body,
		/// <summary>标题：页头、窗口标题、区块标题（设计稿 16.5~20px）。</summary>
		Heading,
		/// <summary>数字强调：预期产量、造价（设计稿 15~18px，加粗）。</summary>
		Number
	}

	/// <summary>
	/// 文字绘制与测量。所有绘制都走这里，保证字号/颜色/换行状态一致，
	/// 并且不会污染原版共享的 GUIStyle。
	/// </summary>
	public static class UiText
	{
		private const int FontCount = 4;

		private static readonly GUIStyle[] plainStyles = new GUIStyle[FontCount];
		private static readonly GUIStyle[] boldStyles = new GUIStyle[FontCount];
		private static bool initialized;

		private static void EnsureInit()
		{
			if (initialized)
			{
				return;
			}
			initialized = true;
			for (int i = 0; i < FontCount; i++)
			{
				plainStyles[i] = Build((UiFont)i, false);
				boldStyles[i] = Build((UiFont)i, true);
			}
		}

		private static GUIStyle Build(UiFont font, bool isBold)
		{
			GameFont gameFont = GameFontOf(font);
			GUIStyle source = Text.fontStyles[(int)gameFont];
			GUIStyle style = new GUIStyle(source);
			style.richText = true;
			style.wordWrap = false;
			style.alignment = TextAnchor.UpperLeft;
			// 与原版一致：不裁剪。用 Clip 会把超出西文 ascent 线的中文字形顶部切掉
			// （原版 Small 字号自带 contentOffset (0,-1)，正是最容易被切的那种情况）。
			// 横向溢出靠调用方的省略号/换行处理，纵向靠布局给足行高。
			style.clipping = TextClipping.Overflow;
			style.normal.textColor = Color.white;
			// 只有动态字体才能合成粗体；静态字体保持原样（层级仍由字号与颜色承担）。
			if (isBold && source.font != null && source.font.dynamic)
			{
				style.fontStyle = FontStyle.Bold;
			}
			return style;
		}

		public static GameFont GameFontOf(UiFont font)
		{
			switch (font)
			{
			case UiFont.Caption:
				// 中文等语言下 Tiny 不可用时原版会回落到 Small，这里跟随同一规则。
				return Text.TinyFontSupported ? GameFont.Tiny : GameFont.Small;
			case UiFont.Body:
				return GameFont.Small;
			default:
				return GameFont.Medium;
			}
		}

		public static GUIStyle Style(UiFont font, bool isBold = false)
		{
			EnsureInit();
			int index = (int)font;
			if (index < 0 || index >= FontCount)
			{
				index = (int)UiFont.Body;
			}
			return isBold ? boldStyles[index] : plainStyles[index];
		}

		public static float LineHeight(UiFont font)
		{
			return Text.LineHeightOf(GameFontOf(font));
		}

		public static void Draw(Rect rect, string text, UiFont font, Color color,
			TextAnchor anchor = TextAnchor.UpperLeft, bool isBold = false, bool wrap = false, bool ellipsis = false)
		{
			if (string.IsNullOrEmpty(text) || rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}
			GUIStyle style = Style(font, isBold);
			TextAnchor previousAnchor = style.alignment;
			bool previousWrap = style.wordWrap;
			style.alignment = anchor;
			style.wordWrap = wrap;
			Color previousColor = GUI.color;
			// 乘性叠加：外层若是禁用态（压了 alpha），文字也会跟着变淡。
			GUI.color = new Color(color.r * previousColor.r, color.g * previousColor.g, color.b * previousColor.b, color.a * previousColor.a);
			GUI.Label(rect, (!wrap && ellipsis) ? Clamp(text, font, rect.width, isBold) : text, style);
			GUI.color = previousColor;
			style.alignment = previousAnchor;
			style.wordWrap = previousWrap;
		}

		public static Vector2 Size(string text, UiFont font, bool isBold = false)
		{
			if (string.IsNullOrEmpty(text))
			{
				return Vector2.zero;
			}
			return Style(font, isBold).CalcSize(new GUIContent(text.StripTags()));
		}

		public static float Width(string text, UiFont font, bool isBold = false)
		{
			return Size(text, font, isBold).x;
		}

		/// <summary>
		/// 测高。一律按「会换行」测：所有调用点绘制时都是 wrap = true，
		/// 而 style.wordWrap 默认是 false，直接 CalcHeight 会把换行后的高度算矮，
		/// 结果是文字画出自己的矩形、压到下面的内容上（下一块的不透明底再盖回来，看着就像两块重叠）。
		/// </summary>
		public static float Height(string text, UiFont font, float width, bool isBold = false)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0f;
			}
			GUIStyle style = Style(font, isBold);
			bool previousWrap = style.wordWrap;
			style.wordWrap = true;
			float height = style.CalcHeight(new GUIContent(text.StripTags()), width);
			style.wordWrap = previousWrap;
			return height;
		}

		/// <summary>单行省略号（等价 CSS text-overflow: ellipsis）。带富文本标签时不截断，直接交给裁剪。</summary>
		public static string Clamp(string text, UiFont font, float width, bool isBold = false)
		{
			if (string.IsNullOrEmpty(text) || width <= 0f || text.IndexOf('<') >= 0)
			{
				return text;
			}
			if (Width(text, font, isBold) <= width)
			{
				return text;
			}
			string suffix = "...";
			float suffixWidth = Width(suffix, font, isBold);
			if (suffixWidth > width)
			{
				return string.Empty;
			}
			int low = 0;
			int high = text.Length;
			while (low < high)
			{
				int mid = (low + high + 1) / 2;
				if (Width(text.Substring(0, mid), font, isBold) + suffixWidth <= width)
				{
					low = mid;
				}
				else
				{
					high = mid - 1;
				}
			}
			return text.Substring(0, low).TrimEnd() + suffix;
		}

		/// <summary>
		/// 多行截断（等价 -webkit-line-clamp）：返回带 \n 的文本，调用方用 wrap=false 绘制。
		/// 中英文都能换行：ASCII 单词整体成块，其余字符逐字成块。
		/// </summary>
		public static string ClampLines(string text, UiFont font, float width, int maxLines, bool isBold, out bool truncated)
		{
			truncated = false;
			if (string.IsNullOrEmpty(text) || width <= 0f || maxLines < 1)
			{
				return text;
			}
			if (text.IndexOf('<') >= 0)
			{
				return text;
			}
			StringBuilder line = new StringBuilder();
			StringBuilder result = new StringBuilder();
			int lineCount = 1;
			int index = 0;
			while (index < text.Length)
			{
				if (text[index] == '\n')
				{
					AppendLine(result, line);
					line.Length = 0;
					lineCount++;
					index++;
					if (lineCount > maxLines)
					{
						truncated = true;
						break;
					}
					continue;
				}
				string token = NextToken(text, ref index);
				if (line.Length == 0 && token == " ")
				{
					continue;
				}
				if (Width(line.ToString() + token, font, isBold) <= width || line.Length == 0)
				{
					line.Append(token);
					continue;
				}
				AppendLine(result, line);
				line.Length = 0;
				lineCount++;
				if (lineCount > maxLines)
				{
					truncated = true;
					break;
				}
				if (token != " ")
				{
					line.Append(token);
				}
			}
			if (!truncated && line.Length > 0)
			{
				AppendLine(result, line);
			}
			else if (truncated && result.Length > 0)
			{
				// 在最后一行尾部加省略号，尽量放得下
				int lastBreak = result.Length - 1;
				while (lastBreak > 0 && result[lastBreak] != '\n')
				{
					lastBreak--;
				}
				string lastLine = result.ToString(lastBreak + 1, result.Length - lastBreak - 1);
				string clamped = Clamp(lastLine, font, width, isBold);
				result.Length = lastBreak + 1;
				result.Append(clamped);
			}
			return result.ToString().TrimEnd();
		}

		private static void AppendLine(StringBuilder result, StringBuilder line)
		{
			if (result.Length > 0)
			{
				result.Append('\n');
			}
			result.Append(line.ToString().TrimEnd());
		}

		private static string NextToken(string text, ref int index)
		{
			char c = text[index];
			if (c == ' ')
			{
				index++;
				return " ";
			}
			bool asciiWord = (c < 128) && !char.IsPunctuation(c) && !char.IsWhiteSpace(c);
			if (!asciiWord)
			{
				index++;
				return c.ToString();
			}
			int start = index;
			while (index < text.Length)
			{
				char current = text[index];
				if (current >= 128 || char.IsWhiteSpace(current) || char.IsPunctuation(current))
				{
					break;
				}
				index++;
			}
			return text.Substring(start, index - start);
		}
	}
}
