using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 纯文字提示弹窗正文：一段说明，没有任何可点的内容。
	/// Height 与 Draw 共用同一套测量，否则中英文换行行数不一致时文字会互相压住。
	/// </summary>
	public sealed class UiNoticeModalBody : IUiModalBody
	{
		private readonly string text;

		public UiNoticeModalBody(string text)
		{
			this.text = text ?? string.Empty;
		}

		public float Height(float width)
		{
			return text.NullOrEmpty() ? 0f : UiText.Height(text, UiFont.Body, width);
		}

		public void Draw(Rect rect)
		{
			if (text.NullOrEmpty())
			{
				return;
			}
			UiText.Draw(new Rect(rect.x, rect.y, rect.width, UiText.Height(text, UiFont.Body, rect.width)),
				text, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
		}
	}

	/// <summary>
	/// 通用提示弹窗的开法：一个标题、一段正文、右下角一个关闭按钮。
	/// 和更新日志弹窗一样，玩家读的时候把游戏停下来。
	/// </summary>
	public static class UiNoticeWindow
	{
		public static void Open(string title, string text)
		{
			if (text.NullOrEmpty())
			{
				return;
			}
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = title,
				PanelWidth = UiMetrics.ModalNormalWidth,
				Body = new UiNoticeModalBody(text)
			};
			window.forcePause = true;
			string closeLabel = "DreamsOutposts.Ui.Close".Translate();
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float buttonWidth = Mathf.Max(UiWidgets.ButtonWidth(closeLabel), 120f);
			window.FooterDrawer = delegate(Rect rect)
			{
				Rect button = new Rect(rect.xMax - buttonWidth, rect.y + (rect.height - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
				if (UiWidgets.Button(button, closeLabel, UiButtonKind.Primary))
				{
					window.Close();
				}
			};
			Find.WindowStack.Add(window);
		}
	}
}
