using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>程序手绘图标集合。Def 里可以用名字覆盖（OutpostFacilityDef.uiIcon）。</summary>
	public enum UiIcon
	{
		None,
		Grid,
		Shield,
		Crate,
		Bell,
		Factory,
		Mine,
		Farm,
		Tree,
		Store,
		Turret,
		Workshop,
		Refinery,
		Generator,
		Person,
		Flag,
		Plus,
		Close,
		Check,
		Cross,
		ChevronDown,
		Menu,
		Search,
		Info,
		Dot
	}

	/// <summary>
	/// 炫彩边框的四条边带。上下两段各自连着一个圆角（角上的正方形区算在段里），
	/// 左右两段只覆盖中间直边，四段互不重叠。
	/// </summary>
	public enum RainbowBand
	{
		Top,
		Bottom,
		Left,
		Right
	}

	/// <summary>
	/// 运行时生成并缓存贴图：圆角矩形 9 宫格、柔和阴影、虚线圆角、几何图标。
	/// 全部按「半径 + 颜色」缓存，生成一次反复使用。
	/// </summary>
	public static class UiTex
	{
		private struct BoxKey : IEquatable<BoxKey>
		{
			public int Radius;

			public int BorderWidth;

			public int Fill;

			public int Border;

			public bool Equals(BoxKey other)
			{
				return Radius == other.Radius && BorderWidth == other.BorderWidth && Fill == other.Fill && Border == other.Border;
			}

			public override bool Equals(object obj)
			{
				return obj is BoxKey && Equals((BoxKey)obj);
			}

			public override int GetHashCode()
			{
				return ((Radius * 397) ^ BorderWidth) * 397 ^ Fill * 31 ^ Border;
			}
		}

		private struct TwoKey : IEquatable<TwoKey>
		{
			public int A;

			public int B;

			public bool Equals(TwoKey other)
			{
				return A == other.A && B == other.B;
			}

			public override bool Equals(object obj)
			{
				return obj is TwoKey && Equals((TwoKey)obj);
			}

			public override int GetHashCode()
			{
				return (A * 397) ^ B;
			}
		}


		private const int Supersample = 4;

		public const float ShadowBlur = 12f;

		public const float ShadowAlpha = 0.085f;

		public const float ShadowOffsetY = 5f;

		private static readonly Dictionary<BoxKey, Texture2D> boxCache = new Dictionary<BoxKey, Texture2D>();

		private static readonly Dictionary<TwoKey, Texture2D> shadowCache = new Dictionary<TwoKey, Texture2D>();

		private static readonly Dictionary<TwoKey, Texture2D> cornerCache = new Dictionary<TwoKey, Texture2D>();

		private static readonly Dictionary<UiIcon, Texture2D> iconCache = new Dictionary<UiIcon, Texture2D>();

		private static Texture2D levelUpgradeSweepTexture;

		private static Texture2D positiveButtonTexture;

		private static Texture2D pageHeadDecorTexture;

		private static Texture2D negativeButtonTexture;

		private static readonly Texture2D[] navCornerArcs = new Texture2D[4];

		/// <summary>图标资源目录（相对 Textures/）。所有图标都是静态 PNG，不在运行时绘制。</summary>
		public const string IconFolder = "DreamsOutposts/Ui/";

		/// <summary>
		/// 取图标贴图。文件名 = UiIcon 枚举名，例如 Textures/DreamsOutposts/Ui/Shield.png。
		/// 贴图是白色 + alpha，绘制时用 GUI.color 着色。
		/// </summary>
		public static Texture2D IconTexture(UiIcon icon)
		{
			if (icon == UiIcon.None)
			{
				return null;
			}
			Texture2D cached;
			if (iconCache.TryGetValue(icon, out cached))
			{
				return cached;
			}
			Texture2D texture = ContentFinder<Texture2D>.Get(IconFolder + icon, false);
			if (texture == null)
			{
				Log.WarningOnce("DreamsOutposts UI: icon texture missing: Textures/" + IconFolder + icon
					+ ".png (expected a 64x64 white PNG with alpha).", GenText.StableStringHash("ui-icon-" + icon));
			}
			iconCache[icon] = texture;
			return texture;
		}

		/// <summary>选中侧栏页签的静态白色角弧。corner：0=左上 1=右上 2=左下 3=右下。</summary>
		public static Texture2D NavCornerArc(int corner)
		{
			int index = Mathf.Clamp(corner, 0, 3);
			Texture2D texture = navCornerArcs[index];
			if (texture == null)
			{
				string[] names = { "TL", "TR", "BL", "BR" };
				texture = ContentFinder<Texture2D>.Get(IconFolder + "NavCornerArc" + names[index], false);
				navCornerArcs[index] = texture;
			}
			return texture;
		}

		/// <summary>内容页页头左侧的装饰底图，按页头高度等比缩放。</summary>
		public static Texture2D PageHeadDecorTexture()
		{
			if (pageHeadDecorTexture == null)
			{
				pageHeadDecorTexture = ContentFinder<Texture2D>.Get(IconFolder + "Decorate1", false);
				if (pageHeadDecorTexture == null)
				{
					Log.WarningOnce("DreamsOutposts UI: page head decor texture missing: Textures/" + IconFolder
						+ "Decorate1.png.", GenText.StableStringHash("ui-page-head-decor"));
				}
			}
			return pageHeadDecorTexture;
		}

		/// <summary>设施页据点升级时，从等级卡底部向上扫过的柔边白光。</summary>
		public static Texture2D LevelUpgradeSweepTexture()
		{
			if (levelUpgradeSweepTexture == null)
			{
				levelUpgradeSweepTexture = ContentFinder<Texture2D>.Get(IconFolder + "UpgradeSweep", false);
				if (levelUpgradeSweepTexture == null)
				{
					Log.WarningOnce("DreamsOutposts UI: upgrade sweep texture missing: Textures/" + IconFolder
						+ "UpgradeSweep.png.", GenText.StableStringHash("ui-upgrade-sweep"));
				}
			}
			return levelUpgradeSweepTexture;
		}

		/// <summary>肯定性操作（建造 / 招募）按钮的白色透明底图，绘制时按主题色染色。</summary>
		public static Texture2D PositiveButtonTexture()
		{
			if (positiveButtonTexture == null)
			{
				positiveButtonTexture = ContentFinder<Texture2D>.Get(IconFolder + "PositiveButton", false);
				if (positiveButtonTexture == null)
				{
					Log.WarningOnce("DreamsOutposts UI: positive button texture missing: Textures/" + IconFolder
						+ "PositiveButton.png.", GenText.StableStringHash("ui-positive-button"));
				}
			}
			return positiveButtonTexture;
		}

		/// <summary>拆除按钮的白色透明底图：与建造按钮完全同规格，绘制时按破坏性语义色（红）染色。</summary>
		public static Texture2D NegativeButtonTexture()
		{
			if (negativeButtonTexture == null)
			{
				negativeButtonTexture = ContentFinder<Texture2D>.Get(IconFolder + "NegativeButton", false);
				if (negativeButtonTexture == null)
				{
					Log.WarningOnce("DreamsOutposts UI: negative button texture missing: Textures/" + IconFolder
						+ "NegativeButton.png.", GenText.StableStringHash("ui-negative-button"));
				}
			}
			return negativeButtonTexture;
		}

		public static Texture2D NewTexture(int width, int height)
		{
			Texture2D texture = new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height), TextureFormat.RGBA32, false);
			texture.filterMode = FilterMode.Bilinear;
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.hideFlags = HideFlags.HideAndDontSave;
			return texture;
		}

		public static int Pack(Color color)
		{
			int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
			int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
			int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
			int a = Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
			return (r << 24) | (g << 16) | (b << 8) | a;
		}

		// ---------------------------------------------------------------
		// 圆角矩形（9 宫格）：角单元 = 半径，边/中心 = 2 倍半径
		// ---------------------------------------------------------------

		public static Texture2D Box(int radius, Color fill, Color border, int borderWidth)
		{
			BoxKey key = default(BoxKey);
			key.Radius = Mathf.Max(radius, 1);
			key.BorderWidth = Mathf.Max(borderWidth, 0);
			key.Fill = Pack(fill);
			key.Border = Pack(border);
			Texture2D cached;
			if (boxCache.TryGetValue(key, out cached))
			{
				return cached;
			}
			Texture2D texture = BuildBox(key.Radius, fill, border, key.BorderWidth);
			boxCache[key] = texture;
			return texture;
		}

		private static Texture2D BuildBox(int radius, Color fill, Color border, int borderWidth)
		{
			int size = radius * 4;
			Texture2D texture = NewTexture(size, size);
			Color[] pixels = new Color[size * size];
			float samples = Supersample * Supersample;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float outer = 0f;
					float inner = 0f;
					for (int sy = 0; sy < Supersample; sy++)
					{
						for (int sx = 0; sx < Supersample; sx++)
						{
							float px = x + (sx + 0.5f) / Supersample;
							float py = y + (sy + 0.5f) / Supersample;
							float sd = BoxSignedDistance(px, py, radius, size);
							if (sd > 0f)
							{
								outer += 1f;
							}
							if (sd > borderWidth)
							{
								inner += 1f;
							}
						}
					}
					outer /= samples;
					inner /= samples;
					float borderCoverage = Mathf.Clamp01(outer - inner) * border.a;
					float fillCoverage = inner * fill.a;
					float alpha = Mathf.Clamp01(borderCoverage + fillCoverage);
					if (alpha <= 0.0001f)
					{
						pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
						continue;
					}
					float r = (border.r * borderCoverage + fill.r * fillCoverage) / alpha;
					float g = (border.g * borderCoverage + fill.g * fillCoverage) / alpha;
					float b = (border.b * borderCoverage + fill.b * fillCoverage) / alpha;
					pixels[y * size + x] = new Color(r, g, b, alpha);
				}
			}
			texture.SetPixels(pixels);
			texture.Apply(false, false);
			return texture;
		}

		/// <summary>9 宫格贴图里某个像素到形状边界的带符号距离（正数在内）。坐标以左上角为原点。</summary>
		private static float BoxSignedDistance(float x, float y, int radius, int size)
		{
			float r = radius;
			float s = size;
			bool left = x < r;
			bool right = x > s - r;
			bool top = y < r;
			bool bottom = y > s - r;
			if ((left || right) && (top || bottom))
			{
				float dx = left ? (r - x) : (x - (s - r));
				float dy = top ? (r - y) : (y - (s - r));
				return r - Mathf.Sqrt(dx * dx + dy * dy);
			}
			return Mathf.Min(Mathf.Min(x, s - x), Mathf.Min(y, s - y));
		}

		// ---------------------------------------------------------------
		// 阴影：带模糊的圆角矩形（9 宫格，角单元 = 半径 + 模糊）
		// ---------------------------------------------------------------

		public static Texture2D Shadow(int radius)
		{
			int r = Mathf.Max(radius, 1);
			TwoKey key = default(TwoKey);
			key.A = r;
			key.B = 0;
			Texture2D cached;
			if (shadowCache.TryGetValue(key, out cached))
			{
				return cached;
			}
			Texture2D texture = BuildShadow(r);
			shadowCache[key] = texture;
			return texture;
		}

		private static Texture2D BuildShadow(int radius)
		{
			int blur = Mathf.RoundToInt(ShadowBlur);
			int cell = radius + blur;
			int size = cell * 2 + radius * 2;
			float[] alpha = new float[size * size];
			float samples = Supersample * Supersample;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float hits = 0f;
					for (int sy = 0; sy < Supersample; sy++)
					{
						for (int sx = 0; sx < Supersample; sx++)
						{
							float px = x + (sx + 0.5f) / Supersample - blur;
							float py = y + (sy + 0.5f) / Supersample - blur;
							float cx = radius * 2f;
							float cy = radius * 2f;
							float hx = radius * 2f;
							float hy = radius * 2f;
							float qx = Mathf.Abs(px - cx) - (hx - radius);
							float qy = Mathf.Abs(py - cy) - (hy - radius);
							float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
							float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
							if (outside + inside - radius <= 0f)
							{
								hits += 1f;
							}
						}
					}
					alpha[y * size + x] = hits / samples;
				}
			}
			// 两次盒式模糊近似高斯
			int blurRadius = Mathf.Max(1, blur / 2);
			BoxBlur(alpha, size, blurRadius);
			BoxBlur(alpha, size, blurRadius);
			Texture2D texture = NewTexture(size, size);
			Color[] pixels = new Color[size * size];
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha[i]) * ShadowAlpha);
			}
			texture.SetPixels(pixels);
			texture.Apply(false, false);
			return texture;
		}

		private static void BoxBlur(float[] values, int size, int radius)
		{
			float[] temp = new float[values.Length];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float sum = 0f;
					int count = 0;
					for (int i = -radius; i <= radius; i++)
					{
						int sx = Mathf.Clamp(x + i, 0, size - 1);
						sum += values[y * size + sx];
						count++;
					}
					temp[y * size + x] = sum / count;
				}
			}
			for (int x = 0; x < size; x++)
			{
				for (int y = 0; y < size; y++)
				{
					float sum = 0f;
					int count = 0;
					for (int i = -radius; i <= radius; i++)
					{
						int sy = Mathf.Clamp(y + i, 0, size - 1);
						sum += temp[sy * size + x];
						count++;
					}
					values[y * size + x] = sum / count;
				}
			}
		}

		public static float ShadowCornerSize(int radius)
		{
			return Mathf.Max(radius, 1) + ShadowBlur;
		}

		// ---------------------------------------------------------------
		// 虚线圆角：4 个角圆弧贴图 + 直边短划（短划由 UiDraw 画）
		// ---------------------------------------------------------------

		/// <summary>corner：0=左上 1=右上 2=左下 3=右下。</summary>
		public static Texture2D CornerArc(int radius, Color color, int corner)
		{
			int r = Mathf.Max(radius, 2);
			TwoKey key = default(TwoKey);
			key.A = r * 4 + corner;
			key.B = Pack(color);
			Texture2D cached;
			if (cornerCache.TryGetValue(key, out cached))
			{
				return cached;
			}
			int size = r * Supersample;
			bool[] bits = new bool[size * size];
			float cx = (corner == 0 || corner == 2) ? r : 0f;
			float cy = (corner == 0 || corner == 1) ? r : 0f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float px = (x + 0.5f) / Supersample;
					float py = (y + 0.5f) / Supersample;
					float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
					bits[y * size + x] = Mathf.Abs(d - r) <= 0.5f;
				}
			}
			Texture2D texture = NewTexture(r, r);
			Color[] pixels = new Color[r * r];
			float inv = 1f / (Supersample * Supersample);
			for (int y = 0; y < r; y++)
			{
				for (int x = 0; x < r; x++)
				{
					int hits = 0;
					for (int sy = 0; sy < Supersample; sy++)
					{
						for (int sx = 0; sx < Supersample; sx++)
						{
							if (bits[(y * Supersample + sy) * size + x * Supersample + sx])
							{
								hits++;
							}
						}
					}
					pixels[(r - 1 - y) * r + x] = new Color(color.r, color.g, color.b, hits * inv * color.a);
				}
			}
			texture.SetPixels(pixels);
			texture.Apply(false, false);
			cornerCache[key] = texture;
			return texture;
		}

		// ---------------------------------------------------------------
		// 炫彩边框（传说卡）：颜色按「沿圆角矩形轮廓的弧长」渐变，随帧偏移流动
		// ---------------------------------------------------------------

		/// <summary>色相表项数（256 项 = 每项 1.4°，肉眼已看不出分段）。必须是 2 的幂。</summary>
		public const int RainbowLutSize = 256;

		/// <summary>炫彩描边的饱和度：略低于 1，免得深色主题上满屏刺眼的纯色。</summary>
		private const float RainbowSaturation = 0.9f;

		private static readonly Color[] rainbowLut = new Color[RainbowLutSize];

		private static int rainbowLutIndex = int.MinValue;

		private struct RainbowKey : IEquatable<RainbowKey>
		{
			public int Band;

			public int CardWidth;

			public int CardHeight;

			public int Radius;

			public bool Equals(RainbowKey other)
			{
				return Band == other.Band && CardWidth == other.CardWidth && CardHeight == other.CardHeight && Radius == other.Radius;
			}

			public override bool Equals(object obj)
			{
				return obj is RainbowKey && Equals((RainbowKey)obj);
			}

			public override int GetHashCode()
			{
				return ((Band * 397 ^ CardWidth) * 397 ^ CardHeight) * 397 ^ Radius;
			}
		}

		private class RainbowBandEntry
		{
			public Texture2D Texture;

			public Color[] Pixels;
		}

		private static readonly Dictionary<RainbowKey, RainbowBandEntry> rainbowBandCache = new Dictionary<RainbowKey, RainbowBandEntry>();

		/// <summary>缓存条目上限：只有卡片尺寸变化才会新建条目，超过就整表清掉重新来。</summary>
		private const int RainbowCacheLimit = 16;

		/// <summary>把「绕了几圈」的小数换算成色相表索引。同一帧里所有边带共用一个索引。</summary>
		public static int RainbowHueIndex(float cycles)
		{
			return Mathf.FloorToInt(Mathf.Repeat(cycles, 1f) * RainbowLutSize);
		}

		/// <summary>
		/// 当前色相偏移下的色相表，索引里已经含了偏移（取像素色时不必再加）。
		/// 同一个索引只算一次：一帧内所有传说卡的边带共用它。
		/// </summary>
		private static Color[] RainbowLut(int hueIndex)
		{
			int index = hueIndex & (RainbowLutSize - 1);
			if (index != rainbowLutIndex)
			{
				for (int i = 0; i < RainbowLutSize; i++)
				{
					rainbowLut[i] = HueToRgb(((i + index) & (RainbowLutSize - 1)) / (float)RainbowLutSize);
				}
				rainbowLutIndex = index;
			}
			return rainbowLut;
		}

		/// <summary>HSV(h, RainbowSaturation, 1) 转 RGB。自己写，不依赖 Unity 版本里是否带 Color.HSVToRGB。</summary>
		private static Color HueToRgb(float hue)
		{
			float h = Mathf.Repeat(hue, 1f) * 6f;
			int sector = Mathf.Min((int)h, 5);
			float f = h - sector;
			float low = 1f - RainbowSaturation;
			float rise = 1f - RainbowSaturation * f;
			float fall = 1f - RainbowSaturation * (1f - f);
			switch (sector)
			{
				case 0: return new Color(1f, fall, low, 1f);
				case 1: return new Color(rise, 1f, low, 1f);
				case 2: return new Color(low, 1f, fall, 1f);
				case 3: return new Color(low, rise, 1f, 1f);
				case 4: return new Color(fall, low, 1f, 1f);
				default: return new Color(1f, low, rise, 1f);
			}
		}

		/// <summary>
		/// 炫彩边框的一条边带贴图。按「边 + 卡宽 + 卡高 + 半径」缓存贴图与像素数组，
		/// 但像素内容每帧按 hueIndex 重填：RGB 是沿轮廓弧长渐变的彩虹色，A 是描边的抗锯齿覆盖度。
		/// </summary>
		public static Texture2D RainbowBandTexture(RainbowBand band, Rect cardRect, int radius, float thickness, int hueIndex)
		{
			int cardWidth = Mathf.Max(Mathf.CeilToInt(cardRect.width), 4);
			int cardHeight = Mathf.Max(Mathf.CeilToInt(cardRect.height), 4);
			int r = ClampRadius(radius, cardWidth, cardHeight);
			bool horizontal = band == RainbowBand.Top || band == RainbowBand.Bottom;
			int width = horizontal ? cardWidth : r;
			int height = horizontal ? r : Mathf.Max(cardHeight - r * 2, 1);
			RainbowKey key = default(RainbowKey);
			key.Band = (int)band;
			key.CardWidth = cardWidth;
			key.CardHeight = cardHeight;
			key.Radius = r;
			RainbowBandEntry entry;
			if (!rainbowBandCache.TryGetValue(key, out entry))
			{
				if (rainbowBandCache.Count >= RainbowCacheLimit)
				{
					rainbowBandCache.Clear();
				}
				entry = new RainbowBandEntry();
				entry.Texture = NewTexture(width, height);
				entry.Pixels = new Color[width * height];
				rainbowBandCache[key] = entry;
			}
			FillRainbowBand(entry, band, cardWidth, cardHeight, r, thickness, hueIndex);
			entry.Texture.SetPixels(entry.Pixels);
			entry.Texture.Apply(false, false);
			return entry.Texture;
		}

		/// <summary>圆角半径的合法区间：至少 2，且不超过短边的一半。</summary>
		public static int ClampRadius(int radius, int width, int height)
		{
			return Mathf.Clamp(radius, 2, Mathf.FloorToInt(Mathf.Min(width, height) * 0.5f));
		}

		private static void FillRainbowBand(RainbowBandEntry entry, RainbowBand band, int cardWidth, int cardHeight,
			int r, float thickness, int hueIndex)
		{
			// 弧长从「上直边起点」(r, 0) 起算，顺时针绕一圈：上直边 a → 右上弧 b → 右直边 c → 右下弧 b
			// → 下直边 a → 左下弧 b → 左直边 c → 左上弧 b，回到起点即整圈 perimeter。
			// 四段边带必须共用这一个原点，否则圆角处色带会错位（直边长度算到 0 时还会直接裂开）。
			float a = cardWidth - r * 2f;      // 上下两条直边长
			float c = cardHeight - r * 2f;     // 左右两条直边长
			float b = Mathf.PI * r * 0.5f;     // 四分之一圆弧长
			float perimeter = 2f * a + 2f * c + 4f * b;
			float indexScale = RainbowLutSize / perimeter;
			float halfPi = Mathf.PI * 0.5f;
			float bottomStart = a + b + c;     // 右下角圆弧的起点（沿轮廓的弧长）
			Color[] lut = RainbowLut(hueIndex);
			int width = entry.Texture.width;
			int height = entry.Texture.height;
			Color[] pixels = entry.Pixels;
			// 边带在卡片局部坐标里的左上角：上=原点，下=贴着下沿，左/右=避开上下两个角。
			float originX = (band == RainbowBand.Right) ? (cardWidth - r) : 0f;
			float originY = 0f;
			if (band == RainbowBand.Bottom)
			{
				originY = cardHeight - r;
			}
			else if (band == RainbowBand.Left || band == RainbowBand.Right)
			{
				originY = r;
			}
			for (int j = 0; j < height; j++)
			{
				float py = originY + j + 0.5f;
				for (int i = 0; i < width; i++)
				{
					float px = originX + i + 0.5f;
					float arc;    // 沿轮廓的弧长位置
					float depth;  // 到轮廓的距离，内侧为正
					if (band == RainbowBand.Top)
					{
						if (px < r)
						{
							// 左上角：圆弧从 (r, 0) 逆着走到 (0, r)，所以弧长要从整圈往回量。
							float dx = px - r;
							float dy = py - r;
							float theta = Mathf.Atan2(dy, dx);
							arc = perimeter - r * (-halfPi - theta);
							depth = r - Mathf.Sqrt(dx * dx + dy * dy);
						}
						else if (px < cardWidth - r)
						{
							arc = px - r;
							depth = py;
						}
						else
						{
							// 右上角：从 (cardWidth-r, 0) 顺时针走到 (cardWidth, r)。
							float dx = px - (cardWidth - r);
							float dy = py - r;
							float theta = Mathf.Atan2(dy, dx);
							arc = a + r * (theta + halfPi);
							depth = r - Mathf.Sqrt(dx * dx + dy * dy);
						}
					}
					else if (band == RainbowBand.Bottom)
					{
						if (px >= cardWidth - r)
						{
							float dx = px - (cardWidth - r);
							float dy = py - (cardHeight - r);
							float theta = Mathf.Atan2(dy, dx);
							arc = bottomStart + r * theta;
							depth = r - Mathf.Sqrt(dx * dx + dy * dy);
						}
						else if (px >= r)
						{
							arc = bottomStart + b + ((cardWidth - r) - px);
							depth = cardHeight - py;
						}
						else
						{
							float dx = px - r;
							float dy = py - (cardHeight - r);
							float theta = Mathf.Atan2(dy, dx);
							arc = bottomStart + b + a + r * (theta - halfPi);
							depth = r - Mathf.Sqrt(dx * dx + dy * dy);
						}
					}
					else if (band == RainbowBand.Left)
					{
						arc = 2f * a + 3f * b + c + ((cardHeight - r) - py);
						depth = px;
					}
					else
					{
						arc = a + b + (py - r);
						depth = cardWidth - px;
					}
					// 覆盖度 = 像素区间落在 [0, thickness] 里的比例（与 9 宫格边框同一种边界平滑度）。
					float coverage = Mathf.Clamp01(thickness - depth + 0.5f) - Mathf.Clamp01(0.5f - depth);
					Color color = lut[((int)(arc * indexScale)) & (RainbowLutSize - 1)];
					// SetPixels 的第 0 行是贴图的**最下面**一行，而上面所有 py 都是「左上角为原点」的屏幕坐标，
					// 所以写的时候要把行号翻过来（CornerArc 用的是同一招），否则每段边带都会被上下镜像：
					// 上下两条边带会缩到卡内 6px 处、圆角变成对角的形状，左右两条边的渐变方向也会反过来。
					// 翻行号后形状与颜色同时归位，不需要把上下两段互换。
					int row = (height - 1 - j) * width;
					// 全透明像素也写上 RGB，避免缩小时双线性采样把透明黑混进边缘。
					pixels[row + i] = new Color(color.r, color.g, color.b, coverage);
				}
			}
		}

		// ---------------------------------------------------------------
		// 几何图标
		/// <summary>把 9 宫格贴图画到 rect，cornerUv 为角单元在贴图里的 UV 边长比例。</summary>
		public static void DrawNine(Rect rect, Texture2D texture, float cornerUv, float cornerSize)
		{
			DrawNine(rect, texture, cornerUv, cornerSize, UiCorners.All);
		}

		/// <summary>
		/// 9 宫格绘制，可指定哪些角是圆角；未指定的角用贴图中心色块补成直角。
		/// </summary>
		public static void DrawNine(Rect rect, Texture2D texture, float cornerUv, float cornerSize, UiCorners corners)
		{
			if (texture == null || rect.width <= 0f || rect.height <= 0f)
			{
				return;
			}
			float a = Mathf.Min(cornerSize, Mathf.Min(rect.width * 0.5f, rect.height * 0.5f));
			if (a <= 0f)
			{
				return;
			}
			float f = Mathf.Clamp01(cornerUv);
			float innerUv = Mathf.Max(1f - f * 2f, 0f);
			Rect topLeft = new Rect(rect.x, rect.y, a, a);
			Rect topRight = new Rect(rect.xMax - a, rect.y, a, a);
			Rect bottomLeft = new Rect(rect.x, rect.yMax - a, a, a);
			Rect bottomRight = new Rect(rect.xMax - a, rect.yMax - a, a, a);
			DrawCorner(topLeft, new Rect(0f, 0f, f, f), texture, (corners & UiCorners.TopLeft) != 0, innerUv, f);
			DrawCorner(topRight, new Rect(1f - f, 0f, f, f), texture, (corners & UiCorners.TopRight) != 0, innerUv, f);
			DrawCorner(bottomLeft, new Rect(0f, 1f - f, f, f), texture, (corners & UiCorners.BottomLeft) != 0, innerUv, f);
			DrawCorner(bottomRight, new Rect(1f - f, 1f - f, f, f), texture, (corners & UiCorners.BottomRight) != 0, innerUv, f);
			// 四条边
			float edgeWidth = rect.width - a * 2f;
			float edgeHeight = rect.height - a * 2f;
			if (edgeWidth > 0f)
			{
				Widgets.DrawTexturePart(new Rect(rect.x + a, rect.y, edgeWidth, a), new Rect(f, 0f, innerUv, f), texture);
				Widgets.DrawTexturePart(new Rect(rect.x + a, rect.yMax - a, edgeWidth, a), new Rect(f, 1f - f, innerUv, f), texture);
			}
			if (edgeHeight > 0f)
			{
				Widgets.DrawTexturePart(new Rect(rect.x, rect.y + a, a, edgeHeight), new Rect(0f, f, f, innerUv), texture);
				Widgets.DrawTexturePart(new Rect(rect.xMax - a, rect.y + a, a, edgeHeight), new Rect(1f - f, f, f, innerUv), texture);
			}
			if (edgeWidth > 0f && edgeHeight > 0f)
			{
				Widgets.DrawTexturePart(new Rect(rect.x + a, rect.y + a, edgeWidth, edgeHeight), new Rect(f, f, innerUv, innerUv), texture);
			}
		}

		private static void DrawCorner(Rect cornerRect, Rect uv, Texture2D texture, bool rounded, float innerUv, float f)
		{
			if (rounded)
			{
				Widgets.DrawTexturePart(cornerRect, uv, texture);
			}
			else
			{
				// 直角：用中心色块填满该角
				Widgets.DrawTexturePart(cornerRect, new Rect(f, f, innerUv, innerUv), texture);
			}
		}
	}

	/// <summary>圆角掩码：哪些角要画成圆角。</summary>
	public enum UiCorners
	{
		None = 0,
		TopLeft = 1,
		TopRight = 2,
		BottomLeft = 4,
		BottomRight = 8,
		All = TopLeft | TopRight | BottomLeft | BottomRight
	}
}
