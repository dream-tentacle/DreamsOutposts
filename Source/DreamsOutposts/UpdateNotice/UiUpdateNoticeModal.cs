using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 更新日志弹窗正文：每个版本一小节（版本号标题行 + 分隔线），下面是该版本的若干条正文。
	/// Height 与 Draw 共用同一套测量，否则中英文换行行数不一致时文字会互相压住。
	/// </summary>
	public sealed class UiUpdateNoticeModalBody : IUiModalBody
	{
		/// <summary>两个版本小节之间的间距。</summary>
		private const float SectionGap = 22f;

		private const float SectionHeadGap = 12f;

		/// <summary>区块标题底下留给分隔线的空隙。</summary>
		private const float SectionHeadLineGap = 6f;

		private const float RowGap = 10f;

		private const string Bullet = "• ";

		private readonly List<UpdateNoticeDef> notices;

		/// <param name="notices">要展示的日志，按给定顺序自上而下排列。</param>
		public UiUpdateNoticeModalBody(List<UpdateNoticeDef> notices)
		{
			this.notices = notices ?? new List<UpdateNoticeDef>();
		}

		public float Height(float width)
		{
			float height = 0f;
			for (int i = 0; i < notices.Count; i++)
			{
				if (i > 0)
				{
					height += SectionGap;
				}
				height += SectionHeight(width, notices[i]);
			}
			return height;
		}

		public void Draw(Rect rect)
		{
			UiStack stack = UiStack.Begin(rect);
			for (int i = 0; i < notices.Count; i++)
			{
				if (i > 0)
				{
					stack.Gap(SectionGap);
				}
				DrawSection(ref stack, notices[i]);
			}
		}

		private static float SectionHeight(float width, UpdateNoticeDef def)
		{
			float height = SectionHeadHeight() + SectionHeadGap;
			float textWidth = Mathf.Max(width - BulletWidth(), 40f);
			List<string> entries = def.entries;
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].NullOrEmpty())
				{
					continue;
				}
				if (i > 0)
				{
					height += RowGap;
				}
				height += UiText.Height(entries[i], UiFont.Body, textWidth);
			}
			return height;
		}

		private static float SectionHeadHeight()
		{
			return UiText.LineHeight(UiFont.Body) + SectionHeadLineGap;
		}

		/// <summary>项目符号的宽度，等于正文的悬挂缩进量。</summary>
		private static float BulletWidth()
		{
			return UiText.Width(Bullet, UiFont.Body);
		}

		private static void DrawSection(ref UiStack stack, UpdateNoticeDef def)
		{
			Rect headRect = stack.Next(SectionHeadHeight());
			UiText.Draw(new Rect(headRect.x, headRect.y, headRect.width, UiText.LineHeight(UiFont.Body)),
				Header(def), UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, true);
			UiDraw.Divider(new Rect(headRect.x, headRect.yMax - 1f, headRect.width, 1f), UiPalette.Line);
			stack.Gap(SectionHeadGap);

			float width = stack.Area.width;
			float bulletWidth = BulletWidth();
			float textWidth = Mathf.Max(width - bulletWidth, 40f);
			List<string> entries = def.entries;
			for (int i = 0; i < entries.Count; i++)
			{
				string text = entries[i];
				if (text.NullOrEmpty())
				{
					continue;
				}
				if (i > 0)
				{
					stack.Gap(RowGap);
				}
				float height = UiText.Height(text, UiFont.Body, textWidth);
				Rect row = stack.Next(height);
				// 悬挂缩进：项目符号单独画在左边，正文右移一段并换行，续行不会跑到符号底下。
				UiText.Draw(new Rect(row.x, row.y, bulletWidth, height), Bullet, UiFont.Body, UiPalette.Ink2);
				UiText.Draw(new Rect(row.x + bulletWidth, row.y, textWidth, height), text, UiFont.Body, UiPalette.Ink2,
					TextAnchor.UpperLeft, false, true);
			}
		}

		/// <summary>版本标题行，例如「版本 1.0.2」。</summary>
		private static string Header(UpdateNoticeDef def)
		{
			return "DreamsOutposts.UpdateNotice.VersionPrefix".Translate(def.version);
		}
	}

	/// <summary>更新日志弹窗的开法：读档时自动弹（只列本次新增的），也可以在设置里手动打开查看全部历史。</summary>
	public static class UiUpdateNoticeWindow
	{
		/// <param name="notices">要展示的日志；为空时不弹窗。</param>
		public static void Open(List<UpdateNoticeDef> notices)
		{
			if (notices == null || notices.Count == 0)
			{
				return;
			}

			// Mod 名作大标题、「更新日志」作小字副标题：一眼就能看出是哪个 Mod 的更新。
			// 万一反查不到所属 Mod，就退回用「更新日志」当标题、不留副标题。
			ModContentPack own = UpdateNoticeUtility.OwnMod;
			string modName = own != null ? own.Name : null;
			bool hasModName = !modName.NullOrEmpty();
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = hasModName
					? modName
					: "DreamsOutposts.UpdateNotice.Title".Translate().ToString(),
				SubText = hasModName
					? "DreamsOutposts.UpdateNotice.Title".Translate().ToString()
					: null,
				PanelWidth = UiMetrics.ModalNormalWidth,
				Body = new UiUpdateNoticeModalBody(notices)
			};
			// 玩家在读的时候把游戏停下来。
			window.forcePause = true;

			string confirmLabel = "DreamsOutposts.UpdateNotice.Confirm".Translate();
			float buttonWidth = Mathf.Max(UiWidgets.ButtonWidth(confirmLabel), 120f);
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			window.FooterDrawer = delegate(Rect rect)
			{
				Rect button = new Rect(rect.xMax - buttonWidth, rect.y + (rect.height - buttonHeight) * 0.5f, buttonWidth, buttonHeight);
				if (UiWidgets.Button(button, confirmLabel, UiButtonKind.Primary))
				{
					window.Close();
				}
			};
			Find.WindowStack.Add(window);
		}
	}
}
