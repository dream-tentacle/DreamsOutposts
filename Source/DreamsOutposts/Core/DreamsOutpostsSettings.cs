using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public enum OutpostUiStyle
	{
		Vanilla = 0,
		ModernTech = 1
	}

	/// <summary>
	/// 所有可选据点 UI 风格的统一目录。
	/// 后续新增风格时，在 enum 与 All 中增加一项，并补对应名称 / 说明即可。
	/// </summary>
	public static class OutpostUiStyles
	{
		public static readonly OutpostUiStyle[] All =
		{
			OutpostUiStyle.Vanilla,
			OutpostUiStyle.ModernTech
		};

		public static bool IsDefined(OutpostUiStyle style)
		{
			for (int i = 0; i < All.Length; i++)
			{
				if (All[i] == style)
				{
					return true;
				}
			}
			return false;
		}

		public static string Label(OutpostUiStyle style)
		{
			switch (style)
			{
			case OutpostUiStyle.Vanilla:
				return "DreamsOutposts.Settings.UiStyle.Vanilla".Translate().ToString();

			case OutpostUiStyle.ModernTech:
			default:
				return "DreamsOutposts.Settings.UiStyle.ModernTech".Translate().ToString();
			}
		}

		public static string Description(OutpostUiStyle style)
		{
			switch (style)
			{
			case OutpostUiStyle.Vanilla:
				return "DreamsOutposts.Settings.UiStyle.Vanilla.Description".Translate().ToString();

			case OutpostUiStyle.ModernTech:
			default:
				return "DreamsOutposts.Settings.UiStyle.ModernTech.Description".Translate().ToString();
			}
		}
	}

	public class DreamsOutpostsSettings : ModSettings
	{
		public const float DefaultProductionMultiplier = 1f;
		public const float MinProductionMultiplier = 0f;
		public const float MaxProductionMultiplier = 10f;

		// 事件与袭击的频率都写成「天数区间」，两端统一限制在这个范围内。
		public const float MinIntervalDays = 1f;
		public const float MaxIntervalDays = 60f;

		public static readonly FloatRange DefaultRandomEventInterval = new FloatRange(3f, 6f);

		public static readonly FloatRange DefaultAttackInterval = new FloatRange(14f, 21f);

		public static readonly OutpostUiStyle DefaultUiStyle = OutpostUiStyle.Vanilla;

		public float productionMultiplier = DefaultProductionMultiplier;

		/// <summary>据点普通随机事件的全局开关。关闭后不再生成随机事件。</summary>
		public bool randomEventsEnabled = true;

		/// <summary>据点袭击的全局开关。关闭后不再生成新袭击，但已经开始的袭击仍会正常结算。</summary>
		public bool attacksEnabled = true;

		/// <summary>普通随机事件的间隔天数区间，两端都在 [MinIntervalDays, MaxIntervalDays] 内。</summary>
		public FloatRange randomEventIntervalDays = new FloatRange(3f, 6f);

		/// <summary>袭击的间隔天数区间，两端都在 [MinIntervalDays, MaxIntervalDays] 内。</summary>
		public FloatRange attackIntervalDays = new FloatRange(14f, 21f);

		// -1 是“尚未写入新 uiStyle 字段”的迁移哨兵。
		private int uiStyleValue = -1;

		// 只用于读取旧设置文件里的 useVanillaUi；不再直接参与绘制。
		private bool legacyUseVanillaUi;

		/// <summary>是否在每次新开局 / 读档后弹出开局指引弹窗。</summary>
		public bool showIntroTips = true;

		public OutpostUiStyle UiStyle
		{
			get
			{
				OutpostUiStyle style = (OutpostUiStyle)uiStyleValue;
				return OutpostUiStyles.IsDefined(style)
					? style
					: DefaultUiStyle;
			}
			set
			{
				OutpostUiStyle normalized = OutpostUiStyles.IsDefined(value)
					? value
					: DefaultUiStyle;

				uiStyleValue = (int)normalized;
				legacyUseVanillaUi = normalized == OutpostUiStyle.Vanilla;
			}
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(
				ref productionMultiplier,
				"productionMultiplier",
				DefaultProductionMultiplier);

			Scribe_Values.Look(
				ref uiStyleValue,
				"uiStyle",
				-1);

			// 兼容旧版本 ModSettings；新代码只读 UiStyle。
			Scribe_Values.Look(
				ref legacyUseVanillaUi,
				"useVanillaUi",
				false);

			Scribe_Values.Look(
				ref showIntroTips,
				"showIntroTips",
				true);

			Scribe_Values.Look(
				ref randomEventsEnabled,
				"randomEventsEnabled",
				true);

			Scribe_Values.Look(
				ref attacksEnabled,
				"attacksEnabled",
				true);

			Scribe_Values.Look(
				ref randomEventIntervalDays,
				"randomEventIntervalDays",
				DefaultRandomEventInterval);

			Scribe_Values.Look(
				ref attackIntervalDays,
				"attackIntervalDays",
				DefaultAttackInterval);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (uiStyleValue < 0)
				{
					uiStyleValue = legacyUseVanillaUi
						? (int)OutpostUiStyle.Vanilla
						: (int)DefaultUiStyle;
				}

				ClampValues();
			}
		}

		public void ClampValues()
		{
			if (float.IsNaN(productionMultiplier) ||
				float.IsInfinity(productionMultiplier))
			{
				productionMultiplier = DefaultProductionMultiplier;
			}

			productionMultiplier = Mathf.Clamp(
				productionMultiplier,
				MinProductionMultiplier,
				MaxProductionMultiplier);

			randomEventIntervalDays = ClampInterval(
				randomEventIntervalDays,
				DefaultRandomEventInterval);

			attackIntervalDays = ClampInterval(
				attackIntervalDays,
				DefaultAttackInterval);

			OutpostUiStyle style = (OutpostUiStyle)uiStyleValue;
			if (!OutpostUiStyles.IsDefined(style))
			{
				uiStyleValue = (int)DefaultUiStyle;
			}

			legacyUseVanillaUi =
				UiStyle == OutpostUiStyle.Vanilla;
		}

		/// <summary>
		/// 把间隔天数区间收进 [MinIntervalDays, MaxIntervalDays]，并取整到整天。
		/// 非法值（NaN / 无穷）整体回落到默认区间，保证调度器永远能拿到可用的天数。
		/// </summary>
		private static FloatRange ClampInterval(FloatRange range, FloatRange fallback)
		{
			if (float.IsNaN(range.min) || float.IsInfinity(range.min) ||
				float.IsNaN(range.max) || float.IsInfinity(range.max))
			{
				return fallback;
			}

			float min = Mathf.Round(
				Mathf.Clamp(range.min, MinIntervalDays, MaxIntervalDays));

			float max = Mathf.Round(
				Mathf.Clamp(range.max, MinIntervalDays, MaxIntervalDays));

			if (max < min)
			{
				max = min;
			}

			return new FloatRange(min, max);
		}
	}
}