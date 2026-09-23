using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// UI 的颜色 token，三套风格共用同一份名单：
	/// 原版风（深灰底 + 白字，接近 RimWorld 自己的界面）、
	/// 现代科技风（浅色科研界面 + 深色信息块 + 黄绿色品牌强调）、
	/// 现代科技风·黑夜（沿用现代风的布局，底色压成近黑，黑白关系整体反转，品牌绿保留）。
	/// 页面与控件不得直接散写颜色，统一从这里取。
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

		private static Color Rgba(int rgb, float alpha)
		{
			Color c = Hex(rgb);
			c.a = alpha;
			return c;
		}

		private static OutpostUiStyle Style
		{
			get { return DreamsOutpostsMod.UiStyle; }
		}

		/// <summary>按当前风格三选一：vanilla = 原版风，modern = 现代科技风，dark = 现代科技风·黑夜。</summary>
		private static Color Pick(Color vanilla, Color modern, Color dark)
		{
			switch (Style)
			{
			case OutpostUiStyle.ModernTechDark:
				return dark;

			case OutpostUiStyle.ModernTech:
				return modern;

			case OutpostUiStyle.Vanilla:
			default:
				return vanilla;
			}
		}

		// ---------- 页面与玻璃层 ----------
		/// <summary>黑夜底与 Background2（#111213）同色系，保证背景图与底色之间没有硬边。</summary>
		public static Color Bg0 { get { return Pick(Hex(0x15191D), Hex(0xD1CFCB), Hex(0x121417)); } }
		public static Color Surface { get { return Pick(Hex(0x15191D), Hex(0xD1CFCB), Hex(0x121417)); } }
		/// <summary>
		/// 内容卡片的底：浅色风的纯白底只留 50% 不透明度，好让背景图透出来；
		/// 原版风与黑夜风保持自己的实色 / 深色玻璃，不受影响。
		/// </summary>
		public static Color Card { get { return Pick(Hex(0x1B2025), Rgba(0xFFFFFF, 0.50f), Rgba(0x1A1E23, 0.78f)); } }
		public static Color Raised { get { return Pick(Hex(0x20262C), Rgba(0xEDF1EB, 0.52f), Rgba(0x262B31, 0.82f)); } }
		public static Color Hover { get { return Pick(Hex(0x252C33), Rgba(0xE8EEE3, 0.94f), Rgba(0x2E353C, 0.94f)); } }
		public static Color Track { get { return Pick(Hex(0x384047), Hex(0xD8DED4), Hex(0x2A3138)); } }

		/// <summary>背景图上用于压低对比度的雾层。</summary>
		public static Color BackgroundVeil { get { return Pick(Clear, Rgba(0xFFFFFF, 0.12f), Rgba(0x000000, 0.28f)); } }
		/// <summary>左栏的磨砂层：浅色风提亮，黑夜风压暗，让左栏比正文更深一档。</summary>
		public static Color SidebarVeil { get { return Pick(Clear, Rgba(0xF3F4F0, 0.62f), Rgba(0x0D1013, 0.55f)); } }
		/// <summary>正文区域的轻玻璃层。</summary>
		public static Color ContentVeil { get { return Pick(Clear, Rgba(0xFFFFFF, 0.04f), Rgba(0xFFFFFF, 0.02f)); } }
		/// <summary>普通信息板。</summary>
		public static Color PanelGlass { get { return Pick(Hex(0x1B2025), Rgba(0xFFFFFF, 0.78f), Rgba(0x1B2025, 0.86f)); } }
		/// <summary>需要更稳定可读性的信息板。</summary>
		public static Color PanelGlassStrong { get { return Pick(Hex(0x20262C), Rgba(0xFFFFFF, 0.90f), Rgba(0x20262C, 0.94f)); } }

		// ---------- 边框 ----------
		/// <summary>
		/// 浅色风的 1px 描边比原来（#C3C8BF）加深一档，
		/// 补上卡片底降到 50% 不透明度后变虚的轮廓；仍明显浅于 LineStrong，悬停时的层次不变。
		/// </summary>
		public static Color Line { get { return Pick(Rgba(0xFFFFFF, 0.20f), Hex(0xADB4A8), Rgba(0xFFFFFF, 0.16f)); } }
		public static Color LineStrong { get { return Pick(Rgba(0xFFFFFF, 0.38f), Hex(0x929B90), Rgba(0xFFFFFF, 0.30f)); } }
		/// <summary>chip 的 1px 外框：浅色底用黑框，深色底用半透明白框。</summary>
		public static Color ChipFrame { get { return Pick(Color.white, Color.black, Rgba(0xFFFFFF, 0.20f)); } }

		// ---------- 文字 ----------
		/// <summary>黑夜风把正文整体翻成近白，最亮只到 #F2F5F0，避免纯白在深底上发刺。</summary>
		public static Color Ink { get { return Pick(Color.white, Hex(0x1A1E1A), Hex(0xF2F5F0)); } }
		public static Color Ink2 { get { return Pick(Color.white, Hex(0x5E675D), Hex(0xAEB6B0)); } }
		public static Color Ink3 { get { return Pick(Color.white, Hex(0x71796F), Hex(0x878F8A)); } }

		// ---------- 品牌色 ----------
		private static readonly Color ModernTechBrand = Hex(0xB5D53D);
		private static readonly Color ModernTechBrandHover = Hex(0x95BA2B);
		private static readonly Color ModernTechBrandText = Hex(0x607D08);
		private static readonly Color ModernTechBrandTint = Rgba(0xEDF6D2, 0.94f);
		private static readonly Color ModernTechBrandLine = Hex(0xA1C936);
		private static readonly Color ModernTechAccent = Hex(0xB3D94A);

		// 黑夜风保留同一支绿色，只按深底调整明度：悬停要更亮，文字要浅。
		private static readonly Color DarkBrand = Hex(0xB5D53D);
		private static readonly Color DarkBrandHover = Hex(0xC9E95B);
		private static readonly Color DarkBrandText = Hex(0xC3E356);
		private static readonly Color DarkBrandTint = Rgba(0x2C3A14, 0.72f);
		private static readonly Color DarkBrandLine = Hex(0x7E9B2C);
		private static readonly Color DarkAccent = Hex(0xB3D94A);

		// 绿底上仍然是深字（绿本身够亮，深字对比度最好），三套风格一致。
		private static readonly Color ModernTechOnAccent = Hex(0x11150F);

		public static Color Brand { get { return Pick(Color.white, ModernTechBrand, DarkBrand); } }
		public static Color BrandHover { get { return Pick(Hex(0xD8D8D8), ModernTechBrandHover, DarkBrandHover); } }
		public static Color BrandText { get { return Pick(Ink, ModernTechBrandText, DarkBrandText); } }

		// 信息链接独立于品牌强调：原版浅蓝 / 黄色，现代主题保留原有文字层级与绿色悬停。
		public static Color InfoLinkText { get { return Pick(Widgets.NormalOptionColor, Ink, Ink); } }
		public static Color InfoLinkHover { get { return Pick(Widgets.MouseoverOptionColor, ModernTechBrandText, DarkBrandText); } }
		public static Color InfoLinkTextFor(Color normalColor)
		{
			return Pick(InfoLinkText, normalColor, normalColor);
		}
		public static Color BrandTint { get { return Pick(new Color(1f, 1f, 1f, 0.18f), ModernTechBrandTint, DarkBrandTint); } }
		public static Color BrandLine { get { return Pick(Color.white, ModernTechBrandLine, DarkBrandLine); } }
		public static Color Accent { get { return Pick(Color.white, ModernTechAccent, DarkAccent); } }
		public static Color OnAccent { get { return Pick(Hex(0x2B2C32), ModernTechOnAccent, ModernTechOnAccent); } }

		// ---------- 导航专用 ----------
		// 选中底「黑白反转」：浅色风是黑块白字，黑夜风是白块黑字。
		public static Color NavIdle { get { return Pick(Rgba(0xFFFFFF, 0.34f), Rgba(0xFFFFFF, 0.34f), Rgba(0xFFFFFF, 0.10f)); } }
		public static Color NavHover { get { return Pick(Rgba(0xF0F5E9, 0.92f), Rgba(0xF0F5E9, 0.92f), Rgba(0xFFFFFF, 0.10f)); } }
		public static Color NavActive { get { return Pick(Hex(0x20241F), Hex(0x20241F), Hex(0xF2F4EF)); } }
		public static Color NavActiveText { get { return Pick(Hex(0xFAFBF8), Hex(0xFAFBF8), Hex(0x15181A)); } }
		public static Color NavActiveSub { get { return Pick(Hex(0xC7CEC3), Hex(0xC7CEC3), Hex(0x4A5148)); } }

		/// <summary>设施主视觉的深色方块底（成品图缺失时的占位与兜底）。上面画的是白色图标，所以三套风格都保持深色。</summary>
		public static Color ArtBlock { get { return Pick(Hex(0x20241F), Hex(0x20241F), Hex(0x2B3138)); } }

		// ---------- 语义状态色 ----------
		// 浅色风是「淡底 + 深字」，黑夜风翻成「暗底 + 亮字」，底色本身也压暗一档。
		public static Color Good { get { return Pick(Hex(0x4D8D43), Hex(0x4D8D43), Hex(0x8FD07F)); } }
		public static Color GoodBg { get { return Pick(Hex(0xE9F3E5), Hex(0xE9F3E5), Hex(0x1E2A1B)); } }
		public static Color GoodLine { get { return Pick(Hex(0xA8C7A2), Hex(0xA8C7A2), Hex(0x3A5233)); } }
		public static Color Warn { get { return Pick(Hex(0x9B7200), Hex(0x9B7200), Hex(0xE6B94A)); } }
		public static Color WarnBg { get { return Pick(Hex(0xF7F0D9), Hex(0xF7F0D9), Hex(0x2E2716)); } }
		public static Color WarnLine { get { return Pick(Hex(0xD8C17A), Hex(0xD8C17A), Hex(0x5C4D22)); } }
		public static Color Bad { get { return Pick(Hex(0xB94E4E), Hex(0xB94E4E), Hex(0xE38B8B)); } }
		public static Color BadBg { get { return Pick(Hex(0xF7E5E4), Hex(0xF7E5E4), Hex(0x2E1D1D)); } }
		public static Color BadLine { get { return Pick(Hex(0xD8A3A0), Hex(0xD8A3A0), Hex(0x5E3838)); } }
		public static Color Info { get { return Pick(Hex(0x3F769B), Hex(0x3F769B), Hex(0x84BBDD)); } }
		public static Color InfoBg { get { return Pick(Hex(0xE4EFF5), Hex(0xE4EFF5), Hex(0x1A2831)); } }
		public static Color InfoLine { get { return Pick(Hex(0xA2BECE), Hex(0xA2BECE), Hex(0x35505F)); } }
		public static Color Purple { get { return Pick(Hex(0x7665A8), Hex(0x7665A8), Hex(0xA995DC)); } }

		public static readonly Color Legendary = ColorLibrary.Gold;

		// ---------- 亮底 / 深底 ----------
		/// <summary>纯白。只用于深色方块（设施主视觉）上的图标，所以三套风格都保持白色。</summary>
		public static readonly Color Light = Hex(0xFFFFFF);
		public static readonly Color LightHover = Hex(0xEFF2ED);
		public static readonly Color OnLight = Hex(0x222722);

		// ---------- 破坏性按钮 ----------
		public static Color Danger { get { return Pick(Hex(0xA43F3F), Hex(0xA43F3F), Hex(0xBE514D)); } }
		public static Color DangerHover { get { return Pick(Hex(0x8F3333), Hex(0x8F3333), Hex(0xA8403C)); } }

		// ---------- 滚动条 ----------
		public static Color ScrollThumb { get { return Pick(Hex(0xA6ADA3), Hex(0xA6ADA3), Hex(0x4A5259)); } }
		public static Color ScrollThumbHover { get { return Pick(Hex(0x777F75), Hex(0x777F75), Hex(0x6C757D)); } }

		// ---------- 遮罩 ----------
		public static Color Scrim { get { return Pick(new Color(0f, 0f, 0f, 0.38f), new Color(0f, 0f, 0f, 0.38f), new Color(0f, 0f, 0f, 0.55f)); } }

		public const float DisabledAlpha = 0.45f;
		public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

		public static Color WithAlpha(Color c, float a)
		{
			return new Color(c.r, c.g, c.b, c.a * a);
		}
	}
}
