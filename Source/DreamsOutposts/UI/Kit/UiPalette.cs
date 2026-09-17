using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 新 UI 的色板 token（深色主题）。这里是唯一的颜色来源：不要在页面里写裸 Color。
	/// 语义与 temp/styles.css 的 :root 一一对应，但这里是深色主题（深底 + 浅字）。
	/// </summary>
	public static class UiPalette
	{
		private static Color Hex(int rgb)
		{
			return new Color(
				(float)((rgb >> 16) & 0xFF) / 255f,
				(float)((rgb >> 8) & 0xFF) / 255f,
				(float)(rgb & 0xFF) / 255f,
				1f);
		}

		// ---------- 表面（由深到浅三档 + 悬停/轨道） ----------
		/// <summary>页面底色（窗口之外，游戏里基本用不到）。</summary>
		public static readonly Color Bg0 = Hex(0x121316);

		/// <summary>窗口 / 弹窗面板表面。</summary>
		public static readonly Color Surface = Hex(0x1B1C20);

		/// <summary>卡片表面（设施卡、面板、安装卡）。</summary>
		public static readonly Color Card = Hex(0x232428);

		/// <summary>抬起的子表面：标题栏 / 侧栏 / 弹窗头尾 / 次级按钮 / 输入框 / 头像底。</summary>
		public static readonly Color Raised = Hex(0x2B2C32);

		/// <summary>悬停底色。</summary>
		public static readonly Color Hover = Hex(0x33343B);

		/// <summary>进度条 / pip 的轨道底色。</summary>
		public static readonly Color Track = Hex(0x3A3B43);

		// ---------- 边框 ----------
		public static readonly Color Line = Hex(0x35363D);

		public static readonly Color LineStrong = Hex(0x4C4E58);

		// ---------- 文字 ----------
		/// <summary>主文字（原浅色主题的 #1a1a1a，深色下是近白）。</summary>
		public static readonly Color Ink = Hex(0xF1F2F4);

		/// <summary>次要文字。</summary>
		public static readonly Color Ink2 = Hex(0xA7AAB3);

		/// <summary>弱化色：只用于装饰性图标，不用于正文。</summary>
		public static readonly Color Ink3 = Hex(0x71747C);

		// ---------- 品牌色 ----------
		/// <summary>主按钮填充（白字在其上可读）。</summary>
		public static readonly Color Brand = Hex(0x047857);

		/// <summary>主按钮悬停填充。</summary>
		public static readonly Color BrandHover = Hex(0x059669);

		/// <summary>品牌色文字 / 图标（深底上的亮绿）。</summary>
		public static readonly Color BrandText = Hex(0x6EE7B7);

		/// <summary>品牌浅底（激活的侧栏项、按钮上的浅色底）。</summary>
		public static readonly Color BrandTint = Hex(0x12302A);

		/// <summary>品牌描边。</summary>
		public static readonly Color BrandLine = Hex(0x1F5C4A);

		/// <summary>进度条 / 分解条 / 权重条的填充色（深底上的亮绿，比 Brand 更醒目）。</summary>
		public static readonly Color Accent = Hex(0x34D399);

		/// <summary>品牌色填充上的文字 / 图标。</summary>
		public static readonly Color OnAccent = Hex(0xFFFFFF);

		// ---------- 语义状态色 ----------
		public static readonly Color Good = Hex(0x6EE7B7);
		public static readonly Color GoodBg = Hex(0x12302A);
		public static readonly Color GoodLine = Hex(0x1F5C4A);
		public static readonly Color Warn = Hex(0xFCD34D);
		public static readonly Color WarnBg = Hex(0x35290F);
		public static readonly Color WarnLine = Hex(0x6B5220);
		public static readonly Color Bad = Hex(0xFCA5A5);
		public static readonly Color BadBg = Hex(0x3A1D1D);
		public static readonly Color BadLine = Hex(0x6E2F2F);
		public static readonly Color Info = Hex(0x93C5FD);
		public static readonly Color InfoBg = Hex(0x18283F);
		public static readonly Color InfoLine = Hex(0x2B4A73);
		public static readonly Color Purple = Hex(0xC4B5FD);

		/// <summary>「传说」显示用的金色：直接用原版的 ColorLibrary.Gold（#DBB40C）。</summary>
		public static readonly Color Legendary = ColorLibrary.Gold;

		// ---------- 亮底控件（白底 + 深色内容，如关闭按钮） ----------
		/// <summary>亮底控件的填充。</summary>
		public static readonly Color Light = Hex(0xFFFFFF);

		/// <summary>亮底控件的悬停填充。</summary>
		public static readonly Color LightHover = Hex(0xE4E6EA);

		/// <summary>亮底控件上的深色图标（浅黑）。</summary>
		public static readonly Color OnLight = Hex(0x2B2C32);

		// ---------- 破坏性按钮（拆除） ----------
		public static readonly Color Danger = Hex(0x7F1D1D);

		public static readonly Color DangerHover = Hex(0x991B1B);

		// ---------- 滚动条 ----------
		public static readonly Color ScrollThumb = Hex(0x4B4D55);

		public static readonly Color ScrollThumbHover = Hex(0x5E606A);

		// ---------- 遮罩 ----------
		public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.55f);

		/// <summary>禁用整体透明度。</summary>
		public const float DisabledAlpha = 0.45f;

		/// <summary>通用透明。</summary>
		public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

		/// <summary>把颜色整体套一个 alpha（用于禁用态、跟踪态）。</summary>
		public static Color WithAlpha(Color c, float a)
		{
			return new Color(c.r, c.g, c.b, c.a * a);
		}
	}
}
