using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 新 UI 的颜色 token。莱茵生命方向：冷白科研界面 + 深色信息块 + 黄绿色品牌强调。
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

		// ---------- 页面与玻璃层 ----------
		public static Color Bg0 { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x15191D) : Hex(0xE6E6E6); } }
		public static Color Surface { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x15191D) : Hex(0xE6E6E6); } }
		public static Color Card { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x1B2025) : Rgba(0xFFFFFF, 0.48f); } }
		public static Color Raised { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x20262C) : Rgba(0xEDF1EB, 0.52f); } }
		public static Color Hover { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x252C33) : Rgba(0xE8EEE3, 0.94f); } }
		public static Color Track { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x384047) : Hex(0xD8DED4); } }

		/// <summary>背景图上用于压低对比度的轻白雾层。</summary>
		public static Color BackgroundVeil { get { return DreamsOutpostsMod.UseVanillaUi ? Clear : Rgba(0xFFFFFF, 0.03f); } }
		/// <summary>左栏的磨砂白层。</summary>
		public static Color SidebarVeil { get { return DreamsOutpostsMod.UseVanillaUi ? Clear : Rgba(0xF7F9F5, 0.10f); } }
		/// <summary>正文区域的轻玻璃层。</summary>
		public static Color ContentVeil { get { return DreamsOutpostsMod.UseVanillaUi ? Clear : Rgba(0xFFFFFF, 0.04f); } }
		/// <summary>普通科研信息板。</summary>
		public static Color PanelGlass { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x1B2025) : Rgba(0xFFFFFF, 0.48f); } }
		/// <summary>需要更稳定可读性的科研信息板。</summary>
		public static Color PanelGlassStrong { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x20262C) : Rgba(0xFFFFFF, 0.54f); } }

		// ---------- 边框 ----------
		public static Color Line { get { return DreamsOutpostsMod.UseVanillaUi ? Rgba(0xFFFFFF, 0.20f) : Hex(0xCDD3C9); } }
		public static Color LineStrong { get { return DreamsOutpostsMod.UseVanillaUi ? Rgba(0xFFFFFF, 0.38f) : Hex(0x929B90); } }

		// ---------- 文字 ----------
		public static Color Ink { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : Hex(0x1A1E1A); } }
		public static Color Ink2 { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : Hex(0x5E675D); } }
		public static Color Ink3 { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : Hex(0x90988F); } }

		// ---------- 品牌色 ----------
		private static readonly Color IndustrialBrand = Hex(0xA8CF38);
		private static readonly Color IndustrialBrandHover = Hex(0x95BA2B);
		private static readonly Color IndustrialBrandText = Hex(0x607D08);
		private static readonly Color IndustrialBrandTint = Rgba(0xEDF6D2, 0.94f);
		private static readonly Color IndustrialBrandLine = Hex(0xA1C936);
		private static readonly Color IndustrialAccent = Hex(0xB3D94A);
		private static readonly Color IndustrialOnAccent = Hex(0x11150F);

		public static Color Brand { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : IndustrialBrand; } }
		public static Color BrandHover { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0xD8D8D8) : IndustrialBrandHover; } }
		public static Color BrandText { get { return DreamsOutpostsMod.UseVanillaUi ? Ink : IndustrialBrandText; } }
		public static Color BrandTint { get { return DreamsOutpostsMod.UseVanillaUi ? new Color(1f, 1f, 1f, 0.18f) : IndustrialBrandTint; } }
		public static Color BrandLine { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : IndustrialBrandLine; } }
		public static Color Accent { get { return DreamsOutpostsMod.UseVanillaUi ? Color.white : IndustrialAccent; } }
		public static Color OnAccent { get { return DreamsOutpostsMod.UseVanillaUi ? Hex(0x2B2C32) : IndustrialOnAccent; } }

		// ---------- 导航专用 ----------
		public static readonly Color NavIdle = Rgba(0xFFFFFF, 0.34f);
		public static readonly Color NavHover = Rgba(0xF0F5E9, 0.92f);
		public static readonly Color NavActive = Hex(0x20241F);
		public static readonly Color NavActiveText = Hex(0xFAFBF8);
		public static readonly Color NavActiveSub = Hex(0xC7CEC3);

		// ---------- 语义状态色 ----------
		public static readonly Color Good = Hex(0x4D8D43);
		public static readonly Color GoodBg = Hex(0xE9F3E5);
		public static readonly Color GoodLine = Hex(0xA8C7A2);
		public static readonly Color Warn = Hex(0x9B7200);
		public static readonly Color WarnBg = Hex(0xF7F0D9);
		public static readonly Color WarnLine = Hex(0xD8C17A);
		public static readonly Color Bad = Hex(0xB94E4E);
		public static readonly Color BadBg = Hex(0xF7E5E4);
		public static readonly Color BadLine = Hex(0xD8A3A0);
		public static readonly Color Info = Hex(0x3F769B);
		public static readonly Color InfoBg = Hex(0xE4EFF5);
		public static readonly Color InfoLine = Hex(0xA2BECE);
		public static readonly Color Purple = Hex(0x7665A8);

		public static readonly Color Legendary = ColorLibrary.Gold;

		// ---------- 亮底 / 深底 ----------
		public static readonly Color Light = Hex(0xFFFFFF);
		public static readonly Color LightHover = Hex(0xEFF2ED);
		public static readonly Color OnLight = Hex(0x222722);

		// ---------- 破坏性按钮 ----------
		public static readonly Color Danger = Hex(0xA43F3F);
		public static readonly Color DangerHover = Hex(0x8F3333);

		// ---------- 滚动条 ----------
		public static readonly Color ScrollThumb = Hex(0xA6ADA3);
		public static readonly Color ScrollThumbHover = Hex(0x777F75);

		// ---------- 遮罩 ----------
		public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.38f);

		public const float DisabledAlpha = 0.45f;
		public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

		public static Color WithAlpha(Color c, float a)
		{
			return new Color(c.r, c.g, c.b, c.a * a);
		}
	}
}