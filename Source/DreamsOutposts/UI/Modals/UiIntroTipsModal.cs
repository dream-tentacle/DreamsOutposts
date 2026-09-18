using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 开局指引弹窗正文：「重要」与「有用的知识」两段纯说明，没有任何可点的内容。
	/// 测高与绘制共用同一套测量，否则中英文换行行数不一致时文字会互相压住。
	/// </summary>
	public sealed class UiIntroTipsModalBody : IUiModalBody
	{
		private const float SectionGap = 22f;

		private const float SectionHeadGap = 12f;

		/// <summary>区块标题底下留给分隔线的空隙。</summary>
		private const float SectionHeadLineGap = 6f;

		private const float LabelGap = 3f;

		private const float RowGap = 14f;

		private const float TextIndent = 12f;

		/// <summary>重要：一进游戏就该知道的事。</summary>
		private static readonly string[] ImportantKeys =
		{
			"DreamsOutposts.IntroTips.Important.Build",
			"DreamsOutposts.IntroTips.Important.Units",
			"DreamsOutposts.IntroTips.Important.Multiplier",
			"DreamsOutposts.IntroTips.Important.UiStyle",
			"DreamsOutposts.IntroTips.Important.Manage",
			"DreamsOutposts.IntroTips.Important.Disable"
		};

		/// <summary>有用的知识：机制说明，玩的过程中再回来看。</summary>
		private static readonly string[] KnowledgeKeys =
		{
			"DreamsOutposts.IntroTips.Knowledge.Defense",
			"DreamsOutposts.IntroTips.Knowledge.SpecialBuildings",
			"DreamsOutposts.IntroTips.Knowledge.Raid",
			"DreamsOutposts.IntroTips.Knowledge.Bombardment"
		};

		public float Height(float width)
		{
			return SectionHeight(width, ImportantKeys) + SectionGap + SectionHeight(width, KnowledgeKeys);
		}

		public void Draw(Rect rect)
		{
			UiStack stack = UiStack.Begin(rect);
			DrawSection(ref stack, "DreamsOutposts.IntroTips.SectionImportant".Translate(), ImportantKeys);
			stack.Gap(SectionGap);
			DrawSection(ref stack, "DreamsOutposts.IntroTips.SectionKnowledge".Translate(), KnowledgeKeys);
		}

		private static float SectionHeight(float width, string[] keys)
		{
			float height = SectionHeadHeight() + SectionHeadGap;
			for (int i = 0; i < keys.Length; i++)
			{
				if (i > 0)
				{
					height += RowGap;
				}
				height += RowHeight(width, keys[i]);
			}
			return height;
		}

		private static float SectionHeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + SectionHeadLineGap;
		}

		private static float RowHeight(float width, string key)
		{
			float textWidth = Mathf.Max(width - TextIndent, 40f);
			return UiText.Height(Label(key), UiFont.Body, width, true) + LabelGap + UiText.Height(Text(key), UiFont.Body, textWidth);
		}

		private static void DrawSection(ref UiStack stack, string head, string[] keys)
		{
			Rect headRect = stack.Next(SectionHeadHeight());
			UiText.Draw(new Rect(headRect.x, headRect.y, headRect.width, UiText.LineHeight(UiFont.Body)),
				head, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true);
			UiDraw.Divider(new Rect(headRect.x, headRect.yMax - 1f, headRect.width, 1f), UiPalette.Line);
			stack.Gap(SectionHeadGap);
			float width = stack.Area.width;
			for (int i = 0; i < keys.Length; i++)
			{
				if (i > 0)
				{
					stack.Gap(RowGap);
				}
				DrawRow(ref stack, width, keys[i]);
			}
		}

		private static void DrawRow(ref UiStack stack, float width, string key)
		{
			string label = Label(key);
			float labelHeight = UiText.Height(label, UiFont.Body, width, true);
			UiText.Draw(stack.Next(labelHeight), label, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true, true);
			stack.Gap(LabelGap);
			string text = Text(key);
			float textWidth = Mathf.Max(width - TextIndent, 40f);
			float textHeight = UiText.Height(text, UiFont.Body, textWidth);
			UiText.Draw(stack.Next(TextIndent, textWidth, textHeight), text, UiFont.Body, UiPalette.Ink2,
				TextAnchor.UpperLeft, false, true);
		}

		private static string Label(string key)
		{
			return (key + ".Label").Translate();
		}

		private static string Text(string key)
		{
			return (key + ".Text").Translate();
		}
	}

	/// <summary>开局指引弹窗的开法：新开局 / 读档后自动弹（带强制阅读时间），也可以在设置里手动打开。</summary>
	public static class UiIntroTipsWindow
	{
		/// <summary>自动弹出时「确定」被锁住的秒数：先让玩家把上面读完。</summary>
		public const float AutoLockSeconds = 10f;

		/// <param name="lockSeconds">大于 0 时，这段时间内「确定」不可点，按钮上显示倒计时。</param>
		public static void Open(float lockSeconds = 0f)
		{
			float startRealtime = Time.realtimeSinceStartup;
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = "DreamsOutposts.IntroTips.Title".Translate(),
				SubText = "DreamsOutposts.IntroTips.Subtitle".Translate(),
				PanelWidth = UiMetrics.ModalNormalWidth,
				Body = new UiIntroTipsModalBody(),
				// 指引没有别的操作，连标题栏的关闭叉也不给。
				ShowCloseButton = false
			};
			// 玩家在读的时候把游戏停下来。
			window.forcePause = true;
			// 唯一的出口是「确定」：Esc 也不关。
			window.closeOnCancel = false;
			// 按钮宽度一次算死（取倒计时最长的那一版），否则倒计时结束时按钮会突然缩一下。
			string confirmLabel = "DreamsOutposts.IntroTips.Confirm".Translate();
			string longestLabel = confirmLabel;
			if (lockSeconds > 0f)
			{
				longestLabel = "DreamsOutposts.IntroTips.ConfirmCountdown".Translate(Mathf.CeilToInt(lockSeconds).ToString());
			}
			float buttonWidth = Mathf.Max(Mathf.Max(UiWidgets.ButtonWidth(confirmLabel), UiWidgets.ButtonWidth(longestLabel)), 120f);
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			window.FooterDrawer = delegate(Rect rect)
			{
				// 用真实时间而不是游戏 tick：弹窗是暂停游戏的，tick 不会走。
				float remaining = lockSeconds - (Time.realtimeSinceStartup - startRealtime);
				bool locked = remaining > 0f;
				string label = confirmLabel;
				if (locked)
				{
					label = "DreamsOutposts.IntroTips.ConfirmCountdown".Translate(Mathf.CeilToInt(remaining).ToString());
				}
				Rect button = new Rect(rect.xMax - buttonWidth, rect.y + (rect.height - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
				if (UiWidgets.Button(button, label, UiButtonKind.Primary, !locked, "DreamsOutposts.IntroTips.ConfirmLocked".Translate()))
				{
					window.Close();
				}
			};
			Find.WindowStack.Add(window);
		}
	}
}
