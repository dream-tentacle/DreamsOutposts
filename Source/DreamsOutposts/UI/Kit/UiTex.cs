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

		private static Texture2D buildButtonTexture;

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

		/// <summary>扩建设施建造按钮的白色透明底图，绘制时按主题色染色。</summary>
		public static Texture2D BuildButtonTexture()
		{
			if (buildButtonTexture == null)
			{
				buildButtonTexture = ContentFinder<Texture2D>.Get(IconFolder + "BuildButton", false);
				if (buildButtonTexture == null)
				{
					Log.WarningOnce("DreamsOutposts UI: build button texture missing: Textures/" + IconFolder
						+ "BuildButton.png.", GenText.StableStringHash("ui-build-button"));
				}
			}
			return buildButtonTexture;
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
