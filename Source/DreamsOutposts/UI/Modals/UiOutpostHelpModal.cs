using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 倾向说明弹窗正文：纯阅读的两段说明，没有可点的内容。
	/// 只用「一组小标题 / 段落」的简单结构，所以不复用 UiIntroTipsModalBody 那套「标签 + 缩进正文」的排版。
	/// 测高与绘制共用同一套累加，否则中英文换行行数不一致时两段会互相压住。
	/// </summary>
	public sealed class UiOutpostHelpModalBody : IUiModalBody
	{
		/// <summary>段落之间（含小标题上方的留白）。</summary>
		private const float SectionGap = 18f;

		/// <summary>小标题与它下面正文的距离。</summary>
		private const float HeadGap = 8f;

		/// <summary>小标题底下那条分隔线往上的空隙。</summary>
		private const float HeadLineGap = 5f;

		/// <summary>分段标题 + 正文的键；正文为空的段落整段不画。</summary>
		private static readonly string[] DefaultSectionKeys =
		{
			"DreamsOutposts.Ui.Help.Events",
			"DreamsOutposts.Ui.Help.Population"
		};

		/// <summary>本弹窗要显示的段落。不传时用事件页 / 酒馆页那两段（倾向与人口倾向）。</summary>
		private readonly string[] sectionKeys;

		public UiOutpostHelpModalBody()
			: this(null)
		{
		}

		public UiOutpostHelpModalBody(string[] sectionKeys)
		{
			this.sectionKeys = (sectionKeys == null || sectionKeys.Length == 0) ? DefaultSectionKeys : sectionKeys;
		}

		public float Height(float width)
		{
			float height = 0f;
			for (int i = 0; i < sectionKeys.Length; i++)
			{
				if (Text(sectionKeys[i]).NullOrEmpty())
				{
					continue;
				}
				if (height > 0f)
				{
					height += SectionGap;
				}
				height += HeadHeight() + HeadGap + UiText.Height(Text(sectionKeys[i]), UiFont.Body, width);
			}
			return Mathf.Max(height, 1f);
		}

		public void Draw(Rect rect)
		{
			float y = rect.y;
			float width = rect.width;
			bool first = true;
			for (int i = 0; i < sectionKeys.Length; i++)
			{
				string text = Text(sectionKeys[i]);
				if (text.NullOrEmpty())
				{
					continue;
				}
				if (!first)
				{
					y += SectionGap;
				}
				first = false;
				float headHeight = HeadHeight();
				DrawHead(new Rect(rect.x, y, width, headHeight), sectionKeys[i]);
				y += headHeight + HeadGap;
				float textHeight = UiText.Height(text, UiFont.Body, width);
				UiText.Draw(new Rect(rect.x, y, width, textHeight), text, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
				y += textHeight;
			}
		}

		private static float HeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + HeadLineGap;
		}

		private static void DrawHead(Rect rect, string key)
		{
			UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.LineHeight(UiFont.Body)),
				(key + ".Head").Translate(), UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true);
			// 分隔线画在标题文字这一行的底部，而不是 rect 底部：HeadHeight 里的 HeadLineGap 只是留给它的空隙
			UiDraw.Divider(new Rect(rect.x, rect.y + UiText.LineHeight(UiFont.Body) + HeadLineGap - 1f, rect.width, 1f), UiPalette.Line);
		}

		private static string Text(string key)
		{
			return key.Translate();
		}
	}

	/// <summary>说明弹窗的开法：事件页 / 酒馆页的「(?)提示」与仓库页殖民者栏的「(?)提示」共用同一个窗口。</summary>
	public static class UiOutpostHelpWindow
	{
		/// <summary>打开默认两段（倾向 / 人口倾向）的说明弹窗。</summary>
		public static void Open()
		{
			Open(null);
		}

		/// <summary>打开只显示指定段落的说明弹窗；sectionKeys 为空时等同 Open()。</summary>
		public static void Open(string[] sectionKeys)
		{
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = "DreamsOutposts.Ui.Help.Title".Translate(),
				PanelWidth = UiMetrics.ModalNarrowWidth,
				Body = new UiOutpostHelpModalBody(sectionKeys)
			};
			// 只有「确认」一颗按钮。宽度一次算死，免得两种语言下按钮忽宽忽窄。
			float buttonWidth = Mathf.Max(UiWidgets.ButtonWidth("DreamsOutposts.Ui.Option.Confirm".Translate()), 120f);
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			window.FooterDrawer = delegate(Rect rect)
			{
				Rect button = new Rect(rect.xMax - buttonWidth, rect.y + (rect.height - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
				if (UiWidgets.Button(button, "DreamsOutposts.Ui.Option.Confirm".Translate(), UiButtonKind.Primary))
				{
					window.Close();
				}
			};
			Find.WindowStack.Add(window);
		}
	}
}
