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
			listing.Label("DreamsOutposts.Settings.ProductionMultiplier".Translate(Settings.productionMultiplier.ToStringPercent()));
			float value = listing.Slider(Settings.productionMultiplier,
				DreamsOutpostsSettings.MinProductionMultiplier,
				DreamsOutpostsSettings.MaxProductionMultiplier);
			Settings.productionMultiplier = Mathf.Round(value * 40f) / 40f;
			listing.GapLine();
			listing.Label("DreamsOutposts.Settings.ProductionMultiplierDescription".Translate());
			listing.End();
		}

		public override void WriteSettings()
		{
			Settings.ClampValues();
			base.WriteSettings();
		}
	}
}
