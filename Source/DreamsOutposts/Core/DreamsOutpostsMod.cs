using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class DreamsOutpostsMod : Mod
	{
		// Widgets.FloatRange 用 id 区分拖拽中的滑块，两个滑块的 id 必须不同。
		private const int RandomEventIntervalSliderId = 7314211;

		private const int AttackIntervalSliderId = 7314212;

		// 滑块高度与原版设置里的范围控件保持一致。
		private const float RangeSliderHeight = 30f;

		/// <summary>设置界面页签内容区的固定高度：内容比一屏短，给足高度就不必先测一遍再画一遍。</summary>
		private const float SettingsContentHeight = 1200f;

		private static SettingsTab currentTab = SettingsTab.Appearance;

		private static Vector2 settingsScroll;

		public static Harmony harmony;

		public static DreamsOutpostsMod Instance;

		public static DreamsOutpostsSettings Settings;

		public static OutpostUiStyle UiStyle
		{
			get
			{
				return Settings != null
					? Settings.UiStyle
					: DreamsOutpostsSettings.DefaultUiStyle;
			}
		}

		public static bool IsUiStyle(OutpostUiStyle style)
		{
			return UiStyle == style;
		}

		public DreamsOutpostsMod(ModContentPack content)
			: base(content)
		{
			Instance = this;
			Settings = GetSettings<DreamsOutpostsSettings>();
			harmony = new Harmony("mjcg.DreamsOutposts");
			harmony.PatchAll();
			CompatibilityManager.ApplyAll(harmony);
		}

		public override string SettingsCategory()
		{
			return base.Content.Name;
		}

		/// <summary>设置界面的两个页签。</summary>
		private enum SettingsTab
		{
			/// <summary>外观：界面风格，以及各种提示类弹窗。</summary>
			Appearance,

			/// <summary>参数：产出倍率、随机事件与袭击的开关和间隔。</summary>
			Parameters
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			List<TabRecord> tabs = new List<TabRecord>
			{
				new TabRecord(
					"DreamsOutposts.Settings.TabAppearance".Translate(),
					delegate
					{
						currentTab = SettingsTab.Appearance;
						settingsScroll = Vector2.zero;
					},
					currentTab == SettingsTab.Appearance),
				new TabRecord(
					"DreamsOutposts.Settings.TabParameters".Translate(),
					delegate
					{
						currentTab = SettingsTab.Parameters;
						settingsScroll = Vector2.zero;
					},
					currentTab == SettingsTab.Parameters)
			};

			// TabDrawer.DrawTabs 会把页签画在传入矩形上方约 31px 处；把基准矩形下移 32px，
			// 页签就落在内容区顶部、Mod 名称下面，不会盖住标题。
			Rect tabBase = new Rect(
				inRect.x,
				inRect.y + 32f,
				inRect.width,
				inRect.height - 32f);

			TabDrawer.DrawTabs<TabRecord>(tabBase, tabs, 200f);

			Rect outRect = new Rect(
				inRect.x + 10f,
				inRect.y + 52f,
				inRect.width - 20f,
				inRect.height - 62f);

			// 宽度里扣掉滚动条槽位，否则内容会钻到滚动条底下。
			Rect viewRect = new Rect(
				0f,
				0f,
				outRect.width - 16f,
				SettingsContentHeight);

			Widgets.BeginScrollView(
				outRect,
				ref settingsScroll,
				viewRect,
				true);

			Listing_Standard listing = new Listing_Standard();
			listing.Begin(viewRect);

			if (currentTab == SettingsTab.Appearance)
			{
				DrawAppearanceSettings(listing);
			}
			else
			{
				DrawParameterSettings(listing);
			}

			listing.End();
			Widgets.EndScrollView();
		}

		/// <summary>外观页：界面风格，以及开局指引与更新日志这两个提示入口。</summary>
		private static void DrawAppearanceSettings(Listing_Standard listing)
		{
			listing.Label(
				"DreamsOutposts.Settings.UiStyle".Translate());

			if (listing.ButtonText(
				OutpostUiStyles.Label(Settings.UiStyle)))
			{
				List<FloatMenuOption> options =
					new List<FloatMenuOption>();

				for (int i = 0; i < OutpostUiStyles.All.Length; i++)
				{
					OutpostUiStyle style =
						OutpostUiStyles.All[i];

					OutpostUiStyle captured = style;

					options.Add(
						new FloatMenuOption(
							OutpostUiStyles.Label(captured),
							delegate
							{
								Settings.UiStyle = captured;
							}));
				}

				Find.WindowStack.Add(
					new FloatMenu(options));
			}

			listing.Label(
				OutpostUiStyles.Description(Settings.UiStyle));

			listing.GapLine();

			listing.CheckboxLabeled(
				"DreamsOutposts.Settings.ShowIntroTips".Translate(),
				ref Settings.showIntroTips,
				"DreamsOutposts.Settings.ShowIntroTips.Description".Translate());

			if (listing.ButtonText(
				"DreamsOutposts.Settings.OpenIntroTips".Translate()))
			{
				UiIntroTipsWindow.Open();
			}

			listing.GapLine();

			if (listing.ButtonText(
				"DreamsOutposts.Settings.OpenUpdateLog".Translate()))
			{
				// 手动查看时列全部历史日志，不改变「已提示过的版本」记录。
				UiUpdateNoticeWindow.Open(UpdateNoticeUtility.AllNotices());
			}

			listing.Label(
				"DreamsOutposts.Settings.OpenUpdateLog.Description".Translate());
		}

		/// <summary>参数页：产出倍率，以及据点随机事件与袭击的开关、间隔。</summary>
		private static void DrawParameterSettings(Listing_Standard listing)
		{
			listing.Label(
				"DreamsOutposts.Settings.ProductionMultiplier"
					.Translate(Settings.productionMultiplier.ToStringPercent()));

			float value = listing.Slider(
				Settings.productionMultiplier,
				DreamsOutpostsSettings.MinProductionMultiplier,
				DreamsOutpostsSettings.MaxProductionMultiplier);

			Settings.productionMultiplier =
				Mathf.Round(value * 10f) / 10f;

			listing.Label(
				"DreamsOutposts.Settings.ProductionMultiplierDescription"
					.Translate());

			listing.GapLine();

			listing.Label(
				"DreamsOutposts.Settings.RandomEvents".Translate());

			listing.CheckboxLabeled(
				"DreamsOutposts.Settings.RandomEvents.Enable".Translate(),
				ref Settings.randomEventsEnabled);

			if (Settings.randomEventsEnabled)
			{
				DrawIntervalRange(
					listing,
					RandomEventIntervalSliderId,
					ref Settings.randomEventIntervalDays,
					"DreamsOutposts.Settings.RandomEvents.Interval");
			}

			listing.Label(
				"DreamsOutposts.Settings.RandomEvents.Description".Translate());

			listing.GapLine();

			listing.Label(
				"DreamsOutposts.Settings.Attacks".Translate());

			listing.CheckboxLabeled(
				"DreamsOutposts.Settings.Attacks.Enable".Translate(),
				ref Settings.attacksEnabled);

			if (Settings.attacksEnabled)
			{
				DrawIntervalRange(
					listing,
					AttackIntervalSliderId,
					ref Settings.attackIntervalDays,
					"DreamsOutposts.Settings.Attacks.Interval");
			}

			listing.Label(
				"DreamsOutposts.Settings.Attacks.Description".Translate());
		}

		/// <summary>
		/// 画一个「间隔：X - Y 天」的原版范围滑块（Widgets.FloatRange）。
		/// 两端都可拖到 MinIntervalDays ~ MaxIntervalDays；gap = 0 允许两端相等，等于「固定每 X 天一次」；
		/// roundTo 让结果始终落回整天，与调度器按整天排期的假设一致。
		/// </summary>
		private static void DrawIntervalRange(
			Listing_Standard listing,
			int sliderId,
			ref FloatRange range,
			string labelKey)
		{
			Widgets.FloatRange(
				listing.GetRect(RangeSliderHeight),
				sliderId,
				ref range,
				min: DreamsOutpostsSettings.MinIntervalDays,
				max: DreamsOutpostsSettings.MaxIntervalDays,
				labelKey: labelKey,
				valueStyle: ToStringStyle.Integer,
				gap: 0f,
				roundTo: 1f);
		}

		public override void WriteSettings()
		{
			Settings.ClampValues();
			base.WriteSettings();
		}
	}
}