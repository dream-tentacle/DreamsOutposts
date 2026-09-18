using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class DreamsOutpostsMod : Mod
	{
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

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(inRect);

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

			listing.End();
		}

		public override void WriteSettings()
		{
			Settings.ClampValues();
			base.WriteSettings();
		}
	}
}