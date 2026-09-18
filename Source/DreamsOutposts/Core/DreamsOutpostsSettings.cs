using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class DreamsOutpostsSettings : ModSettings
	{
		public const float DefaultProductionMultiplier = 1f;
		public const float MinProductionMultiplier = 0f;
		public const float MaxProductionMultiplier = 10f;

		public float productionMultiplier = DefaultProductionMultiplier;

		public bool useVanillaUi = false;

		/// <summary>是否在每次新开局 / 读档后弹出开局指引弹窗。</summary>
		public bool showIntroTips = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref productionMultiplier, "productionMultiplier", DefaultProductionMultiplier);
			Scribe_Values.Look(ref useVanillaUi, "useVanillaUi", false);
			Scribe_Values.Look(ref showIntroTips, "showIntroTips", true);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				ClampValues();
			}
		}

		public void ClampValues()
		{
			if (float.IsNaN(productionMultiplier) || float.IsInfinity(productionMultiplier))
			{
				productionMultiplier = DefaultProductionMultiplier;
			}
			productionMultiplier = Mathf.Clamp(productionMultiplier, MinProductionMultiplier, MaxProductionMultiplier);
		}
	}
}
