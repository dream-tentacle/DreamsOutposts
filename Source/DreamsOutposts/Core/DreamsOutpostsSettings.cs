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

		public static readonly OutpostUiStyle DefaultUiStyle = OutpostUiStyle.Vanilla;

		public float productionMultiplier = DefaultProductionMultiplier;

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

			OutpostUiStyle style = (OutpostUiStyle)uiStyleValue;
			if (!OutpostUiStyles.IsDefined(style))
			{
				uiStyleValue = (int)DefaultUiStyle;
			}

			legacyUseVanillaUi =
				UiStyle == OutpostUiStyle.Vanilla;
		}
	}
}